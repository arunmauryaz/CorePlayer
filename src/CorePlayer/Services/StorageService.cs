using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CorePlayer.Models;

namespace CorePlayer.Services;

public interface IStorageService
{
    void SavePlaybackPosition(string filePath, double seconds, double totalDuration);
    double? GetPlaybackPosition(string filePath);
    List<PlaybackHistoryItem> GetRecentFiles(int limit = 10);
    PlayerSettings LoadSettings();
    void SaveSettings(PlayerSettings settings);
}

public class PlayerSettings
{
    public int Volume { get; set; } = 100;
    public double Speed { get; set; } = 1.0;
    public bool Shuffle { get; set; } = false;
    public int RepeatMode { get; set; } = 0; // 0 = Off, 1 = Repeat All, 2 = Repeat One
    public bool AutoResume { get; set; } = true;
    public bool AutoPlayNextInFolder { get; set; } = true;
    public string? SnapshotDirectory { get; set; }
    public double SeekStepSeconds { get; set; } = 5.0;
    public bool HoverPreview { get; set; } = true;
}

public class StorageService : IStorageService
{
    private readonly string _storageDir;
    private readonly string _historyPath;
    private readonly string _settingsPath;
    private readonly Dictionary<string, PlaybackHistoryItem> _historyCache = new(StringComparer.OrdinalIgnoreCase);
    private PlayerSettings? _cachedSettings;

    public StorageService()
    {
        _storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CorePlayer");
        _historyPath = Path.Combine(_storageDir, "playback_history.json");
        _settingsPath = Path.Combine(_storageDir, "settings.json");

        EnsureDirectoryExists();
        LoadHistoryCache();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_storageDir))
        {
            Directory.CreateDirectory(_storageDir);
        }
    }

    private void LoadHistoryCache()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                var items = JsonSerializer.Deserialize<List<PlaybackHistoryItem>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrWhiteSpace(item.FilePath))
                        {
                            _historyCache[item.FilePath] = item;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback gracefully on parsing issues
        }
    }

    private void PersistHistory()
    {
        try
        {
            EnsureDirectoryExists();
            var list = new List<PlaybackHistoryItem>(_historyCache.Values);
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
        catch
        {
            // Ignore non-critical persistence failures
        }
    }

    public void SavePlaybackPosition(string filePath, double seconds, double totalDuration)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        // If played to the very end (>95%), reset to 0 so next open starts fresh
        if (totalDuration > 0 && seconds / totalDuration >= 0.95)
        {
            seconds = 0;
        }

        _historyCache[filePath] = new PlaybackHistoryItem
        {
            FilePath = filePath,
            LastPositionSeconds = seconds,
            DurationSeconds = totalDuration,
            LastPlayedAt = DateTime.UtcNow
        };

        PersistHistory();
    }

    public double? GetPlaybackPosition(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        if (_historyCache.TryGetValue(filePath, out var item))
        {
            // Only suggest resume if played at least 5 seconds and not finished
            if (item.LastPositionSeconds > 5)
            {
                return item.LastPositionSeconds;
            }
        }
        return null;
    }

    public List<PlaybackHistoryItem> GetRecentFiles(int limit = 10)
    {
        var list = new List<PlaybackHistoryItem>(_historyCache.Values);
        list.Sort((a, b) => b.LastPlayedAt.CompareTo(a.LastPlayedAt));
        if (list.Count > limit)
        {
            list = list.GetRange(0, limit);
        }
        return list;
    }

    public PlayerSettings LoadSettings()
    {
        if (_cachedSettings != null) return _cachedSettings;

        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<PlayerSettings>(json);
                if (settings != null)
                {
                    _cachedSettings = settings;
                    return settings;
                }
            }
        }
        catch
        {
            // Fallback to defaults
        }

        _cachedSettings = new PlayerSettings();
        return _cachedSettings;
    }

    public void SaveSettings(PlayerSettings settings)
    {
        _cachedSettings = settings;
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore
        }
    }
}
