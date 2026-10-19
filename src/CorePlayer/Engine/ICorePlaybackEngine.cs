using System;
using System.Collections.Generic;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Engine;

public interface ICorePlaybackEngine : IDisposable
{
    MpvContext Context { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    int Volume { get; }
    double Speed { get; }
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

    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    event EventHandler? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler? TracksChanged;
    event EventHandler<PlaybackStatistics>? StatisticsUpdated;

    void Load(string mediaPath, double startPosition = 0);
    void Play();
    void Pause();
    void Stop();
    void TogglePlayPause();
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
    void AddSubtitleFile(string path);
    void StepFrame(bool forward = true);
    string TakeSnapshot(bool includeSubtitles = true);
    PlaybackStatistics GetCurrentStatistics();
    void AttachContext(MpvContext context);
}
