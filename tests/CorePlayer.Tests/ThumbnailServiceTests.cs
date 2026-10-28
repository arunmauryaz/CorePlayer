using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CorePlayer.Engine;
using CorePlayer.Models;
using CorePlayer.Services;
using CorePlayer.ViewModels;
using Xunit;

namespace CorePlayer.Tests;

public class ThumbnailServiceTests
{
    private class FakePlaybackService : IPlaybackService
    {
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(120);
        public string? CurrentFilePath { get; set; }
        public string? MediaTitle => "Test";
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
        public CorePlayer.Engine.ICorePlaybackEngine Engine => null!;
        public string? SnapshotDirectory { get; set; }
        public PlaybackStatistics GetCurrentStatistics() => new();

#pragma warning disable CS0067
        public event EventHandler? PlaybackStateChanged;
        public event EventHandler? PositionChanged;
        public event EventHandler? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler? TracksChanged;
        public event EventHandler<PlaybackStatistics>? StatisticsUpdated;
#pragma warning restore CS0067

        public IReadOnlyList<TrackInfo> AudioTracks => [];
        public IReadOnlyList<TrackInfo> SubtitleTracks => [];

        public void Load(string filePath, double startPosition = 0) { CurrentFilePath = filePath; }
        public void Play() { IsPlaying = true; }
        public void Pause() { IsPlaying = false; }
        public void Stop() { IsPlaying = false; }
        public void TogglePlayPause() { IsPlaying = !IsPlaying; }
        public void Seek(double seconds) { }
        public void FastSeek(double seconds) { }
        public void ExactSeek(double seconds) { }
        public void SeekRelative(double offsetSeconds) { }
        public void SetVolume(int volume) { Volume = volume; }
        public void SetSpeed(double speed) { Speed = speed; }
        public void SetBrightness(int brightness) { }
        public void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue) { }
        public void SetRotation(VideoRotation rotation) { }
        public void SetSubtitleDelay(double seconds) { }
        public void SetAudioDelay(double milliseconds) { }
        public void ToggleNightMode() { }
        public void SetAspectRatio(string ratio) { }
        public void SelectTrack(TrackInfo track) { }
        public void RefreshTracks() { }
        public void AddSubtitleFile(string path) { }
        public void StepFrame(bool forward = true) { }
        public string TakeSnapshot() => "";
        public string TakeSnapshot(bool includeSubtitles = true) => "";
        public void AttachContext(HanumanInstitute.LibMpv.MpvContext context) { }
    }

    private class FakeStorageService : IStorageService
    {
        public PlayerSettings LoadSettings() => new() { Volume = 100, Speed = 1.0 };
        public void SaveSettings(PlayerSettings settings) { }
        public double? GetPlaybackPosition(string filePath) => null;
        public void SavePlaybackPosition(string filePath, double positionSeconds, double durationSeconds) { }
        public List<PlaybackHistoryItem> GetRecentFiles(int limit = 10) => [];
    }

    private class FakeThumbnailService : IThumbnailService
    {
        public string? CurrentSource { get; private set; }
        public double CurrentDuration { get; private set; }
        public bool IsCleared { get; private set; }

        public void SetSource(string? filePath, double duration)
        {
            CurrentSource = filePath;
            CurrentDuration = duration;
            IsCleared = false;
        }

        public Bitmap? GetCachedThumbnail(double seconds)
        {
            return null;
        }

        public Task<Bitmap?> GetThumbnailAsync(double seconds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Bitmap?>(null);
        }

        public int CachedThumbnailCount => 0;

        public void Clear()
        {
            IsCleared = true;
            CurrentSource = null;
            CurrentDuration = 0;
        }

        public void Dispose()
        {
            Clear();
        }
    }

    [Fact]
    public void ThumbnailService_InitializesAndClearsCleanly()
    {
        using var service = new ThumbnailService();
        Assert.Null(service.GetCachedThumbnail(10));
        service.Clear();
    }

    [Fact]
    public void ThumbnailService_ReturnsNullWhenNoFileLoaded()
    {
        using var service = new ThumbnailService();
        service.SetSource("non_existent_file.mp4", 100);
        Assert.Null(service.GetCachedThumbnail(5));
    }

    [Fact]
    public void MainViewModel_UpdateHoverPreview_CalculatesTimeAndSetsVisibility()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        var thumbService = new FakeThumbnailService();
        using var vm = new MainViewModel(playback, storage, thumbService);

        // Before media is loaded, hover preview should not show
        vm.UpdateHoverPreview(15, 100, 500);
        Assert.False(vm.IsHoverPreviewVisible);

        // Open media
        vm.HasMedia = true;
        vm.DurationSeconds = 120;

        vm.UpdateHoverPreview(65, 250, 500);
        Assert.True(vm.IsHoverPreviewVisible);
        Assert.Equal("01:05", vm.HoverPreviewTime);
        Assert.Equal(250, vm.HoverPreviewX);

        vm.HideHoverPreview();
        Assert.False(vm.IsHoverPreviewVisible);
    }

    [Fact]
    public void MainViewModel_Stop_ClearsHoverPreviewAndThumbnails()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        var thumbService = new FakeThumbnailService();
        using var vm = new MainViewModel(playback, storage, thumbService);

        vm.HasMedia = true;
        vm.DurationSeconds = 60;
        thumbService.SetSource("video.mp4", 60);

        vm.UpdateHoverPreview(30, 100, 200);
        Assert.True(vm.IsHoverPreviewVisible);

        vm.Stop();
        Assert.False(vm.IsHoverPreviewVisible);
        Assert.True(thumbService.IsCleared);
    }

    [Fact]
    public void MainViewModel_ToggleHoverPreview_TogglesAndHidesWhenDisabled()
    {
        var playback = new FakePlaybackService();
        var storage = new FakeStorageService();
        var thumbService = new FakeThumbnailService();
        using var vm = new MainViewModel(playback, storage, thumbService);

        Assert.True(vm.EnableHoverPreview);
        Assert.Equal("Enabled", vm.HoverPreviewStatusText);

        vm.HasMedia = true;
        vm.DurationSeconds = 60;
        vm.UpdateHoverPreview(15, 50, 100);
        Assert.True(vm.IsHoverPreviewVisible);

        vm.ToggleHoverPreview();
        Assert.False(vm.EnableHoverPreview);
        Assert.Equal("Disabled", vm.HoverPreviewStatusText);
        Assert.False(vm.IsHoverPreviewVisible);

        // While disabled, UpdateHoverPreview does not show card
        vm.UpdateHoverPreview(20, 60, 100);
        Assert.False(vm.IsHoverPreviewVisible);

        vm.ToggleHoverPreview();
        Assert.True(vm.EnableHoverPreview);
        Assert.Equal("Enabled", vm.HoverPreviewStatusText);
    }

    [Fact]
    public void ThumbnailService_GeneratesThumbnails_WithRealVideo()
    {
        var testVideo = @"d:\CORE PLAYER\test_video.mp4";
        if (!File.Exists(testVideo)) return;

        using var service = new ThumbnailService();
        service.SetSource(testVideo, 5.0);

        // Wait up to 3 seconds for background worker to produce at least 1 thumbnail
        for (int i = 0; i < 30; i++)
        {
            Thread.Sleep(100);
            if (service.CachedThumbnailCount > 0) break;
        }

        Assert.True(service.CachedThumbnailCount > 0);
        service.Clear();
        Assert.Equal(0, service.CachedThumbnailCount);
    }
}
