using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CorePlayer.Engine;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Services;

public interface IThumbnailService : IDisposable
{
    void SetSource(string? filePath, double duration);
    Bitmap? GetCachedThumbnail(double seconds);
    Task<Bitmap?> GetThumbnailAsync(double seconds, CancellationToken cancellationToken = default);
    int CachedThumbnailCount { get; }
    void Clear();
}

public class ThumbnailService : IThumbnailService
{
    private readonly object _syncLock = new();
    private string? _currentFilePath;
    private double _duration;
    private string? _tempDir;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    // Timestamp (seconds) -> File path on disk
    private readonly ConcurrentDictionary<double, string> _thumbnailPaths = new();
    
    // In-memory cache for fast display: Timestamp (seconds) -> Avalonia Bitmap
    private readonly Dictionary<double, Bitmap> _bitmapCache = new();

    public int CachedThumbnailCount => _thumbnailPaths.Count;

    public ThumbnailService()
    {
        CleanupOldTempDirs();
    }

    private static void CleanupOldTempDirs()
    {
        try
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "CorePlayer_Thumbs");
            if (Directory.Exists(baseDir))
            {
                var dirInfo = new DirectoryInfo(baseDir);
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    try
                    {
                        // Clean up temp directories older than 1 day
                        if (DateTime.Now - subDir.CreationTime > TimeSpan.FromDays(1))
                        {
                            subDir.Delete(true);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public void SetSource(string? filePath, double duration)
    {
        Clear();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || duration <= 0)
        {
            return;
        }

        _currentFilePath = filePath;
        _duration = duration;
        _tempDir = Path.Combine(Path.GetTempPath(), "CorePlayer_Thumbs", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(_tempDir);
        }
        catch
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _backgroundTask = Task.Run(() => GenerateThumbnailsWorker(filePath, duration, _tempDir, ct), ct);
    }

    private void GenerateThumbnailsWorker(string filePath, double duration, string tempDir, CancellationToken ct)
    {
        MpvContext? ctx = null;
        try
        {
            ctx = new MpvContext();
            ctx.Option["audio"].Set("no");
            ctx.Option["sub"].Set("no");
            ctx.Option["sid"].Set("no");
            ctx.Option["aid"].Set("no");
            ctx.Option["keep-open"].Set("yes");
            ctx.Option["pause"].Set("yes");
            ctx.Option["vo"].Set("image");
            ctx.Option["vf"].Set("scale=280:-1");
            ctx.Option["hwdec"].Set("auto-safe");

            ctx.RunCommand(default, "loadfile", filePath);

            // Wait for file to load (up to 3 seconds)
            for (int i = 0; i < 30; i++)
            {
                if (ct.IsCancellationRequested) return;
                Thread.Sleep(100);
                try
                {
                    var d = ctx.Duration.Get();
                    if (d.HasValue && d.Value > 0) break;
                }
                catch { }
            }

            if (ct.IsCancellationRequested) return;

            // Adaptive step size: ~60 to 100 samples across duration, bounded between 2s and 20s
            double step = Math.Max(2.0, Math.Min(20.0, duration / 80.0));
            int index = 0;

            for (double pos = 0; pos <= duration; pos += step)
            {
                if (ct.IsCancellationRequested) return;

                var thumbFile = Path.Combine(tempDir, $"thumb_{index++}_{Math.Round(pos)}.jpg");
                try
                {
                    var posStr = pos.ToString(CultureInfo.InvariantCulture);
                    ctx.RunCommand(default, "seek", posStr, "absolute+keyframes");
                    ctx.RunCommand(default, "screenshot-to-file", thumbFile, "video");

                    if (File.Exists(thumbFile) && new FileInfo(thumbFile).Length > 0)
                    {
                        _thumbnailPaths[pos] = thumbFile;
                    }
                }
                catch
                {
                    // Frame extraction error on specific position, proceed to next
                }

                // Subtle pause to keep background CPU utilization minimal
                Thread.Sleep(15);
            }
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Thumbnail worker exception: {ex.Message}\n");
            }
            catch { }
        }
        finally
        {
            try
            {
                ctx?.Dispose();
            }
            catch { }
        }
    }

    public Bitmap? GetCachedThumbnail(double seconds)
    {
        if (_thumbnailPaths.IsEmpty) return null;

        lock (_syncLock)
        {
            double closestPos = -1;
            double minDiff = double.MaxValue;

            foreach (var pos in _thumbnailPaths.Keys)
            {
                var diff = Math.Abs(pos - seconds);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestPos = pos;
                }
            }

            if (closestPos >= 0 && _thumbnailPaths.TryGetValue(closestPos, out var path) && File.Exists(path))
            {
                if (_bitmapCache.TryGetValue(closestPos, out var cachedBmp))
                {
                    return cachedBmp;
                }

                try
                {
                    using var stream = File.OpenRead(path);
                    var bmp = new Bitmap(stream);

                    // Maintain max 120 bitmaps in memory cache
                    if (_bitmapCache.Count > 120)
                    {
                        foreach (var oldBmp in _bitmapCache.Values)
                        {
                            try { oldBmp.Dispose(); } catch { }
                        }
                        _bitmapCache.Clear();
                    }

                    _bitmapCache[closestPos] = bmp;
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    public Task<Bitmap?> GetThumbnailAsync(double seconds, CancellationToken cancellationToken = default)
    {
        var cached = GetCachedThumbnail(seconds);
        return Task.FromResult(cached);
    }

    public void Clear()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { }

        lock (_syncLock)
        {
            foreach (var bmp in _bitmapCache.Values)
            {
                try { bmp.Dispose(); } catch { }
            }
            _bitmapCache.Clear();
            _thumbnailPaths.Clear();
        }

        if (!string.IsNullOrWhiteSpace(_tempDir) && Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch { }
            _tempDir = null;
        }

        _currentFilePath = null;
        _duration = 0;
    }

    public void Dispose()
    {
        Clear();
    }
}
