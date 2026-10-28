using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Services;

public interface IThumbnailService : IDisposable
{
    void SetSource(string? filePath, double duration);
    Bitmap? GetCachedThumbnail(double seconds);
    Task<Bitmap?> GetThumbnailAsync(double seconds, CancellationToken cancellationToken = default);
    int CachedThumbnailCount { get; }
    void Clear();
    event EventHandler<(double Seconds, Bitmap Bitmap)>? ThumbnailReady;
}

public class ThumbnailService : IThumbnailService
{
    private readonly object _syncLock = new();
    private string? _currentFilePath;
    private double _duration;
    private string? _tempDir;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;
    private Channel<double>? _channel;

    // LRU in-memory cache: Rounded second -> Avalonia Bitmap
    private readonly Dictionary<int, Bitmap> _bitmapCache = new();
    private readonly LinkedList<int> _lruOrder = new();
    private const int MaxCacheSize = 30;

    public event EventHandler<(double Seconds, Bitmap Bitmap)>? ThumbnailReady;

    public int CachedThumbnailCount
    {
        get
        {
            lock (_syncLock)
            {
                return _bitmapCache.Count;
            }
        }
    }

    public ThumbnailService()
    {
        CleanupOldTempDirs();
    }

    public static void CleanupAllTempDirs()
    {
        try
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "CorePlayer_Thumbs");
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
        }
        catch { }
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
                        if (DateTime.Now - subDir.CreationTime > TimeSpan.FromHours(2))
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

        _channel = Channel.CreateBounded<double>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var reader = _channel.Reader;
        var currentTempDir = _tempDir;

        _workerTask = Task.Run(() => WorkerLoop(filePath, currentTempDir, reader, ct), ct);
    }

    private void WorkerLoop(string filePath, string tempDir, ChannelReader<double> reader, CancellationToken ct)
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
            try { ctx.Option["vo-image-outdir"].Set(tempDir); } catch { }
            ctx.Option["vf"].Set("scale=280:-1");
            ctx.Option["hwdec"].Set("auto-safe");
            ctx.Option["hr-seek"].Set("yes");
            ctx.Option["hr-seek-framedrop"].Set("no");

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

            var thumbFile = Path.Combine(tempDir, "preview_frame.jpg");

            while (!ct.IsCancellationRequested)
            {
                double targetSec;
                try
                {
                    // Asynchronously wait for the next hover request
                    var readTask = reader.ReadAsync(ct).AsTask();
                    readTask.Wait(ct);
                    targetSec = readTask.Result;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
                {
                    break;
                }

                if (ct.IsCancellationRequested) break;

                int key = (int)Math.Round(targetSec);
                Bitmap? existingBmp = null;
                lock (_syncLock)
                {
                    if (_bitmapCache.TryGetValue(key, out existingBmp))
                    {
                        // Already in cache
                    }
                }

                if (existingBmp != null)
                {
                    ThumbnailReady?.Invoke(this, (targetSec, existingBmp));
                    continue;
                }

                try
                {
                    var secStr = targetSec.ToString("0.00", CultureInfo.InvariantCulture);
                    // Use exact seek to retrieve the precise frame of that exact scene
                    ctx.RunCommand(default, "seek", secStr, "absolute+exact");
                    ctx.RunCommand(default, "screenshot-to-file", thumbFile, "video");

                    if (File.Exists(thumbFile) && new FileInfo(thumbFile).Length > 0)
                    {
                        var bytes = File.ReadAllBytes(thumbFile);
                        // Immediately delete the temp file from disk so zero disk storage is wasted
                        try { File.Delete(thumbFile); } catch { }

                        using var ms = new MemoryStream(bytes);
                        var bmp = new Bitmap(ms);

                        lock (_syncLock)
                        {
                            // Maintain bounded LRU in-memory cache (max 30 small bitmaps ~5MB RAM)
                            if (_bitmapCache.Count >= MaxCacheSize)
                            {
                                var oldest = _lruOrder.First?.Value;
                                if (oldest.HasValue)
                                {
                                    _lruOrder.RemoveFirst();
                                    if (_bitmapCache.Remove(oldest.Value, out var evicted))
                                    {
                                        try { evicted.Dispose(); } catch { }
                                    }
                                }
                            }

                            _bitmapCache[key] = bmp;
                            _lruOrder.AddLast(key);
                        }

                        ThumbnailReady?.Invoke(this, (targetSec, bmp));
                    }
                }
                catch
                {
                    // Frame extraction error on specific frame, continue
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Thumbnail worker error: {ex.Message}\n");
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
        int key = (int)Math.Round(seconds);
        lock (_syncLock)
        {
            if (_bitmapCache.TryGetValue(key, out var bmp))
            {
                _lruOrder.Remove(key);
                _lruOrder.AddLast(key);
                return bmp;
            }

            // Return frame within 1-second proximity if available while exact frame loads
            foreach (var offset in new[] { -1, 1 })
            {
                if (_bitmapCache.TryGetValue(key + offset, out var closeBmp))
                {
                    return closeBmp;
                }
            }
        }

        return null;
    }

    public Task<Bitmap?> GetThumbnailAsync(double seconds, CancellationToken cancellationToken = default)
    {
        var cached = GetCachedThumbnail(seconds);
        if (cached != null)
        {
            return Task.FromResult<Bitmap?>(cached);
        }

        // Send to worker channel (drops older pending requests automatically)
        _channel?.Writer.TryWrite(seconds);
        return Task.FromResult<Bitmap?>(null);
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
            _lruOrder.Clear();
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
