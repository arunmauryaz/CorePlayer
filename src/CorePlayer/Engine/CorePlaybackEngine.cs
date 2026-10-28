using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia.Threading;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Engine;

public class CorePlaybackEngine : ICorePlaybackEngine
{
    private MpvContext _context;
    private bool _ownsContext;
    private readonly DispatcherTimer _pollTimer;
    private readonly HardwarePipelineBuilder _pipelineBuilder = new();
    private readonly AudioSubsystem _audioSubsystem = new();
    private readonly SubtitleSubsystem _subtitleSubsystem = new();
    private readonly VideoSubsystem _videoSubsystem = new();
    private readonly List<TrackInfo> _audioTracks = [];
    private readonly List<TrackInfo> _subtitleTracks = [];
    private readonly EngineOptions _options;

    private int _lastTrackCount = -1;
    private string _lastAid = "";
    private string _lastSid = "";

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

    private double? _pendingSeekTarget;
    private long _suppressPositionUpdateUntilTicks;
    private string? _snapshotDirectory;

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
    public string? SnapshotDirectory
    {
        get => _snapshotDirectory;
        set => _snapshotDirectory = value;
    }

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

    public CorePlaybackEngine(EngineOptions? options = null) : this(null, options)
    {
    }

    public CorePlaybackEngine(MpvContext? context, EngineOptions? options = null)
    {
        _options = options ?? new EngineOptions();
        _ownsContext = context == null;
        _context = context ?? new MpvContext();

        ApplyPipelineOptions();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void ApplyPipelineOptions()
    {
        var builtOptions = _pipelineBuilder.BuildOptions(_options);
        foreach (var (k, v) in builtOptions)
        {
            try
            {
                _context.Option[k].Set(v);
            }
            catch
            {
                try
                {
                    _context.SetPropertyString(k, v);
                }
                catch
                {
                    // Best effort
                }
            }
        }

        // Explicitly suppress MPV's built-in OSD bar and OSD seek overlays
        try { _context.SetPropertyString("osd-bar", "no"); } catch { }
        try { _context.SetPropertyString("osd-on-seek", "no"); } catch { }
        try { _context.SetPropertyString("osd-level", "0"); } catch { }
    }

    public void AttachContext(MpvContext context)
    {
        if (context == null || ReferenceEquals(_context, context)) return;

        if (_ownsContext)
        {
            try { _context.Dispose(); } catch { }
        }

        _ownsContext = false;
        _context = context;
        ApplyPipelineOptions();

        if (!string.IsNullOrWhiteSpace(_currentFilePath) && File.Exists(_currentFilePath))
        {
            Load(_currentFilePath, _position.TotalSeconds);
        }
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        try
        {
            var isPaused = SafeGet(() => _context.Pause.Get()) == true;
            var timePos = SafeGet(() => _context.TimePos.Get());
            var currentlyPlaying = !isPaused && timePos.HasValue;
            if (currentlyPlaying != _isPlaying)
            {
                _isPlaying = currentlyPlaying;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (timePos.HasValue)
            {
                var nowTicks = Stopwatch.GetTimestamp();
                if (nowTicks >= _suppressPositionUpdateUntilTicks)
                {
                    _pendingSeekTarget = null;
                    var newPos = TimeSpan.FromSeconds(Math.Max(0, timePos.Value));
                    if (Math.Abs((newPos - _position).TotalMilliseconds) >= 15)
                    {
                        _position = newPos;
                        PositionChanged?.Invoke(this, EventArgs.Empty);
                    }
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

            var currentTrackCount = SafeGet(() =>
            {
                var cStr = _context.GetPropertyString("track-list/count");
                if (int.TryParse(cStr, out var c)) return c;
                return _context.TrackListCount.Get();
            }) ?? 0;

            var currentAid = SafeGet(() => _context.GetPropertyString("aid")) ?? "";
            var currentSid = SafeGet(() => _context.GetPropertyString("sid")) ?? "";

            if (currentTrackCount != _lastTrackCount || currentAid != _lastAid || currentSid != _lastSid)
            {
                _lastTrackCount = currentTrackCount;
                _lastAid = currentAid;
                _lastSid = currentSid;
                RefreshTracks();
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
        _lastTrackCount = -1;
        _lastAid = "";
        _lastSid = "";

        if (startPosition > 0)
        {
            try
            {
                _context.Option["start"].Set(startPosition.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }
        else
        {
            try
            {
                _context.Option["start"].Set("none");
            }
            catch { }
        }

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

        _pendingSeekTarget = seconds;
        _suppressPositionUpdateUntilTicks = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 0.40);

        var secStr = seconds.ToString(CultureInfo.InvariantCulture);
        try
        {
            _context.RunCommandString($"no-osd seek {secStr} absolute+keyframes");
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

        _pendingSeekTarget = seconds;
        _suppressPositionUpdateUntilTicks = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * 0.40);

        var secStr = seconds.ToString(CultureInfo.InvariantCulture);
        try
        {
            _context.RunCommandString($"no-osd seek {secStr} absolute+exact");
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
        var baseSeconds = (_suppressPositionUpdateUntilTicks > Stopwatch.GetTimestamp() && _pendingSeekTarget.HasValue)
            ? _pendingSeekTarget.Value
            : _position.TotalSeconds;
        var target = baseSeconds + offsetSeconds;
        ExactSeek(target);
    }

    public void SetVolume(int volume)
    {
        volume = Math.Clamp(volume, 0, 150);
        _volume = volume;
        try
        {
            _context.RunCommandString($"no-osd set volume {volume}");
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
            var prop = track.Kind == TrackKind.Audio ? "aid" : "sid";
            var val = track.Id <= 0 ? "no" : track.Id.ToString();
            try { _context.SetPropertyString(prop, val); } catch { }
            try { _context.RunCommandString($"no-osd set {prop} {val}"); } catch { }
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
        try
        {
            if (_context.TimePos.Get() is double pos)
            {
                _position = TimeSpan.FromSeconds(pos);
                PositionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch { }
    }

    public string TakeSnapshot(bool includeSubtitles = true)
    {
        var folder = !string.IsNullOrWhiteSpace(_snapshotDirectory) && Directory.Exists(_snapshotDirectory)
            ? _snapshotDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CorePlayer_Snapshots");
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

    public void RefreshTracks()
    {
        try
        {
            var audioList = new List<TrackInfo>();
            var subList = new List<TrackInfo>();

            int count = 0;
            var countStr = SafeGet(() => _context.GetPropertyString("track-list/count"));
            if (!int.TryParse(countStr, out count))
            {
                count = SafeGet(() => _context.TrackListCount.Get()) ?? 0;
            }

            var currentAid = SafeGet(() => _context.GetPropertyString("aid")) ?? "";
            var currentSid = SafeGet(() => _context.GetPropertyString("sid")) ?? "";

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var type = SafeGet(() => _context.GetPropertyString($"track-list/{i}/type"))
                        ?? SafeGet(() => _context.TrackListType[i].Get());

                    if (string.IsNullOrEmpty(type))
                        continue;

                    var isAudio = string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase);
                    var isSub = string.Equals(type, "sub", StringComparison.OrdinalIgnoreCase);

                    if (!isAudio && !isSub)
                        continue;

                    var idStr = SafeGet(() => _context.GetPropertyString($"track-list/{i}/id"));
                    int id = int.TryParse(idStr, out var parsedId)
                        ? parsedId
                        : (SafeGet(() => _context.TrackListId[i].Get()) ?? (i + 1));

                    var selectedStr = SafeGet(() => _context.GetPropertyString($"track-list/{i}/selected"));
                    bool selected = string.Equals(selectedStr, "yes", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(selectedStr, "true", StringComparison.OrdinalIgnoreCase)
                        || (isAudio && !string.IsNullOrEmpty(currentAid) && currentAid == id.ToString())
                        || (isSub && !string.IsNullOrEmpty(currentSid) && currentSid == id.ToString())
                        || (SafeGet(() => _context.TrackListIsSelected[i].Get()) == true);

                    var rawTitle = SafeGet(() => _context.GetPropertyString($"track-list/{i}/title")) ?? "";
                    var langCode = SafeGet(() => _context.GetPropertyString($"track-list/{i}/lang"))
                        ?? SafeGet(() => _context.TrackListLanguage[i].Get()) ?? "";
                    var codec = SafeGet(() => _context.GetPropertyString($"track-list/{i}/codec"))
                        ?? SafeGet(() => _context.TrackListCodec[i].Get()) ?? "";

                    var langDisplay = FormatLanguageName(langCode);

                    var track = new TrackInfo
                    {
                        Id = id,
                        Language = langDisplay,
                        Title = rawTitle,
                        Codec = codec,
                        IsSelected = selected,
                        Kind = isAudio ? TrackKind.Audio : TrackKind.Subtitle
                    };

                    if (isAudio)
                    {
                        audioList.Add(track);
                    }
                    else
                    {
                        subList.Add(track);
                    }
                }
                catch
                {
                    // Continue with next track if any individual track has unexpected properties
                }
            }

            _audioTracks.Clear();
            _audioTracks.AddRange(audioList);

            _subtitleTracks.Clear();
            _subtitleTracks.AddRange(subList);

            TracksChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Ignore track enumeration errors
        }
    }

    private static string FormatLanguageName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        code = code.Trim();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "eng", "English" }, { "en", "English" },
            { "hin", "Hindi" }, { "hi", "Hindi" },
            { "jpn", "Japanese" }, { "ja", "Japanese" },
            { "spa", "Spanish" }, { "es", "Spanish" },
            { "fra", "French" }, { "fre", "French" }, { "fr", "French" },
            { "deu", "German" }, { "ger", "German" }, { "de", "German" },
            { "ita", "Italian" }, { "it", "Italian" },
            { "kor", "Korean" }, { "ko", "Korean" },
            { "zho", "Chinese" }, { "chi", "Chinese" }, { "zh", "Chinese" },
            { "rus", "Russian" }, { "ru", "Russian" },
            { "por", "Portuguese" }, { "pt", "Portuguese" },
            { "ara", "Arabic" }, { "ar", "Arabic" },
            { "tam", "Tamil" }, { "ta", "Tamil" },
            { "tel", "Telugu" }, { "te", "Telugu" },
            { "ben", "Bengali" }, { "bn", "Bengali" },
            { "mar", "Marathi" }, { "mr", "Marathi" },
            { "pan", "Punjabi" }, { "pa", "Punjabi" },
            { "mal", "Malayalam" }, { "ml", "Malayalam" },
            { "kan", "Kannada" }, { "kn", "Kannada" },
            { "guj", "Gujarati" }, { "gu", "Gujarati" },
            { "urd", "Urdu" }, { "ur", "Urdu" },
            { "vie", "Vietnamese" }, { "vi", "Vietnamese" },
            { "tha", "Thai" }, { "th", "Thai" },
            { "ind", "Indonesian" }, { "id", "Indonesian" },
            { "pol", "Polish" }, { "pl", "Polish" },
            { "tur", "Turkish" }, { "tr", "Turkish" },
            { "nld", "Dutch" }, { "dut", "Dutch" }, { "nl", "Dutch" },
            { "swe", "Swedish" }, { "sv", "Swedish" },
            { "nor", "Norwegian" }, { "no", "Norwegian" },
            { "dan", "Danish" }, { "da", "Danish" },
            { "fin", "Finnish" }, { "fi", "Finnish" },
            { "ces", "Czech" }, { "cze", "Czech" }, { "cs", "Czech" },
            { "hun", "Hungarian" }, { "hu", "Hungarian" },
            { "ell", "Greek" }, { "gre", "Greek" }, { "el", "Greek" },
            { "heb", "Hebrew" }, { "he", "Hebrew" },
            { "ukr", "Ukrainian" }, { "uk", "Ukrainian" }
        };

        if (map.TryGetValue(code, out var name))
            return name;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            if (!string.IsNullOrWhiteSpace(culture.EnglishName) && !culture.EnglishName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                return culture.EnglishName;
        }
        catch { }

        return code.Length <= 4 ? code.ToUpperInvariant() : char.ToUpperInvariant(code[0]) + code[1..];
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _pollTimer.Stop();
        try
        {
            _context.RunCommandString("stop");
            if (_ownsContext)
            {
                _context.Dispose();
            }
        }
        catch
        {
            // Dispose best effort
        }
    }
}
