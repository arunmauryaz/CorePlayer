using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Threading;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Services;

public interface IPlaybackService
{
    MpvContext Context { get; }
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
    IReadOnlyList<TrackInfo> AudioTracks { get; }
    IReadOnlyList<TrackInfo> SubtitleTracks { get; }

    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    event EventHandler? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler? TracksChanged;

    void Load(string filePath, double startPosition = 0);
    void Play();
    void Pause();
    void Stop();
    void TogglePlayPause();
    void Seek(double seconds);
    void SeekRelative(double offsetSeconds);
    void SetVolume(int volume);
    void SetSpeed(double speed);
    void SetBrightness(int brightness);
    void SetSubtitleDelay(double seconds);
    void SetAspectRatio(string ratio);
    void SelectTrack(TrackInfo track);
    void AddSubtitleFile(string path);
    void StepFrame(bool forward = true);
    string TakeSnapshot();
}

public class MpvPlaybackService : IPlaybackService, IDisposable
{
    private readonly MpvContext _context;
    private readonly DispatcherTimer _pollTimer;
    private string? _currentFilePath;
    private bool _isPlaying;
    private TimeSpan _position = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private int _volume = 100;
    private double _speed = 1.0;
    private int _brightness = 0;
    private double _subtitleDelay = 0.0;
    private readonly List<TrackInfo> _audioTracks = [];
    private readonly List<TrackInfo> _subtitleTracks = [];
    private bool _isDisposed;

    public MpvContext Context => _context;
    public bool IsPlaying => _isPlaying;
    public TimeSpan Position => _position;
    public TimeSpan Duration => _duration;
    public int Volume => _volume;
    public double Speed => _speed;
    public int Brightness => _brightness;
    public double SubtitleDelay => _subtitleDelay;
    public string? CurrentFilePath => _currentFilePath;
    public string? MediaTitle => _context.MediaTitle.Get() ?? Path.GetFileNameWithoutExtension(_currentFilePath);
    public string? VideoCodec => _context.VideoCodec.Get();
    public string? AudioCodec => _context.AudioCodec.Get();
    public string? Resolution => _context.Width.Get() > 0 ? $"{_context.Width.Get()}x{_context.Height.Get()}" : null;
    private string _aspectRatio = "-1";
    public string AspectRatio => _aspectRatio;
    public IReadOnlyList<TrackInfo> AudioTracks => _audioTracks;
    public IReadOnlyList<TrackInfo> SubtitleTracks => _subtitleTracks;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PositionChanged;
    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler? TracksChanged;

    public MpvPlaybackService()
    {
        _context = new MpvContext();

        // Configure hardware acceleration and playback options
        try
        {
            _context.Option["hwdec"].Set("auto-safe");
            _context.Option["keep-open"].Set("yes");
            _context.Option["sub-auto"].Set("fuzzy");
            _context.Option["audio-pitch-correction"].Set("yes");
        }
        catch
        {
            // Best effort
        }

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        try
        {
            var isPaused = _context.Pause.Get() == true;
            var currentlyPlaying = !isPaused && (_context.TimePos.Get() ?? 0) > 0;
            if (currentlyPlaying != _isPlaying)
            {
                _isPlaying = currentlyPlaying;
                PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            }

            var timePos = _context.TimePos.Get();
            if (timePos.HasValue)
            {
                var newPos = TimeSpan.FromSeconds(Math.Max(0, timePos.Value));
                if (Math.Abs((newPos - _position).TotalMilliseconds) > 100)
                {
                    _position = newPos;
                    PositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            var dur = _context.Duration.Get();
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

            // Check if EOF reached
            if (_context.EofReached.Get() == true)
            {
                MediaEnded?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // Ignore polling errors
        }
    }

    public void Load(string filePath, double startPosition = 0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        _currentFilePath = filePath;
        _position = TimeSpan.Zero;
        _duration = TimeSpan.Zero;

        // MPV loadfile command
        var sanitized = filePath.Replace("\"", "\\\"");
        _context.RunCommandString($"loadfile \"{sanitized}\"");

        if (startPosition > 0)
        {
            _context.TimePos.Set(startPosition);
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

    public void Seek(double seconds)
    {
        if (seconds < 0) seconds = 0;
        if (_duration.TotalSeconds > 0 && seconds > _duration.TotalSeconds)
            seconds = _duration.TotalSeconds;

        _context.TimePos.Set(seconds);
        _position = TimeSpan.FromSeconds(seconds);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SeekRelative(double offsetSeconds)
    {
        var target = _position.TotalSeconds + offsetSeconds;
        Seek(target);
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
        speed = Math.Clamp(speed, 0.25, 3.0);
        _speed = speed;
        try
        {
            var str = speed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _context.RunCommandString($"set speed {str}");
        }
        catch
        {
            try { _context.Option["speed"].Set(speed.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch { }
        }
    }

    public void SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, -100, 100);
        _brightness = brightness;
        try
        {
            _context.Brightness.Set(brightness);
        }
        catch
        {
            // Soft fail if brightness property is unsupported on current hwdec
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

    public void SetAspectRatio(string ratio)
    {
        _aspectRatio = ratio;
        try
        {
            _context.RunCommandString($"set video-aspect-override \"{ratio}\"");
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
            var sanitized = path.Replace("\"", "\\\"");
            _context.RunCommandString($"sub-add \"{sanitized}\"");
            RefreshTracks();
        }
    }

    public void StepFrame(bool forward = true)
    {
        Pause();
        _context.RunCommandString(forward ? "frame-step" : "frame-back-step");
    }

    public string TakeSnapshot()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var folder = Path.Combine(pictures, "CorePlayer_Snapshots");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var fileName = $"Snap_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var fullPath = Path.Combine(folder, fileName);

        var sanitized = fullPath.Replace("\"", "\\\"");
        _context.RunCommandString($"screenshot-to-file \"{sanitized}\"");
        return fullPath;
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
