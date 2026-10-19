using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Threading;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Engine;

public class CorePlaybackEngine : ICorePlaybackEngine
{
    private readonly MpvContext _context;
    private readonly DispatcherTimer _pollTimer;
    private readonly HardwarePipelineBuilder _pipelineBuilder = new();
    private readonly AudioSubsystem _audioSubsystem = new();
    private readonly SubtitleSubsystem _subtitleSubsystem = new();
    private readonly VideoSubsystem _videoSubsystem = new();
    private readonly List<TrackInfo> _audioTracks = [];
    private readonly List<TrackInfo> _subtitleTracks = [];
    private readonly EngineOptions _options;

    private string? _currentFilePath;
    private bool _isPlaying;
    private TimeSpan _position = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private int _volume = 100;
    private double _speed = 1.0;
    private int _brightness = 0;
    private double _subtitleDelay = 0.0;
    private string _aspectRatio = "-1";
    private bool _isNightModeActive;
    private VideoRotation _rotation = VideoRotation.Normal;
    private bool _isDisposed;
    private bool _eofHandled;
    private int _pollTicks;

    public MpvContext Context => _context;
    public bool IsPlaying => _isPlaying;
    public TimeSpan Position => _position;
    public TimeSpan Duration => _duration;
    public int Volume => _volume;
    public double Speed => _speed;
    public int Brightness => _brightness;
    public double SubtitleDelay => _subtitleDelay;
    public string? CurrentFilePath => _currentFilePath;
    public string? MediaTitle => SafeGet(() => _context.MediaTitle.Get()) ?? Path.GetFileNameWithoutExtension(_currentFilePath);
    public string? VideoCodec => SafeGet(() => _context.VideoCodec.Get());
    public string? AudioCodec => SafeGet(() => _context.AudioCodec.Get());
    public string? Resolution
    {
        get
        {
            var w = SafeGet(() => _context.Width.Get()) ?? 0;
            var h = SafeGet(() => _context.Height.Get()) ?? 0;
            return w > 0 && h > 0 ? $"{w}x{h}" : null;
        }
    }
    public string AspectRatio => _aspectRatio;
    public bool IsNightModeActive => _isNightModeActive;
    public VideoRotation Rotation => _rotation;
    public IReadOnlyList<TrackInfo> AudioTracks => _audioTracks;
    public IReadOnlyList<TrackInfo> SubtitleTracks => _subtitleTracks;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler? TracksChanged;
    public event EventHandler<PlaybackStatistics>? StatisticsUpdated;

    private static T? SafeGet<T>(Func<T?> getter, T? defaultValue = default)
    {
        try
        {
            return getter();
        }
        catch
        {
            return defaultValue;
        }
    }

    public CorePlaybackEngine(EngineOptions? options = null)
    {
        _options = options ?? new EngineOptions();
        _context = new MpvContext();

        var builtOptions = _pipelineBuilder.BuildOptions(_options);
        foreach (var (k, v) in builtOptions)
        {
            try
            {
                _context.Option[k].Set(v);
            }
            catch
            {
                // Best effort
            }
        }

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        try
        {
            var isPaused = SafeGet(() => _context.Pause.Get()) == true;
            var timePos = SafeGet(() => _context.TimePos.Get());
            var currentlyPlaying = !isPaused && (timePos ?? 0) > 0;
            if (currentlyPlaying != _isPlaying)
            {
                _isPlaying = currentlyPlaying;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (timePos.HasValue)
            {
                var newPos = TimeSpan.FromSeconds(Math.Max(0, timePos.Value));
                if (Math.Abs((newPos - _position).TotalMilliseconds) >= 15)
                {
                    _position = newPos;
                    PositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            var dur = SafeGet(() => _context.Duration.Get());
            if (dur.HasValue && dur.Value > 0)
            {
                var newDur = TimeSpan.FromSeconds(dur.Value);
                if (newDur != _duration)
                {
                    _duration = newDur;
                    MediaOpened?.Invoke(this, EventArgs.Empty);
                    RefreshTracks();
                }
            }

            var eof = SafeGet(() => _context.EofReached.Get()) == true;
            if (eof)
            {
                if (!_eofHandled)
                {
                    _eofHandled = true;
                    MediaEnded?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _eofHandled = false;
            }

            _pollTicks++;
            if (_pollTicks % 60 == 0)
            {
                StatisticsUpdated?.Invoke(this, GetCurrentStatistics());
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Poll tick unhandled: {ex}\n");
        }
    }

    public void Load(string mediaPath, double startPosition = 0)
    {
        if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
            return;

        _currentFilePath = mediaPath;
        _position = TimeSpan.Zero;
        _duration = TimeSpan.Zero;
        _eofHandled = false;

        _context.RunCommand(default, "loadfile", mediaPath);

        if (startPosition > 0)
        {
            try
            {
                _context.TimePos.Set(startPosition);
            }
            catch
            {
                // File may still be loading
            }
        }

        try
        {
            var discoveredSubs = _subtitleSubsystem.FindSubtitlesForMedia(mediaPath);
            foreach (var sub in discoveredSubs)
            {
                _context.RunCommand(default, "sub-add", sub, "auto");
            }
        }
        catch
        {
            // Best effort subtitle discovery
        }

        _isPlaying = true;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        _context.Pause.Set(false);
        _isPlaying = true;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        _context.Pause.Set(true);
        _isPlaying = false;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        try
        {
            _context.Pause.Set(true);
            _context.TimePos.Set(0);
        }
        catch { }
        _position = TimeSpan.Zero;
        _isPlaying = false;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePlayPause()
    {
        if (_isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void FastSeek(double seconds)
    {
        if (seconds < 0) seconds = 0;
        if (_duration.TotalSeconds > 0 && seconds > _duration.TotalSeconds)
            seconds = _duration.TotalSeconds;

        var secStr = seconds.ToString(CultureInfo.InvariantCulture);
        try
        {
            _context.RunCommandString($"seek {secStr} absolute+keyframes");
        }
        catch
        {
            try { _context.TimePos.Set(seconds); } catch { }
        }
        _position = TimeSpan.FromSeconds(seconds);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExactSeek(double seconds)
    {
        if (seconds < 0) seconds = 0;
        if (_duration.TotalSeconds > 0 && seconds > _duration.TotalSeconds)
            seconds = _duration.TotalSeconds;

        var secStr = seconds.ToString(CultureInfo.InvariantCulture);
        try
        {
            _context.RunCommandString($"seek {secStr} absolute+exact");
        }
        catch
        {
            try { _context.TimePos.Set(seconds); } catch { }
        }
        _position = TimeSpan.FromSeconds(seconds);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SeekRelative(double offsetSeconds)
    {
        var target = _position.TotalSeconds + offsetSeconds;
        ExactSeek(target);
    }

    public void SetVolume(int volume)
    {
        volume = Math.Clamp(volume, 0, 150);
        _volume = volume;
        try
        {
            _context.RunCommandString($"set volume {volume}");
        }
        catch
        {
            try { _context.Option["volume"].Set(volume.ToString()); } catch { }
        }
    }

    public void SetSpeed(double speed)
    {
        speed = Math.Clamp(speed, 0.25, 4.0);
        _speed = speed;
        try
        {
            var cmd = _audioSubsystem.BuildSpeedCommand(speed, preservePitch: true);
            _context.RunCommandString(cmd);
        }
        catch
        {
            try { _context.Option["speed"].Set(speed.ToString(CultureInfo.InvariantCulture)); } catch { }
        }
    }

    public void SetBrightness(int brightness)
    {
        _brightness = brightness;
        SetPictureAdjustment(brightness, 0, 0, 0, 0);
    }

    public void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue)
    {
        _brightness = brightness;
        try
        {
            var cmd = _videoSubsystem.BuildColorAdjustmentCommand(brightness, contrast, saturation, gamma, hue);
            _context.RunCommandString(cmd);
        }
        catch
        {
            try { _context.Brightness.Set(brightness); } catch { }
        }
    }

    public void SetRotation(VideoRotation rotation)
    {
        _rotation = rotation;
        try
        {
            var cmd = _videoSubsystem.BuildRotationCommand(rotation);
            _context.RunCommandString(cmd);
        }
        catch
        {
            // Best effort
        }
    }

    public void SetSubtitleDelay(double seconds)
    {
        _subtitleDelay = seconds;
        try
        {
            _context.SubDelay.Set(seconds);
        }
        catch
        {
            // Ignore
        }
    }

    public void SetAudioDelay(double milliseconds)
    {
        try
        {
            var cmd = _audioSubsystem.BuildDelayCommand(milliseconds);
            _context.RunCommandString(cmd);
        }
        catch
        {
            // Ignore
        }
    }

    public void ToggleNightMode()
    {
        _isNightModeActive = !_isNightModeActive;
        try
        {
            var filter = _audioSubsystem.BuildNormalizationFilter(_isNightModeActive);
            if (_isNightModeActive)
            {
                _context.RunCommandString($"set af \"{filter}\"");
            }
            else
            {
                _context.RunCommandString("set af \"\"");
            }
        }
        catch
        {
            // Best effort
        }
    }

    public void SetAspectRatio(string ratio)
    {
        _aspectRatio = ratio;
        try
        {
            var cmd = _videoSubsystem.BuildAspectRatioCommand(ratio);
            _context.RunCommandString(cmd);
        }
        catch
        {
            // Best effort
        }
    }

    public void SelectTrack(TrackInfo track)
    {
        try
        {
            if (track.Kind == TrackKind.Audio)
            {
                _context.Option["aid"].Set(track.Id.ToString());
            }
            else
            {
                _context.Option["sid"].Set(track.Id.ToString());
            }
            RefreshTracks();
        }
        catch
        {
            // Ignore
        }
    }

    public void AddSubtitleFile(string path)
    {
        if (File.Exists(path))
        {
            _context.RunCommand(default, "sub-add", path);
            RefreshTracks();
        }
    }

    public void StepFrame(bool forward = true)
    {
        Pause();
        _context.RunCommand(default, forward ? "frame-step" : "frame-back-step");
    }

    public string TakeSnapshot(bool includeSubtitles = true)
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var folder = Path.Combine(pictures, "CorePlayer_Snapshots");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var fileName = $"Snap_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var fullPath = Path.Combine(folder, fileName);

        var subArg = includeSubtitles ? "subtitles" : "video";
        _context.RunCommand(default, "screenshot-to-file", fullPath, subArg);
        return fullPath;
    }

    public PlaybackStatistics GetCurrentStatistics()
    {
        try
        {
            double fps = 0;
            var fpsStr = _context.GetPropertyString("estimated-vf-fps") ?? _context.GetPropertyString("fps");
            if (!string.IsNullOrEmpty(fpsStr))
            {
                double.TryParse(fpsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
            }

            int dropped = 0;
            var dropStr = _context.GetPropertyString("frame-drop-count") ?? _context.GetPropertyString("decoder-frame-drop-count");
            if (!string.IsNullOrEmpty(dropStr))
            {
                int.TryParse(dropStr, NumberStyles.Any, CultureInfo.InvariantCulture, out dropped);
            }

            var vCodec = _context.VideoCodec.Get() ?? _context.GetPropertyString("video-codec") ?? "";
            var aCodec = _context.AudioCodec.Get() ?? _context.GetPropertyString("audio-codec") ?? "";
            var hwdec = _context.GetPropertyString("hwdec-current") ?? _context.Option["hwdec"].Get() ?? "";

            double vBitrate = 0;
            var vbStr = _context.GetPropertyString("video-bitrate");
            if (!string.IsNullOrEmpty(vbStr))
            {
                double.TryParse(vbStr, NumberStyles.Any, CultureInfo.InvariantCulture, out vBitrate);
            }

            double aBitrate = 0;
            var abStr = _context.GetPropertyString("audio-bitrate");
            if (!string.IsNullOrEmpty(abStr))
            {
                double.TryParse(abStr, NumberStyles.Any, CultureInfo.InvariantCulture, out aBitrate);
            }

            double cacheFill = 0;
            var cfStr = _context.GetPropertyString("demuxer-cache-state/free-percent");
            if (!string.IsNullOrEmpty(cfStr) && double.TryParse(cfStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var free))
            {
                cacheFill = Math.Max(0, 100 - free);
            }

            TimeSpan cacheTime = TimeSpan.Zero;
            var cdStr = _context.GetPropertyString("demuxer-cache-duration");
            if (!string.IsNullOrEmpty(cdStr) && double.TryParse(cdStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var cd))
            {
                cacheTime = TimeSpan.FromSeconds(cd);
            }

            int width = _context.Width.Get() ?? 0;
            if (width <= 0)
            {
                var wStr = _context.GetPropertyString("width");
                int.TryParse(wStr, out width);
            }

            int height = _context.Height.Get() ?? 0;
            if (height <= 0)
            {
                var hStr = _context.GetPropertyString("height");
                int.TryParse(hStr, out height);
            }

            double aspect = width > 0 && height > 0 ? (double)width / height : 0;

            return new PlaybackStatistics
            {
                Fps = fps,
                DroppedFrames = dropped,
                VideoCodec = vCodec,
                AudioCodec = aCodec,
                HardwareDecoder = hwdec,
                VideoBitrate = vBitrate,
                AudioBitrate = aBitrate,
                CacheFillPercent = cacheFill,
                CacheTime = cacheTime,
                Width = width,
                Height = height,
                AspectRatio = aspect
            };
        }
        catch
        {
            return new PlaybackStatistics();
        }
    }

    private void RefreshTracks()
    {
        try
        {
            _audioTracks.Clear();
            _subtitleTracks.Clear();

            var count = _context.TrackListCount.Get() ?? 0;
            for (int i = 0; i < count; i++)
            {
                var id = _context.TrackListId[i].Get() ?? i;
                var type = _context.TrackListType[i].Get();
                var lang = _context.TrackListLanguage[i].Get() ?? "";
                var codec = _context.TrackListCodec[i].Get() ?? "";
                var selected = _context.TrackListIsSelected[i].Get() == true;
                var title = !string.IsNullOrWhiteSpace(lang) ? lang : $"Track #{id}";

                var track = new TrackInfo
                {
                    Id = id,
                    Language = lang,
                    Title = title,
                    Codec = codec,
                    IsSelected = selected,
                    Kind = string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase) 
                        ? TrackKind.Audio 
                        : TrackKind.Subtitle
                };

                if (track.Kind == TrackKind.Audio)
                {
                    _audioTracks.Add(track);
                }
                else if (string.Equals(type, "sub", StringComparison.OrdinalIgnoreCase))
                {
                    _subtitleTracks.Add(track);
                }
            }
            TracksChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Ignore track enumeration errors
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _pollTimer.Stop();
        try
        {
            _context.RunCommandString("stop");
            _context.Dispose();
        }
        catch
        {
            // Dispose best effort
        }
    }
}
