using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CorePlayer.Engine;

public class SubtitleSubsystem
{
    private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx"];

    public IReadOnlyList<string> FindSubtitlesForMedia(string mediaFilePath)
    {
        if (string.IsNullOrWhiteSpace(mediaFilePath) || !File.Exists(mediaFilePath))
            return Array.Empty<string>();

        var directory = Path.GetDirectoryName(mediaFilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Array.Empty<string>();

        var mediaStem = Path.GetFileNameWithoutExtension(mediaFilePath);
        var candidates = new List<string>();

        // 1. Search in current media directory
        SearchDirectory(directory, mediaStem, candidates);

        // 2. Search in "Subs" or "Subtitles" subdirectory if present
        foreach (var subfolder in new[] { "Subs", "Subtitles", "subtitles", "subs" })
        {
            var subPath = Path.Combine(directory, subfolder);
            if (Directory.Exists(subPath))
            {
                SearchDirectory(subPath, mediaStem, candidates);
            }
        }

        return candidates.Distinct().ToList();
    }

    private static void SearchDirectory(string dir, string mediaStem, List<string> candidates)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (SubtitleExtensions.Contains(ext))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith(mediaStem, StringComparison.OrdinalIgnoreCase) ||
                    mediaStem.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(file);
                }
            }
        }
    }

    public string BuildTypographyCommand(string font, int size, string colorHex, bool backgroundBox)
    {
        var boxOpt = backgroundBox ? "yes" : "no";
        return $"set sub-font \"{font}\"; set sub-font-size {size}; set sub-color \"{colorHex}\"; set sub-back-color \"#80000000\"; set sub-box \"{boxOpt}\"";
    }
}
