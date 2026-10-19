using System;
using System.Globalization;

namespace CorePlayer.Engine;

public class AudioSubsystem
{
    public string BuildNormalizationFilter(bool enable)
    {
        // Dynamic audio normalizer: smooths out huge peaks (explosions) and boosts quiet dialogue
        return enable ? "lavfi=[dynaudnorm=f=250:g=31:p=0.5:m=10.0:r=0.9]" : "no";
    }

    public string BuildSpeedCommand(double speed, bool preservePitch)
    {
        speed = Math.Clamp(speed, 0.1, 4.0);
        var speedStr = speed.ToString(CultureInfo.InvariantCulture);
        var pitchStr = preservePitch ? "yes" : "no";
        return $"set speed {speedStr}; set audio-pitch-correction {pitchStr}";
    }

    public string BuildDelayCommand(double offsetMilliseconds)
    {
        var seconds = offsetMilliseconds / 1000.0;
        return $"set audio-delay {seconds.ToString(CultureInfo.InvariantCulture)}";
    }
}
