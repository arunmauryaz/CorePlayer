using System;
using System.Collections.Generic;
using CorePlayer.Engine;
using CorePlayer.Models;
using CorePlayer.Services;
using CorePlayer.ViewModels;
using Xunit;

namespace CorePlayer.Tests;

public class MainViewModelTests
{
    private class FakePlaybackService : IPlaybackService
    {
        public bool IsPlaying { get; private set; }
        public TimeSpan Position { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(120);
        public string? CurrentFilePath { get; set; }
        public string? MediaTitle { get; set; }
        public string? VideoCodec => "h264";
        public string? AudioCodec => "aac";
        public string? Resolution => "1920x1080";
        public int Volume { get; set; } = 100;
        public double Speed { get; set; } = 1.0;
        public int Brightness { get; set; } = 0;
        public double SubtitleDelay { get; set; } = 0;
        public string AspectRatio { get; set; } = "-1";
        public bool IsNightModeActive { get; set; }
        public VideoRotation Rotation { get; set; } = VideoRotation.Normal;
        public HanumanInstitute.LibMpv.MpvContext Context => null!;
        public ICorePlaybackEngine Engine => null!;
        public string? SnapshotDirectory { get; set; }

        public double LastRelativeSeekOffset { get; private set; }
        public int SeekRelativeCallCount { get; private set; }

#pragma warning disable CS0067
        public event EventHandler? PlaybackStateChanged;
        public event EventHandler? PositionChanged;
        public event EventHandler? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler? TracksChanged;
        public event EventHandler<PlaybackStatistics>? StatisticsUpdated;
#pragma warning restore CS0067

        public void Load(string filePath, double startPosition = 0)
        {
            CurrentFilePath = filePath;
            if (startPosition > 0)
            {
                Position = TimeSpan.FromSeconds(startPosition);
            }
            MediaOpened?.Invoke(this, EventArgs.Empty);
        }
        public void Play() { IsPlaying = true; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Pause() { IsPlaying = false; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void TogglePlayPause() { IsPlaying = !IsPlaying; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Stop() { IsPlaying = false; Position = TimeSpan.Zero; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Seek(double seconds) { Position = TimeSpan.FromSeconds(seconds); PositionChanged?.Invoke(this, EventArgs.Empty); }
        public void FastSeek(double seconds) => Seek(seconds);
        public void ExactSeek(double seconds) => Seek(seconds);
        public void SeekRelative(double offsetSeconds)
        {
            LastRelativeSeekOffset = offsetSeconds;
            SeekRelativeCallCount++;
            Position = TimeSpan.FromSeconds(Math.Max(0, Position.TotalSeconds + offsetSeconds));
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
        public void SetVolume(int volume) => Volume = volume;
        public void SetSpeed(double speed) => Speed = speed;
        public void SetBrightness(int brightness) => Brightness = brightness;
        public void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue) { }
        public void SetRotation(VideoRotation rotation) => Rotation = rotation;
        public void SetSubtitleDelay(double seconds) => SubtitleDelay = seconds;
        public void SetAudioDelay(double milliseconds) { }
        public void ToggleNightMode() => IsNightModeActive = !IsNightModeActive;
        public void SetAspectRatio(string ratio) => AspectRatio = ratio;
        public List<TrackInfo> FakeAudioTracks { get; } = [];
        public List<TrackInfo> FakeSubtitleTracks { get; } = [];
        public IReadOnlyList<TrackInfo> AudioTracks => FakeAudioTracks;
        public IReadOnlyList<TrackInfo> SubtitleTracks => FakeSubtitleTracks;
        public TrackInfo? LastSelectedTrack { get; private set; }
        public int RefreshTracksCallCount { get; private set; }

        public void SelectTrack(TrackInfo track)
        {
            LastSelectedTrack = track;
            foreach (var t in FakeAudioTracks) t.IsSelected = (t.Id == track.Id && t.Kind == track.Kind);
            foreach (var t in FakeSubtitleTracks) t.IsSelected = (t.Id == track.Id && t.Kind == track.Kind);
            TracksChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshTracks()
        {
            RefreshTracksCallCount++;
            TracksChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddSubtitleFile(string path) { }
        public void StepFrame(bool forward = true) { }
        public string TakeSnapshot() => "dummy.png";
        public string TakeSnapshot(bool includeSubtitles) => "dummy.png";
        public PlaybackStatistics GetCurrentStatistics() => new();
        public void AttachContext(HanumanInstitute.LibMpv.MpvContext context) { }
    }

    private class FakeStorageService : IStorageService
    {
        private readonly Dictionary<string, double> _positions = new();
        public PlayerSettings LoadSettings() => new() { Volume = 100, Speed = 1.0 };
        public void SaveSettings(PlayerSettings settings) { }
        public double? GetPlaybackPosition(string filePath) => _positions.TryGetValue(filePath, out var pos) ? pos : null;
        public void SavePlaybackPosition(string filePath, double positionSeconds, double durationSeconds) => _positions[filePath] = positionSeconds;
        public List<PlaybackHistoryItem> GetRecentFiles(int limit = 10) => [];
    }

    [Fact]
    public void SeekForward5_ShowsYouTubeRightSkipHud_With5Seconds()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.SeekForward5();

        Assert.True(vm.IsSkipForwardHudVisible);
        Assert.False(vm.IsSkipBackwardHudVisible);
        Assert.Equal("+5 seconds", vm.SkipForwardText);
        Assert.Equal(5, playback.LastRelativeSeekOffset);
        Assert.False(vm.IsGestureHudVisible); // No center rectangle
    }

    [Fact]
    public void ConsecutiveSeekForward5_AccumulatesSeconds()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.SeekForward5();
        Assert.Equal("+5 seconds", vm.SkipForwardText);

        vm.SeekForward5();
        Assert.Equal("+10 seconds", vm.SkipForwardText);

        vm.SeekForward5();
        Assert.Equal("+15 seconds", vm.SkipForwardText);

        Assert.True(vm.IsSkipForwardHudVisible);
        Assert.False(vm.IsSkipBackwardHudVisible);
    }

    [Fact]
    public void SeekBackward5_ShowsYouTubeLeftSkipHud_WithMinus5Seconds()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.SeekBackward5();

        Assert.True(vm.IsSkipBackwardHudVisible);
        Assert.False(vm.IsSkipForwardHudVisible);
        Assert.Equal("-5 seconds", vm.SkipBackwardText);
        Assert.Equal(-5, playback.LastRelativeSeekOffset);
        Assert.False(vm.IsGestureHudVisible); // No center rectangle
    }

    [Fact]
    public void ConsecutiveSeekBackward5_AccumulatesSeconds()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.SeekBackward5();
        Assert.Equal("-5 seconds", vm.SkipBackwardText);

        vm.SeekBackward5();
        Assert.Equal("-10 seconds", vm.SkipBackwardText);

        Assert.True(vm.IsSkipBackwardHudVisible);
        Assert.False(vm.IsSkipForwardHudVisible);
    }

    [Fact]
    public void DirectionSwitch_ResetsAccumulator()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.SeekForward5();
        vm.SeekForward5();
        Assert.Equal("+10 seconds", vm.SkipForwardText);

        // Switch to backward
        vm.SeekBackward5();
        Assert.True(vm.IsSkipBackwardHudVisible);
        Assert.False(vm.IsSkipForwardHudVisible);
        Assert.Equal("-5 seconds", vm.SkipBackwardText);
    }

    [Fact]
    public void AdjustVolume_DoesNotShowCenterGestureHud()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.AdjustVolume(5);

        Assert.Equal(105, vm.Volume);
        Assert.False(vm.IsGestureHudVisible); // No center rectangle irritating user
    }

    [Fact]
    public void AdjustVolume_WhenMuted_RestoresFromUnmutedVolume()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.Volume = 80;
        vm.ToggleMute();
        Assert.True(vm.IsMuted);
        Assert.Equal(0, vm.Volume);

        // Scrolling up while muted restores from 80 + 5 = 85 and unmutes
        vm.AdjustVolume(5);
        Assert.False(vm.IsMuted);
        Assert.Equal(85, vm.Volume);
    }

    [Fact]
    public void ToggleFullscreen_TogglesIsFullscreenProperty()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        Assert.False(vm.IsFullscreen);
        vm.ToggleFullscreen();
        Assert.True(vm.IsFullscreen);
        vm.ToggleFullscreen();
        Assert.False(vm.IsFullscreen);
    }

    [Fact]
    public void ShowControls_EnsuresControlsAreVisible()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        vm.IsControlsVisible = false;
        Assert.False(vm.IsControlsVisible);

        vm.ShowControls();
        Assert.True(vm.IsControlsVisible);
    }

    [Fact]
    public void WhenPlaybackPauses_ControlsBecomeVisible()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        playback.Play();
        vm.IsControlsVisible = false;

        playback.Pause();
        Assert.True(vm.IsControlsVisible);
    }

    [Fact]
    public void WhenMediaOpenedWithSavedPosition_AutomaticallyResumesWithoutPrompt()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        playback.CurrentFilePath = "D:\\Videos\\anime.mp4";
        playback.Duration = TimeSpan.FromMinutes(20);
        storage.SavePlaybackPosition("D:\\Videos\\anime.mp4", 350.0, 1200.0);

        using var vm = new MainViewModel(playback, storage);

        playback.Load("D:\\Videos\\anime.mp4");

        Assert.False(vm.ShowResumePrompt);
        Assert.Equal(350.0, playback.Position.TotalSeconds);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("Resumed playback", vm.ToastMessage);
    }

    [Fact]
    public void TrackInfo_DisplayText_FormatsLanguageTitleAndCodec()
    {
        var track = new TrackInfo
        {
            Id = 1,
            Language = "Japanese",
            Title = "Original Japanese",
            Codec = "aac"
        };
        Assert.Equal("Japanese • Original Japanese • [AAC]", track.DisplayText);
    }

    [Fact]
    public void TrackInfo_DisplayText_DeduplicatesIdenticalTitleAndLanguage()
    {
        var track = new TrackInfo
        {
            Id = 2,
            Language = "Hindi",
            Title = "Hindi",
            Codec = "eac3"
        };
        Assert.Equal("Hindi • [EAC3]", track.DisplayText);
    }

    [Fact]
    public void TrackInfo_DisplayText_FallsBackToTrackId_WhenEmpty()
    {
        var track = new TrackInfo { Id = 3 };
        Assert.Equal("Track #3", track.DisplayText);
    }

    [Fact]
    public void ToggleTracksMenu_RefreshesTracksAndPopulatesLists()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        playback.FakeAudioTracks.Add(new TrackInfo { Id = 1, Kind = TrackKind.Audio, Language = "Japanese", IsSelected = true });
        playback.FakeAudioTracks.Add(new TrackInfo { Id = 2, Kind = TrackKind.Audio, Language = "Hindi", IsSelected = false });
        playback.FakeSubtitleTracks.Add(new TrackInfo { Id = 1, Kind = TrackKind.Subtitle, Language = "English", IsSelected = true });

        using var vm = new MainViewModel(playback, storage);
        vm.ToggleTracksMenu();

        Assert.True(vm.IsTracksMenuOpen);
        Assert.Equal(1, playback.RefreshTracksCallCount);
        Assert.Equal(2, vm.AudioTracks.Count);
        Assert.Single(vm.SubtitleTracks);
        Assert.Equal("Japanese", vm.AudioTracks[0].Language);
        Assert.Equal("Hindi", vm.AudioTracks[1].Language);
    }

    [Fact]
    public void SelectTrack_UpdatesTrackSelectionAndShowsToast()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        var t1 = new TrackInfo { Id = 1, Kind = TrackKind.Audio, Language = "Japanese", IsSelected = true };
        var t2 = new TrackInfo { Id = 2, Kind = TrackKind.Audio, Language = "Hindi", IsSelected = false };
        playback.FakeAudioTracks.Add(t1);
        playback.FakeAudioTracks.Add(t2);

        using var vm = new MainViewModel(playback, storage);
        vm.ToggleTracksMenu();

        vm.SelectTrack(t2);

        Assert.Equal(t2, playback.LastSelectedTrack);
        Assert.True(t2.IsSelected);
        Assert.False(t1.IsSelected);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("Hindi", vm.ToastMessage);
    }

    [Fact]
    public void DisableSubtitles_SelectsOffSubtitleTrack()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        playback.FakeSubtitleTracks.Add(new TrackInfo { Id = 1, Kind = TrackKind.Subtitle, Language = "English", IsSelected = true });

        using var vm = new MainViewModel(playback, storage);
        vm.ToggleTracksMenu();

        vm.DisableSubtitles();

        Assert.NotNull(playback.LastSelectedTrack);
        Assert.Equal(0, playback.LastSelectedTrack.Id);
        Assert.Equal(TrackKind.Subtitle, playback.LastSelectedTrack.Kind);
        Assert.True(vm.IsToastVisible);
        Assert.Contains("Subtitles: Off", vm.ToastMessage);
    }

    [Fact]
    public void ToggleFullscreen_TogglesViewModelIsFullscreen()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();

        using var vm = new MainViewModel(playback, storage);
        Assert.False(vm.IsFullscreen);

        var propertyChangedFired = false;
        string? changedPropertyName = null;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullscreen))
            {
                propertyChangedFired = true;
                changedPropertyName = e.PropertyName;
            }
        };

        vm.ToggleFullscreen();
        Assert.True(vm.IsFullscreen);
        Assert.True(propertyChangedFired);
        Assert.Equal(nameof(MainViewModel.IsFullscreen), changedPropertyName);

        propertyChangedFired = false;
        vm.ToggleFullscreen();
        Assert.False(vm.IsFullscreen);
        Assert.True(propertyChangedFired);
    }

    [Fact]
    public void OptionsDrawer_Navigation_And_SubPages_WorkAsExpected()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();

        using var vm = new MainViewModel(playback, storage);
        Assert.False(vm.IsMoreMenuOpen);
        Assert.True(vm.IsOptionsRootPage);
        Assert.False(vm.IsSettingsSubPage);
        Assert.False(vm.IsOptionsSubPage);

        // Open drawer
        vm.ToggleMoreMenu();
        Assert.True(vm.IsMoreMenuOpen);
        Assert.True(vm.IsOptionsRootPage);

        // Navigate to Settings (page 1)
        vm.NavigateToOptionsPage(1);
        Assert.False(vm.IsOptionsRootPage);
        Assert.True(vm.IsSettingsSubPage);
        Assert.True(vm.IsOptionsSubPage);
        Assert.Equal("Settings", vm.OptionsPanelTitle);

        // Navigate back to root
        vm.NavigateBack();
        Assert.True(vm.IsOptionsRootPage);
        Assert.False(vm.IsSettingsSubPage);
        Assert.False(vm.IsOptionsSubPage);
        Assert.Equal("Options", vm.OptionsPanelTitle);

        // Navigate to Aspect Ratio (page 2)
        vm.NavigateToOptionsPage(2);
        Assert.True(vm.IsAspectRatioSubPage);
        Assert.Equal("Aspect Ratio", vm.OptionsPanelTitle);

        // Set Aspect Ratio to 16:9
        vm.SetAspectRatio("16:9");
        Assert.Equal("16:9", playback.AspectRatio);
        Assert.True(vm.IsAspect169);
        Assert.False(vm.IsAspect43);

        // Navigate to Speed (page 3)
        vm.NavigateToOptionsPage(3);
        Assert.True(vm.IsSpeedSubPage);
        Assert.Equal("Playback Speed", vm.OptionsPanelTitle);

        // Set speed
        vm.SetSpeed("1.5");
        Assert.Equal(1.5, playback.Speed);
        Assert.True(vm.IsSpeed15);
        Assert.False(vm.IsSpeed10);

        // Close menu
        vm.CloseMoreMenu();
        Assert.False(vm.IsMoreMenuOpen);
        Assert.True(vm.IsOptionsRootPage);
    }

    [Fact]
    public void CycleSeekStep_UpdatesSeekStepIntervalAndAppliesToSeekForward()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        Assert.Equal(5, vm.SeekStepSeconds);
        Assert.Equal("5s", vm.SeekStepDisplay);

        vm.CycleSeekStep();
        Assert.Equal(10, vm.SeekStepSeconds);
        Assert.Equal("10s", vm.SeekStepDisplay);

        vm.SeekForward5();
        Assert.Equal(10, playback.LastRelativeSeekOffset);
        Assert.Equal("+10 seconds", vm.SkipForwardText);
    }

    [Fact]
    public void ToggleAutoResume_TogglesAndPersists()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        Assert.True(vm.AutoResume);
        Assert.Equal("Enabled", vm.AutoResumeStatusText);

        vm.ToggleAutoResume();
        Assert.False(vm.AutoResume);
        Assert.Equal("Disabled", vm.AutoResumeStatusText);

        vm.ToggleAutoResume();
        Assert.True(vm.AutoResume);
        Assert.Equal("Enabled", vm.AutoResumeStatusText);
    }

    [Fact]
    public void SetSnapshotDirectory_UpdatesDirectoryAndPlaybackEngine()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        using var vm = new MainViewModel(playback, storage);

        var customPath = @"C:\CustomSnaps";
        vm.SetSnapshotDirectory(customPath);

        Assert.Equal(customPath, vm.SnapshotDirectory);
        Assert.Equal(customPath, playback.SnapshotDirectory);
        Assert.Equal(customPath, vm.SnapshotDirectoryDisplay);
    }
}
