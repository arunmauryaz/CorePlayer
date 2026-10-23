using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CorePlayer.Engine;
using CorePlayer.Services;
using CorePlayer.ViewModels;

namespace CorePlayer.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private WindowState _previousWindowState = WindowState.Normal;
    private DispatcherTimer? _canvasClickTimer;
    private Point _lastPointerPosition;
    private readonly Cursor _hiddenCursor = new(StandardCursorType.None);
    private bool _isUpdatingFullscreen;
    private MainViewModel? _hookedViewModel;
    private DispatcherTimer? _volumeCollapseTimer;
    private bool _isPointerOverVolume;
    private bool _isDraggingVolumeSlider;

    public MainWindow()
    {
        InitializeComponent();

        var engine = new CorePlaybackEngine(PlayerView.MpvContext);
        var playback = new MpvPlaybackService(engine);
        var storage = new StorageService();
        DataContext = new MainViewModel(playback, storage);

        DataContextChanged += (_, _) => HookViewModel(ViewModel);
        HookViewModel(ViewModel);

        PlayerView.ViewInitialized += (s, e) =>
        {
            if (PlayerView.MpvContext != null && ViewModel != null)
            {
                ViewModel.AttachMpvContext(PlayerView.MpvContext);
            }
        };

        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(InputElement.PointerMovedEvent, OnWindowPointerMoved, RoutingStrategies.Tunnel);

        VolumeSlider.AddHandler(InputElement.PointerPressedEvent, OnVolumeSliderPointerPressed, RoutingStrategies.Tunnel);
        VolumeSlider.AddHandler(InputElement.PointerReleasedEvent, OnVolumeSliderPointerReleased, RoutingStrategies.Tunnel);
        VolumeSlider.AddHandler(InputElement.PointerCaptureLostEvent, (s, e) =>
        {
            _isDraggingVolumeSlider = false;
            if (!_isPointerOverVolume)
            {
                ExpandVolumeSlider(400);
            }
        }, RoutingStrategies.Tunnel);

        this.GetPropertyChangedObservable(WindowStateProperty).Subscribe(_ =>
        {
            if (_isUpdatingFullscreen) return;
            if (ViewModel == null) return;

            bool isFs = WindowState == WindowState.FullScreen;
            if (ViewModel.IsFullscreen != isFs)
            {
                // If the window is minimized, retain the fullscreen flag so restoring brings it back to fullscreen
                if (!isFs && WindowState == WindowState.Minimized)
                {
                    return;
                }

                _isUpdatingFullscreen = true;
                try
                {
                    ViewModel.IsFullscreen = isFs;
                }
                finally
                {
                    Dispatcher.UIThread.Post(() => _isUpdatingFullscreen = false, DispatcherPriority.Background);
                }
            }
        });
    }

    private void ExpandVolumeSlider(int holdDurationMs = 800)
    {
        VolumeControlBorder?.Classes.Set("expanded", true);
        _volumeCollapseTimer?.Stop();

        if (holdDurationMs > 0)
        {
            if (_volumeCollapseTimer == null)
            {
                _volumeCollapseTimer = new DispatcherTimer();
                _volumeCollapseTimer.Tick += (_, _) =>
                {
                    _volumeCollapseTimer.Stop();
                    if (!_isPointerOverVolume && !_isDraggingVolumeSlider)
                    {
                        VolumeControlBorder?.Classes.Set("expanded", false);
                    }
                };
            }
            _volumeCollapseTimer.Interval = TimeSpan.FromMilliseconds(holdDurationMs);
            _volumeCollapseTimer.Start();
        }
    }

    private void OnVolumePointerEntered(object? sender, PointerEventArgs e)
    {
        _isPointerOverVolume = true;
        ExpandVolumeSlider(0);
    }

    private void OnVolumePointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerOverVolume = false;
        if (!_isDraggingVolumeSlider)
        {
            ExpandVolumeSlider(350);
        }
    }

    private void OnVolumePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel == null) return;

        int step = e.Delta.Y > 0 ? 5 : -5;
        ViewModel.AdjustVolume(step);

        ExpandVolumeSlider(1000);
        e.Handled = true;
    }

    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDraggingVolumeSlider = true;
        ExpandVolumeSlider(0);
    }

    private void OnVolumeSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingVolumeSlider = false;
        if (!_isPointerOverVolume)
        {
            ExpandVolumeSlider(400);
        }
    }

    private void HookViewModel(MainViewModel? vm)
    {
        if (_hookedViewModel == vm) return;

        if (_hookedViewModel != null)
        {
            _hookedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _hookedViewModel = vm;

        if (_hookedViewModel != null)
        {
            _hookedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateFullscreenState(_hookedViewModel.IsFullscreen);
            UpdateCursorVisibility();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
        {
            UpdateFullscreenState(ViewModel.IsFullscreen);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsControlsVisible) || e.PropertyName == nameof(MainViewModel.IsPlaying))
        {
            UpdateCursorVisibility();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsMiniPlayer))
        {
            if (ViewModel.IsMiniPlayer)
            {
                Width = 440;
                Height = 250;
                WindowState = WindowState.Normal;
            }
        }
    }

    private void UpdateFullscreenState(bool isFullscreen)
    {
        if (_isUpdatingFullscreen) return;
        _isUpdatingFullscreen = true;
        try
        {
            if (isFullscreen)
            {
                if (WindowState != WindowState.FullScreen)
                {
                    _previousWindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState;
                    WindowState = WindowState.FullScreen;
                }
            }
            else
            {
                if (WindowState == WindowState.FullScreen)
                {
                    WindowState = _previousWindowState == WindowState.FullScreen ? WindowState.Normal : _previousWindowState;
                }
            }
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingFullscreen = false;
            }, DispatcherPriority.Background);
        }
    }

    private void UpdateCursorVisibility()
    {
        if (ViewModel != null && !ViewModel.IsControlsVisible && ViewModel.IsPlaying)
        {
            if (Cursor != _hiddenCursor)
            {
                Cursor = _hiddenCursor;
            }
        }
        else
        {
            if (Cursor != Cursor.Default)
            {
                Cursor = Cursor.Default;
            }
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDraggingOver = true;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDraggingOver = false;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsDraggingOver = false;
        }

        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0 || ViewModel == null) return;

        string[] subtitleExts = [".srt", ".ass", ".vtt", ".sub"];

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (subtitleExts.Contains(ext))
            {
                ViewModel.AddSubtitleFile(path);
            }
            else
            {
                ViewModel.OpenFile(path);
            }
        }
    }

    private void OnTopBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Do not initiate drag if user clicked on interactive elements (Buttons, Menus, MenuItems)
            if (e.Source is Visual visual)
            {
                if (visual.FindAncestorOfType<Button>() != null ||
                    visual.FindAncestorOfType<MenuItem>() != null ||
                    visual.FindAncestorOfType<Menu>() != null)
                {
                    return;
                }
            }

            if (e.ClickCount == 2)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else if (WindowState == WindowState.Normal)
                {
                    WindowState = WindowState.Maximized;
                }
                return;
            }

            if (WindowState != WindowState.FullScreen)
            {
                BeginMoveDrag(e);
            }
        }
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPoint = e.GetPosition(this);
        var deltaX = Math.Abs(currentPoint.X - _lastPointerPosition.X);
        var deltaY = Math.Abs(currentPoint.Y - _lastPointerPosition.Y);

        // Filter out tiny jitter or synthetic pointer messages (e.g. emitted when Cursor is changed)
        if (deltaX < 2 && deltaY < 2)
        {
            return;
        }

        _lastPointerPosition = currentPoint;

        if (Cursor != Cursor.Default)
        {
            Cursor = Cursor.Default;
        }

        ViewModel?.ShowControls();
    }

    private void OnVideoAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        OnWindowPointerMoved(sender, e);
    }

    private void OnVideoAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsMoreMenuOpen = false;
            ViewModel.IsTracksMenuOpen = false;
            ViewModel.IsQueueOpen = false;
            ViewModel.ShowControls();

            // When no video is playing, clicking anywhere on the black canvas drags the window!
            if (!ViewModel.HasMedia && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && WindowState != WindowState.FullScreen)
            {
                BeginMoveDrag(e);
                return;
            }

            // Single click on video canvas toggles Play/Pause (delayed slightly to allow double-click fullscreen)
            if (ViewModel.HasMedia && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (_canvasClickTimer == null)
                {
                    _canvasClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
                    _canvasClickTimer.Tick += (_, _) =>
                    {
                        _canvasClickTimer.Stop();
                        ViewModel?.TogglePlayPause();
                    };
                }

                _canvasClickTimer.Stop();
                _canvasClickTimer.Start();
            }
        }
    }

    private void OnVideoAreaDoubleTapped(object? sender, TappedEventArgs e)
    {
        _canvasClickTimer?.Stop();
        ViewModel?.ToggleFullscreen();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (ViewModel == null) return;
        if (FocusManager?.GetFocusedElement() is TextBox) return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.K:
                ViewModel.TogglePlayPause();
                e.Handled = true;
                break;

            case Key.Left:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    ViewModel.PlayPreviousTrack();
                else
                    ViewModel.SeekBackward5();
                e.Handled = true;
                break;

            case Key.Right:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    ViewModel.PlayNextTrack();
                else
                    ViewModel.SeekForward5();
                e.Handled = true;
                break;

            case Key.J:
                ViewModel.SeekBackward10();
                e.Handled = true;
                break;

            case Key.L:
                ViewModel.SeekForward10();
                e.Handled = true;
                break;

            case Key.Up:
                ViewModel.VolumeUp();
                e.Handled = true;
                break;

            case Key.Down:
                ViewModel.VolumeDown();
                e.Handled = true;
                break;

            case Key.M:
                ViewModel.ToggleMute();
                e.Handled = true;
                break;

            case Key.F:
            case Key.F11:
                ViewModel.ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.Escape:
                if (ViewModel.IsFullscreen)
                {
                    ViewModel.ToggleFullscreen();
                    e.Handled = true;
                }
                else if (ViewModel.IsTracksMenuOpen || ViewModel.IsMoreMenuOpen || ViewModel.IsQueueOpen || ViewModel.IsInfoModalOpen)
                {
                    ViewModel.IsTracksMenuOpen = false;
                    ViewModel.IsMoreMenuOpen = false;
                    ViewModel.IsQueueOpen = false;
                    ViewModel.IsInfoModalOpen = false;
                    e.Handled = true;
                }
                break;

            case Key.Q:
                ViewModel.ToggleQueue();
                e.Handled = true;
                break;

            case Key.T:
            case Key.C:
                ViewModel.ToggleTracksMenu();
                e.Handled = true;
                break;

            case Key.I:
                ViewModel.ToggleInfoModal();
                e.Handled = true;
                break;

            case Key.S:
                ViewModel.TakeSnapshot();
                e.Handled = true;
                break;

            case Key.OemPeriod:
                ViewModel.StepFrame(true);
                e.Handled = true;
                break;

            case Key.OemComma:
                ViewModel.StepFrame(false);
                e.Handled = true;
                break;

            case Key.B:
                ViewModel.CycleAudioTrack();
                e.Handled = true;
                break;

            case Key.V:
                ViewModel.CycleSubtitleTrack();
                e.Handled = true;
                break;

            case Key.N when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                ViewModel.PlayNextTrack();
                e.Handled = true;
                break;

            case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                ViewModel.PlayPreviousTrack();
                e.Handled = true;
                break;

            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                OnOpenFileClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Media File",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Video Files")
                {
                    Patterns = ["*.mp4", "*.mkv", "*.webm", "*.avi", "*.mov", "*.flv", "*.wmv", "*.ts", "*.m4v"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            ]
        });

        if (files.Count > 0)
        {
            foreach (var f in files)
            {
                ViewModel.OpenFile(f.Path.LocalPath);
            }
        }
    }

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder with Media Files"
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            string[] validExts = [".mp4", ".mkv", ".webm", ".avi", ".mov", ".flv", ".wmv", ".ts", ".m4v"];
            var files = Directory.GetFiles(folder.Path.LocalPath)
                .Where(f => validExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                ViewModel.OpenFile(file);
            }
        }
    }

    private async void OnAddExternalSubtitleClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load External Subtitles",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Subtitle Files")
                {
                    Patterns = ["*.srt", "*.ass", "*.vtt", "*.sub"]
                }
            ]
        });

        if (files.Count > 0)
        {
            ViewModel.AddSubtitleFile(files[0].Path.LocalPath);
        }
    }

    private void OnOpenUrlClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ShowToast("Open URL: Direct stream dialog coming soon");
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ShowToast("CORE PLAYER v1.0 • Liquid Glass Edition");
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.FullScreen || ViewModel?.IsFullscreen == true)
        {
            if (ViewModel != null)
            {
                ViewModel.IsFullscreen = false;
            }
            else
            {
                WindowState = _previousWindowState == WindowState.FullScreen ? WindowState.Normal : _previousWindowState;
            }
            return;
        }

        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}