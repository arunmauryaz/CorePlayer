using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CorePlayer.Engine;
using CorePlayer.Models;
using CorePlayer.Services;
using HanumanInstitute.LibMpv;

namespace CorePlayer.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IPlaybackService _playback;
    private readonly IStorageService _storage;
    private readonly DispatcherTimer _controlsTimer;
    private readonly DispatcherTimer _gestureTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Random _random = new();

    private double _pendingResumePosition;
    private bool _isDisposed;
    private int _unmutedVolume = 100;

    public MpvContext MpvContext => _playback.Context;

    [ObservableProperty]
    private string _mediaTitle = string.Empty;

    [ObservableProperty]
    private string _headerTitleText = string.Empty;

    [ObservableProperty]
    private string _aspectRatioName = "Fit";

    [ObservableProperty]
    private string _positionFormatted = "00:00";

    [ObservableProperty]
    private string _durationFormatted = "00:00";

    [ObservableProperty]
    private string _timeRemainingFormatted = "-00:00";

    [ObservableProperty]
    private double _positionSeconds;

    [ObservableProperty]
    private double _durationSeconds = 1.0;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private string _speedText = "1.0x";

    [ObservableProperty]
    private int _brightness;

    [ObservableProperty]
    private double _subtitleDelay;

    [ObservableProperty]
    private string _subtitleDelayText = "0.0s";

    [ObservableProperty]
    private bool _hasMedia;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isAlwaysOnTop;

    [ObservableProperty]
    private bool _isMiniPlayer;

    [ObservableProperty]
    private bool _isControlsVisible = true;

    [ObservableProperty]
    private bool _isQueueOpen;

    [ObservableProperty]
    private bool _isTracksMenuOpen;

    [ObservableProperty]
    private int _selectedTracksTab = 1; // 0 = Audio, 1 = Subtitles

    public bool IsAudioTabSelected => SelectedTracksTab == 0;
    public bool IsSubtitlesTabSelected => SelectedTracksTab == 1;

    partial void OnSelectedTracksTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsAudioTabSelected));
        OnPropertyChanged(nameof(IsSubtitlesTabSelected));
    }

    [ObservableProperty]
    private bool _isInfoModalOpen;

    [ObservableProperty]
    private PlaybackStatistics? _currentStats;

    [ObservableProperty]
    private bool _isNightMode;

    [ObservableProperty]
    private string _statsDisplay = string.Empty;

    [ObservableProperty]
    private bool _isMoreMenuOpen;

    public string SpeedDisplayUpper => $"{Speed:0.##}X";
    public double ShuffleOpacity => Shuffle ? 1.0 : 0.45;
    public double RepeatOpacity => RepeatMode > 0 ? 1.0 : 0.45;

    // Smart Auto-Resume Prompt
    [ObservableProperty]
    private bool _showResumePrompt;

    [ObservableProperty]
    private string _resumeTimeText = "00:00";

    // Gesture HUD
    [ObservableProperty]
    private bool _isGestureHudVisible;

    [ObservableProperty]
    private string _gestureHudTitle = string.Empty;

    [ObservableProperty]
    private string _gestureHudValue = string.Empty;

    [ObservableProperty]
    private double _gestureHudPercentage;

    // Drag & drop highlight
    [ObservableProperty]
    private bool _isDraggingOver;

    // Toast notifications
    [ObservableProperty]
    private bool _isToastVisible;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    // Playlist
    public ObservableCollection<MediaItem> Playlist { get; } = [];

    [ObservableProperty]
    private MediaItem? _currentItem;

    [ObservableProperty]
    private int _currentIndex = -1;

    [ObservableProperty]
    private int _repeatMode; // 0 = Off, 1 = Repeat All, 2 = Repeat One

    [ObservableProperty]
    private bool _shuffle;

    // Tracks
    public ObservableCollection<TrackInfo> AudioTracks { get; } = [];
    public ObservableCollection<TrackInfo> SubtitleTracks { get; } = [];

    // Media Details
    [ObservableProperty]
    private string _videoCodec = "Unknown";

    [ObservableProperty]
    private string _audioCodec = "Unknown";

    [ObservableProperty]
    private string _resolution = "Unknown";

    public MainViewModel() : this(new MpvPlaybackService(), new StorageService())
    {
    }

    public MainViewModel(IPlaybackService playback, IStorageService storage)
    {
        _playback = playback;
        _storage = storage;

        var settings = _storage.LoadSettings();
        _volume = settings.Volume;
        _playback.SetVolume(_volume);
        _speed = settings.Speed;
        _playback.SetSpeed(_speed);
        _speedText = $"{_speed:0.##}x";
        _shuffle = settings.Shuffle;
        _repeatMode = settings.RepeatMode;

        _playback.PlaybackStateChanged += OnPlaybackStateChanged;
        _playback.PositionChanged += OnPositionChanged;
        _playback.MediaOpened += OnMediaOpened;
        _playback.MediaEnded += OnMediaEnded;
        _playback.TracksChanged += OnTracksChanged;
        _playback.StatisticsUpdated += OnStatisticsUpdated;

        _controlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _controlsTimer.Tick += (_, _) =>
        {
            if (IsPlaying && !IsQueueOpen && !IsTracksMenuOpen && !IsInfoModalOpen)
            {
                IsControlsVisible = false;
            }
        };

        _gestureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _gestureTimer.Tick += (_, _) =>
        {
            IsGestureHudVisible = false;
            _gestureTimer.Stop();
        };

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.0) };
        _toastTimer.Tick += (_, _) =>
        {
            IsToastVisible = false;
            _toastTimer.Stop();
        };
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        IsPlaying = _playback.IsPlaying;
        if (IsPlaying)
        {
            _controlsTimer.Stop();
            _controlsTimer.Start();
        }
        else
        {
            IsControlsVisible = true;
        }
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        PositionSeconds = _playback.Position.TotalSeconds;
        PositionFormatted = FormatTime(_playback.Position);

        var remaining = _playback.Duration - _playback.Position;
        TimeRemainingFormatted = "-" + FormatTime(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);

        // Periodically save resume position (every ~5 seconds)
        if (HasMedia && !string.IsNullOrWhiteSpace(_playback.CurrentFilePath) && _playback.Position.TotalSeconds > 5)
        {
            _storage.SavePlaybackPosition(_playback.CurrentFilePath, _playback.Position.TotalSeconds, _playback.Duration.TotalSeconds);
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        DurationSeconds = _playback.Duration.TotalSeconds > 0 ? _playback.Duration.TotalSeconds : 1.0;
        DurationFormatted = FormatTime(_playback.Duration);
        HasMedia = true;
        MediaTitle = _playback.MediaTitle ?? (CurrentItem?.Title ?? "Media");
        UpdateHeaderTitle();
        VideoCodec = _playback.VideoCodec ?? "H.264/HEVC";
        AudioCodec = _playback.AudioCodec ?? "AAC/Opus";
        Resolution = _playback.Resolution ?? "1920x1080";
        CurrentStats = _playback.GetCurrentStatistics();

        // Check if smart auto-resume is available
        if (!string.IsNullOrWhiteSpace(_playback.CurrentFilePath))
        {
            var savedPos = _storage.GetPlaybackPosition(_playback.CurrentFilePath);
            if (savedPos.HasValue && savedPos.Value > 10 && savedPos.Value < _playback.Duration.TotalSeconds - 10)
            {
                _pendingResumePosition = savedPos.Value;
                ResumeTimeText = FormatTime(TimeSpan.FromSeconds(savedPos.Value));
                ShowResumePrompt = true;
            }
        }
    }

    private void OnStatisticsUpdated(object? sender, PlaybackStatistics stats)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentStats = stats;
            StatsDisplay = $"{stats.FormattedResolution} | {stats.FormattedFps} | {stats.FormattedVideoBitrate}";
        });
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (RepeatMode == 2) // Repeat One
        {
            _playback.Seek(0);
            _playback.Play();
        }
        else
        {
            PlayNextTrack();
        }
    }

    private void OnTracksChanged(object? sender, EventArgs e)
    {
        AudioTracks.Clear();
        foreach (var t in _playback.AudioTracks)
        {
            AudioTracks.Add(t);
        }

        SubtitleTracks.Clear();
        foreach (var t in _playback.SubtitleTracks)
        {
            SubtitleTracks.Add(t);
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.TotalHours >= 1 
            ? time.ToString(@"hh\:mm\:ss") 
            : time.ToString(@"mm\:ss");
    }

    private void UpdateHeaderTitle()
    {
        if (Playlist.Count > 1 && CurrentIndex >= 0 && CurrentIndex < Playlist.Count)
        {
            var item = Playlist[CurrentIndex];
            HeaderTitleText = $"[{CurrentIndex + 1}/{Playlist.Count}] {item.FileName}";
        }
        else if (CurrentItem != null)
        {
            HeaderTitleText = CurrentItem.FileName;
        }
        else if (!string.IsNullOrWhiteSpace(_playback.CurrentFilePath))
        {
            HeaderTitleText = Path.GetFileName(_playback.CurrentFilePath);
        }
        else
        {
            HeaderTitleText = MediaTitle;
        }
    }

    public void ShowControls()
    {
        IsControlsVisible = true;
        _controlsTimer.Stop();
        if (IsPlaying)
        {
            _controlsTimer.Start();
        }
    }

    public void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void TriggerGestureHud(string title, string value, double percentage)
    {
        GestureHudTitle = title;
        GestureHudValue = value;
        GestureHudPercentage = Math.Clamp(percentage, 0.0, 1.0);
        IsGestureHudVisible = true;

        _gestureTimer.Stop();
        _gestureTimer.Start();
    }

    // --- Commands ---

    [RelayCommand]
    public void OpenFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        var item = new MediaItem
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath),
            DateAdded = DateTime.Now
        };

        // If queue is empty or item is not in queue, add it
        var existing = Playlist.FirstOrDefault(x => string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            Playlist.Add(item);
            CurrentIndex = Playlist.Count - 1;
            CurrentItem = item;
        }
        else
        {
            CurrentIndex = Playlist.IndexOf(existing);
            CurrentItem = existing;
        }

        PlayMediaItem(CurrentItem);
        AutoQueueFolderEpisodes(filePath);
    }

    private void AutoQueueFolderEpisodes(string currentPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            string[] validExts = [".mp4", ".mkv", ".webm", ".avi", ".mov", ".flv", ".wmv", ".ts", ".m4v"];
            var files = Directory.GetFiles(dir)
                .Where(f => validExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var f in files)
            {
                if (!Playlist.Any(p => string.Equals(p.FilePath, f, StringComparison.OrdinalIgnoreCase)))
                {
                    Playlist.Add(new MediaItem
                    {
                        FilePath = f,
                        Title = Path.GetFileNameWithoutExtension(f),
                        DateAdded = DateTime.Now
                    });
                }
            }
            UpdateHeaderTitle();
        }
        catch
        {
            // Best effort auto-queue
        }
    }

    [RelayCommand]
    public void PlayMediaItem(MediaItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        CurrentItem = item;
        CurrentIndex = Playlist.IndexOf(item);
        ShowResumePrompt = false;

        _playback.Load(item.FilePath);
        MediaTitle = item.Title;
        UpdateHeaderTitle();
        HasMedia = true;
        ShowControls();
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (!HasMedia && Playlist.Count > 0)
        {
            PlayMediaItem(Playlist[0]);
            return;
        }

        _playback.TogglePlayPause();
        ShowControls();
    }

    [RelayCommand]
    public void Stop()
    {
        _playback.Stop();
        PositionSeconds = 0;
        PositionFormatted = "00:00";
        ShowControls();
        ShowToast("Stopped");
    }

    [RelayCommand]
    public void SeekTo(double seconds)
    {
        _playback.Seek(seconds);
        ShowControls();
    }

    [RelayCommand]
    public void SeekRelative(double offsetSeconds)
    {
        _playback.SeekRelative(offsetSeconds);
        ShowControls();

        var sign = offsetSeconds > 0 ? "+" : "";
        TriggerGestureHud("Seek", $"{sign}{offsetSeconds:0}s ({PositionFormatted})", DurationSeconds > 0 ? PositionSeconds / DurationSeconds : 0);
    }

    [RelayCommand]
    public void SeekForward10() => SeekRelative(10);

    [RelayCommand]
    public void SeekBackward10() => SeekRelative(-10);

    partial void OnVolumeChanged(int value)
    {
        if (value > 0)
        {
            _unmutedVolume = value;
            if (IsMuted)
            {
                IsMuted = false;
            }
        }
        else if (value == 0)
        {
            IsMuted = true;
        }
        _playback.SetVolume(value);
    }

    [RelayCommand]
    public void AdjustVolume(int delta)
    {
        var target = IsMuted ? delta : Volume + delta;
        Volume = Math.Clamp(target, 0, 150);
        TriggerGestureHud("Volume", $"{Volume}%", Volume / 150.0);
    }

    [RelayCommand]
    public void VolumeUp() => AdjustVolume(5);

    [RelayCommand]
    public void VolumeDown() => AdjustVolume(-5);

    [RelayCommand]
    public void ToggleMute()
    {
        if (!IsMuted)
        {
            _unmutedVolume = Volume > 0 ? Volume : 100;
            IsMuted = true;
            Volume = 0;
            TriggerGestureHud("Audio", "Muted", 0);
        }
        else
        {
            IsMuted = false;
            Volume = _unmutedVolume > 0 ? _unmutedVolume : 100;
            TriggerGestureHud("Audio", $"{Volume}%", Volume / 150.0);
        }
    }

    [RelayCommand]
    public void AdjustBrightness(int delta)
    {
        var newBright = Math.Clamp(Brightness + delta, -100, 100);
        Brightness = newBright;
        _playback.SetBrightness(Brightness);

        var sign = Brightness > 0 ? "+" : "";
        TriggerGestureHud("Brightness", $"{sign}{Brightness}%", (Brightness + 100) / 200.0);
    }

    [RelayCommand]
    public void CycleSpeed()
    {
        double[] speeds = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];
        int idx = Array.IndexOf(speeds, Speed);
        double nextSpeed = (idx >= 0 && idx < speeds.Length - 1) ? speeds[idx + 1] : speeds[0];

        Speed = nextSpeed;
        SpeedText = $"{Speed:0.##}x";
        OnPropertyChanged(nameof(SpeedDisplayUpper));
        _playback.SetSpeed(Speed);

        ShowToast($"Playback Speed: {SpeedText}");
        TriggerGestureHud("Speed", SpeedText, Speed / 3.0);
    }

    [RelayCommand]
    public void SetSpeed(string speedParam)
    {
        if (double.TryParse(speedParam, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var s))
        {
            Speed = s;
            SpeedText = $"{Speed:0.##}x";
            OnPropertyChanged(nameof(SpeedDisplayUpper));
            _playback.SetSpeed(Speed);
            ShowToast($"Speed: {SpeedText}");
            TriggerGestureHud("Speed", SpeedText, Speed / 3.0);
        }
    }

    [RelayCommand]
    public void OpenPreferences()
    {
        ShowToast("Preferences Dialog (Coming Soon)");
    }

    [RelayCommand]
    public void OpenAdvancedSettings()
    {
        ShowToast("Advanced Settings: Control bar options coming soon");
    }

    [RelayCommand]
    public void ResumeFromPrompt()
    {
        ShowResumePrompt = false;
        if (_pendingResumePosition > 0)
        {
            _playback.Seek(_pendingResumePosition);
            ShowToast($"Resumed at {ResumeTimeText}");
        }
    }

    [RelayCommand]
    public void DismissResumePrompt()
    {
        ShowResumePrompt = false;
    }

    [RelayCommand]
    public void StepFrame(bool forward)
    {
        _playback.StepFrame(forward);
        ShowToast(forward ? "Step Forward" : "Step Backward");
    }

    [RelayCommand]
    public void TakeSnapshot()
    {
        try
        {
            var savedPath = _playback.TakeSnapshot();
            ShowToast($"Snapshot captured! Saved to Pictures");
        }
        catch (Exception ex)
        {
            ShowToast($"Screenshot failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ToggleQueue()
    {
        IsQueueOpen = !IsQueueOpen;
        if (IsQueueOpen)
        {
            IsTracksMenuOpen = false;
            IsInfoModalOpen = false;
        }
    }

    [RelayCommand]
    public void ToggleTracksMenu()
    {
        IsTracksMenuOpen = !IsTracksMenuOpen;
        if (IsTracksMenuOpen)
        {
            SelectedTracksTab = 1; // Default to Subtitles tab
            IsQueueOpen = false;
            IsInfoModalOpen = false;
            IsMoreMenuOpen = false;
        }
    }

    [RelayCommand]
    public void SelectAudioTab()
    {
        SelectedTracksTab = 0;
    }

    [RelayCommand]
    public void SelectSubtitlesTab()
    {
        SelectedTracksTab = 1;
    }

    [RelayCommand]
    public void SetTracksTab(int tab)
    {
        SelectedTracksTab = tab;
    }

    [RelayCommand]
    public void ToggleInfoModal()
    {
        IsInfoModalOpen = !IsInfoModalOpen;
        if (IsInfoModalOpen)
        {
            IsQueueOpen = false;
            IsTracksMenuOpen = false;
            CurrentStats = _playback.GetCurrentStatistics();
        }
    }

    [RelayCommand]
    public void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    public void ToggleAlwaysOnTop()
    {
        IsAlwaysOnTop = !IsAlwaysOnTop;
        ShowToast(IsAlwaysOnTop ? "Pinned on Top" : "Unpinned");
    }

    [RelayCommand]
    public void ToggleMiniPlayer()
    {
        IsMiniPlayer = !IsMiniPlayer;
        if (IsMiniPlayer)
        {
            IsAlwaysOnTop = true;
            IsQueueOpen = false;
        }
    }

    [RelayCommand]
    public void PlayNextTrack()
    {
        if (Playlist.Count == 0) return;

        if (Shuffle)
        {
            int nextIdx = _random.Next(Playlist.Count);
            PlayMediaItem(Playlist[nextIdx]);
            return;
        }

        if (CurrentIndex + 1 < Playlist.Count)
        {
            PlayMediaItem(Playlist[CurrentIndex + 1]);
        }
        else if (RepeatMode == 1) // Repeat All
        {
            PlayMediaItem(Playlist[0]);
        }
    }

    [RelayCommand]
    public void PlayPreviousTrack()
    {
        if (Playlist.Count == 0) return;

        if (PositionSeconds > 3)
        {
            _playback.Seek(0);
            return;
        }

        if (CurrentIndex > 0)
        {
            PlayMediaItem(Playlist[CurrentIndex - 1]);
        }
        else
        {
            PlayMediaItem(Playlist[^1]);
        }
    }

    [RelayCommand]
    public void ToggleRepeat()
    {
        RepeatMode = (RepeatMode + 1) % 3;
        OnPropertyChanged(nameof(RepeatOpacity));
        var modeName = RepeatMode switch
        {
            1 => "Repeat All",
            2 => "Repeat One",
            _ => "Repeat Off"
        };
        ShowToast(modeName);
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        OnPropertyChanged(nameof(ShuffleOpacity));
        ShowToast(Shuffle ? "Shuffle On" : "Shuffle Off");
    }

    [RelayCommand]
    public void CycleAspectRatio()
    {
        (string val, string label) = _playback.AspectRatio switch
        {
            "-1" => ("16:9", "16:9"),
            "16:9" => ("4:3", "4:3"),
            "4:3" => ("2.35:1", "2.35:1"),
            _ => ("-1", "Fit")
        };

        _playback.SetAspectRatio(val);
        AspectRatioName = label;
        TriggerGestureHud("Aspect", label, 0.5);
        ShowToast($"Aspect Ratio: {label}");
    }

    [RelayCommand]
    public void SetAspectRatio(string ratio)
    {
        _playback.SetAspectRatio(ratio);
        AspectRatioName = ratio == "-1" ? "Fit" : ratio;
        TriggerGestureHud("Aspect", AspectRatioName, 0.5);
        ShowToast($"Aspect Ratio: {AspectRatioName}");
    }

    [RelayCommand]
    public void ToggleMoreMenu()
    {
        IsMoreMenuOpen = !IsMoreMenuOpen;
        if (IsMoreMenuOpen)
        {
            IsQueueOpen = false;
            IsTracksMenuOpen = false;
            IsInfoModalOpen = false;
        }
    }

    [RelayCommand]
    public void ClearQueue()
    {
        Playlist.Clear();
        CurrentIndex = -1;
        CurrentItem = null;
        UpdateHeaderTitle();
    }

    [RelayCommand]
    public void RemoveQueueItem(MediaItem item)
    {
        var idx = Playlist.IndexOf(item);
        if (idx >= 0)
        {
            Playlist.RemoveAt(idx);
            if (CurrentItem == item)
            {
                if (Playlist.Count > 0)
                {
                    PlayMediaItem(Playlist[Math.Min(idx, Playlist.Count - 1)]);
                }
                else
                {
                    _playback.Pause();
                    HasMedia = false;
                    DurationSeconds = 1.0;
                    PositionSeconds = 0;
                }
            }
            UpdateHeaderTitle();
        }
    }

    [RelayCommand]
    public void SelectTrack(TrackInfo track)
    {
        _playback.SelectTrack(track);
        ShowToast($"Selected {track.DisplayText}");
    }

    [RelayCommand]
    public void AdjustSubtitleDelay(double delta)
    {
        SubtitleDelay = Math.Round(SubtitleDelay + delta, 1);
        SubtitleDelayText = $"{(SubtitleDelay >= 0 ? "+" : "")}{SubtitleDelay:0.0}s";
        _playback.SetSubtitleDelay(SubtitleDelay);
        ShowToast($"Subtitles: {SubtitleDelayText}");
    }

    [RelayCommand]
    public void SubtitleDelayPlus() => AdjustSubtitleDelay(0.1);

    [RelayCommand]
    public void SubtitleDelayMinus() => AdjustSubtitleDelay(-0.1);

    [RelayCommand]
    public void AddSubtitleFile(string path)
    {
        if (File.Exists(path))
        {
            _playback.AddSubtitleFile(path);
            ShowToast($"Subtitle loaded: {Path.GetFileName(path)}");
        }
    }

    [RelayCommand]
    public void ToggleNightMode()
    {
        _playback.ToggleNightMode();
        IsNightMode = _playback.IsNightModeActive;
        ShowToast(IsNightMode ? "Night Mode (Dialogue Boost): ON" : "Night Mode (Dialogue Boost): OFF");
    }

    [RelayCommand]
    public void RotateVideo(object? parameter)
    {
        int degrees = 0;
        if (parameter is int d) degrees = d;
        else if (parameter is string s && int.TryParse(s, out var parsed)) degrees = parsed;

        var rotation = degrees switch
        {
            90 => VideoRotation.Rotate90,
            180 => VideoRotation.Rotate180,
            270 => VideoRotation.Rotate270,
            _ => VideoRotation.Normal
        };

        _playback.SetRotation(rotation);
        ShowToast($"Video Rotation: {degrees}°");
    }

    [RelayCommand]
    public void CycleAudioTrack()
    {
        if (AudioTracks.Count <= 1) return;
        int currentIdx = AudioTracks.ToList().FindIndex(t => t.IsSelected);
        int nextIdx = (currentIdx + 1) % AudioTracks.Count;
        var nextTrack = AudioTracks[nextIdx];
        SelectTrack(nextTrack);
    }

    [RelayCommand]
    public void CycleSubtitleTrack()
    {
        if (SubtitleTracks.Count == 0) return;
        int currentIdx = SubtitleTracks.ToList().FindIndex(t => t.IsSelected);
        int nextIdx = (currentIdx + 1) % SubtitleTracks.Count;
        var nextTrack = SubtitleTracks[nextIdx];
        SelectTrack(nextTrack);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _controlsTimer.Stop();
        _gestureTimer.Stop();
        _toastTimer.Stop();

        var settings = new PlayerSettings
        {
            Volume = Volume,
            Speed = Speed,
            Shuffle = Shuffle,
            RepeatMode = RepeatMode
        };
        _storage.SaveSettings(settings);

        if (_playback is IDisposable d)
        {
            d.Dispose();
        }
    }
}
