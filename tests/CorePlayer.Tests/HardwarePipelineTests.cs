using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class HardwarePipelineTests
{
    [Fact]
    public void BuildOptions_AutoSafe_GeneratesCorrectMpvProperties()
    {
        var options = new EngineOptions
        {
            HardwareMode = HardwareDecodeMode.AutoSafe,
            Quality = QualityProfile.Ultra,
            DemuxerMaxBytes = 200 * 1024 * 1024
        };

        var builder = new HardwarePipelineBuilder();
        var mpvOptions = builder.BuildOptions(options);

        Assert.Equal("auto-safe", mpvOptions["hwdec"]);
        Assert.Equal("spline36", mpvOptions["scale"]);
        Assert.Equal("yes", mpvOptions["deband"]);
        Assert.Equal("yes", mpvOptions["keep-open"]);
        Assert.Equal("209715200", mpvOptions["demuxer-max-bytes"]);
        Assert.Equal("yes", mpvOptions["audio-pitch-correction"]);
    }

    [Fact]
    public void BuildOptions_BatterySaver_OptimizesForPowerEfficiency()
    {
        var options = new EngineOptions
        {
            Quality = QualityProfile.BatterySaver
        };

        var builder = new HardwarePipelineBuilder();
        var mpvOptions = builder.BuildOptions(options);

        Assert.Equal("bilinear", mpvOptions["scale"]);
        Assert.Equal("no", mpvOptions["deband"]);
    }
}
