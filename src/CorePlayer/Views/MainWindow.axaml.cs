using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CorePlayer.ViewModels;

namespace CorePlayer.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private WindowState _previousWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

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
            }
            ExtendClientAreaToDecorationsHint = false;
            SystemDecorations = SystemDecorations.None;
            WindowState = WindowState.FullScreen;
            if (TopBarBorder != null)
            {
                TopBarBorder.Margin = new Thickness(0);
            }
        }
        else
        {
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
            WindowState = _previousWindowState == WindowState.FullScreen ? WindowState.Normal : _previousWindowState;
            if (TopBarBorder != null)
            {
                TopBarBorder.Margin = new Thickness(0, 0, 140, 0);
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

    private void OnVideoAreaPointerMoved(object? sender, PointerEventArgs e)
    {
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
            }
        }
    }

    private void OnVideoAreaPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel == null) return;

        var pos = e.GetPosition(this);
        var delta = e.Delta.Y > 0 ? 5 : -5;

        // Right 50% = Volume, Left 50% = Brightness
        if (pos.X > Bounds.Width * 0.5)
        {
            ViewModel.AdjustVolume(delta);
        }
        else
        {
            ViewModel.AdjustBrightness(delta);
        }
    }

    private void OnVideoAreaDoubleTapped(object? sender, TappedEventArgs e)
    {
        ViewModel?.ToggleFullscreen();
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