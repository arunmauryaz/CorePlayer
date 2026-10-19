using System;
using System.IO;

namespace CorePlayer.Models;

public class MediaItem
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public TimeSpan LastPosition { get; set; } = TimeSpan.Zero;
    public DateTime DateAdded { get; set; } = DateTime.Now;

    public string FileName => !string.IsNullOrEmpty(FilePath) ? Path.GetFileName(FilePath) : Title;

    public string DurationFormatted => Duration.TotalHours >= 1 
        ? Duration.ToString(@"hh\:mm\:ss") 
        : Duration.ToString(@"mm\:ss");

    public string FileSizeFormatted
    {
        get
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                return string.Empty;

            var bytes = new FileInfo(FilePath).Length;
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}

public class PlaybackHistoryItem
{
    public string FilePath { get; set; } = string.Empty;
    public double LastPositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime LastPlayedAt { get; set; } = DateTime.Now;
}

public enum TrackKind
{
    Audio,
    Subtitle
}

public class TrackInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public TrackKind Kind { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Language)
        ? (!string.IsNullOrWhiteSpace(Title) ? Title : $"Track #{Id}")
        : $"{Language} - {(!string.IsNullOrWhiteSpace(Title) ? Title : Codec)}";
}
