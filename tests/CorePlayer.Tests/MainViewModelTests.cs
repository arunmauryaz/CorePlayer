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
        public void SelectTrack(TrackInfo track) { }
        public void AddSubtitleFile(string path) { }
        public void StepFrame(bool forward = true) { }
        public string TakeSnapshot() => "dummy.png";
        public string TakeSnapshot(bool includeSubtitles) => "dummy.png";
        public PlaybackStatistics GetCurrentStatistics() => new();
        public void AttachContext(HanumanInstitute.LibMpv.MpvContext context) { }
        public IReadOnlyList<TrackInfo> AudioTracks => [];
        public IReadOnlyList<TrackInfo> SubtitleTracks => [];
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
}
