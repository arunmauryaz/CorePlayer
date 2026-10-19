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

    public MainWindow()
    {
        InitializeComponent();

        if (PlayerView.MpvContext != null)
        {
            var engine = new CorePlaybackEngine(PlayerView.MpvContext);
            var playback = new MpvPlaybackService(engine);
            var storage = new StorageService();
            DataContext = new MainViewModel(playback, storage);
        }

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

        this.GetPropertyChangedObservable(WindowStateProperty).Subscribe(_ =>
        {
            if (ViewModel != null)
            {
                bool isFs = WindowState == WindowState.FullScreen;
                if (ViewModel.IsFullscreen != isFs)
                {
                    ViewModel.IsFullscreen = isFs;
                }
            }
        });

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsFullscreen))
                {
                    UpdateFullscreenState(ViewModel.IsFullscreen);
                }
                else if (args.PropertyName == nameof(MainViewModel.IsControlsVisible) || args.PropertyName == nameof(MainViewModel.IsPlaying))
                {
                    UpdateCursorVisibility();
                }
                else if (args.PropertyName == nameof(MainViewModel.IsMiniPlayer))
                {
                    if (ViewModel.IsMiniPlayer)
                    {
                        Width = 440;
                        Height = 250;
                        WindowState = WindowState.Normal;
                    }
                }
            };
        }
    }

    private void UpdateFullscreenState(bool isFullscreen)
    {
        if (isFullscreen)
        {
            if (WindowState != WindowState.FullScreen)
            {
                _previousWindowState = WindowState;
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

    private void UpdateCursorVisibility()
    {
        if (ViewModel != null && !ViewModel.IsControlsVisible && ViewModel.IsPlaying)
        {
            Cursor = new Cursor(StandardCursorType.None);
        }
        else
        {
            Cursor = Cursor.Default;
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

    private void OnVideoAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (Cursor != Cursor.Default)
        {
            Cursor = Cursor.Default;
        }
        ViewModel?.ShowControls();
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
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}