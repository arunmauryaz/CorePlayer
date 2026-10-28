using System;
using System.Collections.Generic;
using CorePlayer.Engine;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Services;

public interface IPlaybackService
{
    MpvContext Context { get; }
    ICorePlaybackEngine Engine { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    int Volume { get; }
    double Speed { get; }
    int Brightness { get; }
    double SubtitleDelay { get; }
    string? CurrentFilePath { get; }
    string? MediaTitle { get; }
    string? VideoCodec { get; }
    string? AudioCodec { get; }
    string? Resolution { get; }
    string AspectRatio { get; }
    bool IsNightModeActive { get; }
    VideoRotation Rotation { get; }
    IReadOnlyList<TrackInfo> AudioTracks { get; }
    IReadOnlyList<TrackInfo> SubtitleTracks { get; }
    string? SnapshotDirectory { get; set; }

    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    event EventHandler? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler? TracksChanged;
    event EventHandler<PlaybackStatistics>? StatisticsUpdated;

    void Load(string filePath, double startPosition = 0);
    void Play();
    void Pause();
    void Stop();
    void TogglePlayPause();
    void Seek(double seconds);
    void FastSeek(double seconds);
    void ExactSeek(double seconds);
    void SeekRelative(double offsetSeconds);
    void SetVolume(int volume);
    void SetSpeed(double speed);
    void SetBrightness(int brightness);
    void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue);
    void SetRotation(VideoRotation rotation);
    void SetSubtitleDelay(double seconds);
    void SetAudioDelay(double milliseconds);
    void ToggleNightMode();
    void SetAspectRatio(string ratio);
    void SelectTrack(TrackInfo track);
    void RefreshTracks();
    void AddSubtitleFile(string path);
    void StepFrame(bool forward = true);
    string TakeSnapshot();
    string TakeSnapshot(bool includeSubtitles);
    PlaybackStatistics GetCurrentStatistics();
    void AttachContext(MpvContext context);
}

public class MpvPlaybackService : IPlaybackService, ICorePlaybackEngine
{
    private readonly CorePlaybackEngine _engine;

    public MpvPlaybackService(EngineOptions? options = null) : this(new CorePlaybackEngine(options))
    {
    }

    public MpvPlaybackService(MpvContext context, EngineOptions? options = null) : this(new CorePlaybackEngine(context, options))
    {
    }

    public MpvPlaybackService(CorePlaybackEngine engine)
    {
        _engine = engine;
        _engine.PlaybackStateChanged += (s, e) => PlaybackStateChanged?.Invoke(this, e);
        _engine.PositionChanged += (s, e) => PositionChanged?.Invoke(this, e);
        _engine.MediaOpened += (s, e) => MediaOpened?.Invoke(this, e);
        _engine.MediaEnded += (s, e) => MediaEnded?.Invoke(this, e);
        _engine.TracksChanged += (s, e) => TracksChanged?.Invoke(this, e);
        _engine.StatisticsUpdated += (s, e) => StatisticsUpdated?.Invoke(this, e);
    }

    public ICorePlaybackEngine Engine => _engine;
    public MpvContext Context => _engine.Context;
    public bool IsPlaying => _engine.IsPlaying;
    public TimeSpan Position => _engine.Position;
    public TimeSpan Duration => _engine.Duration;
    public int Volume => _engine.Volume;
    public double Speed => _engine.Speed;
    public int Brightness => _engine.Brightness;
    public double SubtitleDelay => _engine.SubtitleDelay;
    public string? CurrentFilePath => _engine.CurrentFilePath;
    public string? MediaTitle => _engine.MediaTitle;
    public string? VideoCodec => _engine.VideoCodec;
    public string? AudioCodec => _engine.AudioCodec;
    public string? Resolution => _engine.Resolution;
    public string AspectRatio => _engine.AspectRatio;
    public bool IsNightModeActive => _engine.IsNightModeActive;
    public VideoRotation Rotation => _engine.Rotation;
    public IReadOnlyList<TrackInfo> AudioTracks => _engine.AudioTracks;
    public IReadOnlyList<TrackInfo> SubtitleTracks => _engine.SubtitleTracks;
    public string? SnapshotDirectory
    {
        get => _engine.SnapshotDirectory;
        set => _engine.SnapshotDirectory = value;
    }

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler? TracksChanged;
    public event EventHandler<PlaybackStatistics>? StatisticsUpdated;

    public void Load(string filePath, double startPosition = 0) => _engine.Load(filePath, startPosition);
    public void Play() => _engine.Play();
    public void Pause() => _engine.Pause();
    public void Stop() => _engine.Stop();
    public void TogglePlayPause() => _engine.TogglePlayPause();
    public void Seek(double seconds) => _engine.ExactSeek(seconds);
    public void FastSeek(double seconds) => _engine.FastSeek(seconds);
    public void ExactSeek(double seconds) => _engine.ExactSeek(seconds);
    public void SeekRelative(double offsetSeconds) => _engine.SeekRelative(offsetSeconds);
    public void SetVolume(int volume) => _engine.SetVolume(volume);
    public void SetSpeed(double speed) => _engine.SetSpeed(speed);
    public void SetBrightness(int brightness) => _engine.SetBrightness(brightness);
    public void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue) => _engine.SetPictureAdjustment(brightness, contrast, saturation, gamma, hue);
    public void SetRotation(VideoRotation rotation) => _engine.SetRotation(rotation);
    public void SetSubtitleDelay(double seconds) => _engine.SetSubtitleDelay(seconds);
    public void SetAudioDelay(double milliseconds) => _engine.SetAudioDelay(milliseconds);
    public void ToggleNightMode() => _engine.ToggleNightMode();
    public void SetAspectRatio(string ratio) => _engine.SetAspectRatio(ratio);
    public void SelectTrack(TrackInfo track) => _engine.SelectTrack(track);
    public void RefreshTracks() => _engine.RefreshTracks();
    public void AddSubtitleFile(string path) => _engine.AddSubtitleFile(path);
    public void StepFrame(bool forward = true) => _engine.StepFrame(forward);
    public string TakeSnapshot() => _engine.TakeSnapshot(true);
    public string TakeSnapshot(bool includeSubtitles = true) => _engine.TakeSnapshot(includeSubtitles);
    public PlaybackStatistics GetCurrentStatistics() => _engine.GetCurrentStatistics();
    public void AttachContext(MpvContext context) => _engine.AttachContext(context);

    public void Dispose()
    {
        _engine.Dispose();
    }
}
