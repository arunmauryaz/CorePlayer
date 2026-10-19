using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class AudioSubsystemTests
{
    [Fact]
    public void BuildNormalizationFilter_ReturnsDynaudnormFilterWhenEnabled()
    {
        var audio = new AudioSubsystem();
        var filter = audio.BuildNormalizationFilter(true);

        Assert.Contains("dynaudnorm", filter);
        Assert.Equal("no", audio.BuildNormalizationFilter(false));
    }

    [Fact]
    public void BuildSpeedCommand_PreservesPitchUsingScaleTempo2()
    {
        var audio = new AudioSubsystem();
        var cmd = audio.BuildSpeedCommand(1.5, preservePitch: true);

        Assert.Contains("set speed 1.5", cmd);
        Assert.Contains("audio-pitch-correction yes", cmd);
    }
}
