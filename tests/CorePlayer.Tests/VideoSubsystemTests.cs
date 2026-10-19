using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class VideoSubsystemTests
{
    [Fact]
    public void BuildColorAdjustmentCommand_ClampsValuesAndGeneratesProperties()
    {
        var video = new VideoSubsystem();
        var cmd = video.BuildColorAdjustmentCommand(brightness: 15, contrast: -10, saturation: 20, gamma: 5, hue: 0);

        Assert.Contains("set brightness 15", cmd);
        Assert.Contains("set contrast -10", cmd);
        Assert.Contains("set saturation 20", cmd);
        Assert.Contains("set gamma 5", cmd);
        Assert.Contains("set hue 0", cmd);
    }

    [Fact]
    public void BuildRotationCommand_SetsVideoRotateProperty()
    {
        var video = new VideoSubsystem();
        var cmd = video.BuildRotationCommand(VideoRotation.Rotate90);

        Assert.Equal("set video-rotate 90", cmd);
    }
}
