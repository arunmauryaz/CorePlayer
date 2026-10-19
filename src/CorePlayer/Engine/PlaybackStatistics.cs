using System;

namespace CorePlayer.Engine;

public class PlaybackStatistics
{
    public double Fps { get; init; }
    public int DroppedFrames { get; init; }
    public string VideoCodec { get; init; } = string.Empty;
    public string AudioCodec { get; init; } = string.Empty;
    public string HardwareDecoder { get; init; } = string.Empty;
    public double VideoBitrate { get; init; } // bps
    public double AudioBitrate { get; init; } // bps
    public double CacheFillPercent { get; init; }
    public TimeSpan CacheTime { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double AspectRatio { get; init; }

    public bool IsHardwareAccelerated => 
        !string.IsNullOrWhiteSpace(HardwareDecoder) && 
        !string.Equals(HardwareDecoder, "no", StringComparison.OrdinalIgnoreCase);

    public string FormattedVideoBitrate => VideoBitrate switch
    {
        >= 1_000_000 => $"{(VideoBitrate / 1_000_000):0.#} Mbps",
        >= 1_000 => $"{(VideoBitrate / 1_000):0} kbps",
        _ => $"{VideoBitrate:0} bps"
    };

    public string FormattedResolution => (Width, Height) switch
    {
        (>= 3800, >= 2100) => $"4K UHD ({Width}x{Height})",
        (>= 2500, >= 1400) => $"1440p QHD ({Width}x{Height})",
        (>= 1900, >= 1000) => $"1080p FHD ({Width}x{Height})",
        (>= 1200, >= 700) => $"720p HD ({Width}x{Height})",
        (> 0, > 0) => $"{Width}x{Height}",
        _ => "Unknown"
    };
}
