using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class EngineTypesTests
{
    [Fact]
    public void EngineOptions_DefaultValues_AreOptimizedForHighPerformance()
    {
        var options = new EngineOptions();

        Assert.Equal(HardwareDecodeMode.AutoSafe, options.HardwareMode);
        Assert.Equal(QualityProfile.Balanced, options.Quality);
        Assert.True(options.DemuxerMaxBytes >= 100 * 1024 * 1024); // at least 100MB
        Assert.True(options.ReadaheadSeconds >= 10);
        Assert.True(options.AudioPitchCorrection);
    }

    [Fact]
    public void PlaybackStatistics_FormatsBitrateCorrectly()
    {
        var stats = new PlaybackStatistics
        {
            Fps = 59.94,
            DroppedFrames = 0,
            VideoBitrate = 14500000, // 14.5 Mbps
            HardwareDecoder = "d3d11va",
            Width = 3840,
            Height = 2160
        };

        Assert.Equal("14.5 Mbps", stats.FormattedVideoBitrate);
        Assert.Equal("4K UHD (3840x2160)", stats.FormattedResolution);
        Assert.True(stats.IsHardwareAccelerated);
        Assert.Equal("59.94 fps", stats.FormattedFps);
    }

    [Fact]
    public void PlaybackStatistics_FormatsFpsAndCache_WithDrops()
    {
        var stats = new PlaybackStatistics
        {
            Fps = 23.976,
            DroppedFrames = 4,
            CacheFillPercent = 85.5,
            CacheTime = TimeSpan.FromSeconds(12.3)
        };

        Assert.Equal("23.98 fps (4 dropped)", stats.FormattedFps);
        Assert.Equal("85.5% (12.3s ahead)", stats.FormattedCache);
    }
}
