using System;

namespace CorePlayer.Engine;

public class VideoSubsystem
{
    public string BuildColorAdjustmentCommand(int brightness, int contrast, int saturation, int gamma, int hue)
    {
        brightness = Math.Clamp(brightness, -100, 100);
        contrast = Math.Clamp(contrast, -100, 100);
        saturation = Math.Clamp(saturation, -100, 100);
        gamma = Math.Clamp(gamma, -100, 100);
        hue = Math.Clamp(hue, -100, 100);

        return $"set brightness {brightness}; set contrast {contrast}; set saturation {saturation}; set gamma {gamma}; set hue {hue}";
    }

    public string BuildRotationCommand(VideoRotation rotation)
    {
        return $"set video-rotate {(int)rotation}";
    }

    public string BuildAspectRatioCommand(string ratio)
    {
        return $"set video-aspect-override \"{ratio}\"";
    }

    public string BuildDeinterlaceCommand(bool enable)
    {
        return $"set deinterlace {(enable ? "yes" : "no")}";
    }
}
