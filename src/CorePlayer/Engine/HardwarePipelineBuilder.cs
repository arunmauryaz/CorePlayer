using System.Collections.Generic;

namespace CorePlayer.Engine;

public class HardwarePipelineBuilder
{
    public Dictionary<string, string> BuildOptions(EngineOptions options)
    {
        var dict = new Dictionary<string, string>
        {
            ["keep-open"] = "yes",
            ["sub-auto"] = "fuzzy",
            ["osd-bar"] = "no",
            ["osd-on-seek"] = "no",
            ["osd-level"] = "0",
            ["audio-pitch-correction"] = options.AudioPitchCorrection ? "yes" : "no",
            ["demuxer-max-bytes"] = options.DemuxerMaxBytes.ToString(),
            ["demuxer-max-back-bytes"] = options.DemuxerMaxBackBytes.ToString(),
            ["demuxer-readahead-secs"] = options.ReadaheadSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["hwdec"] = options.HardwareMode switch
            {
                HardwareDecodeMode.D3D11VA => "d3d11va",
                HardwareDecodeMode.D3D11VACopy => "d3d11va-copy",
                HardwareDecodeMode.NVDec => "nvdec",
                HardwareDecodeMode.VAAPI => "vaapi",
                HardwareDecodeMode.Software => "no",
                _ => "auto-safe"
            }
        };

        // Quality Profile Shaders
        switch (options.Quality)
        {
            case QualityProfile.Ultra:
                dict["scale"] = "spline36";
                dict["cscale"] = "spline36";
                dict["dscale"] = "mitchell";
                dict["deband"] = "yes";
                dict["dither-depth"] = "auto";
                dict["correct-downscaling"] = "yes";
                dict["linear-downscaling"] = "yes";
                break;

            case QualityProfile.BatterySaver:
                dict["scale"] = "bilinear";
                dict["cscale"] = "bilinear";
                dict["dscale"] = "bilinear";
                dict["deband"] = "no";
                dict["dither-depth"] = "no";
                break;

            case QualityProfile.Balanced:
            default:
                dict["scale"] = "spline36";
                dict["cscale"] = "bilinear";
                dict["dscale"] = "mitchell";
                dict["deband"] = options.EnableDebanding ? "yes" : "no";
                dict["dither-depth"] = "auto";
                break;
        }

        // HDR & Color Pipeline
        if (options.EnableHdrTonemapping)
        {
            dict["tone-mapping"] = "bt.2446a";
            dict["target-colorspace-hint"] = "yes";
        }

        // Subtitle defaults
        dict["sub-font"] = options.SubtitleFont;
        dict["sub-font-size"] = options.SubtitleFontSize.ToString();
        dict["sub-border-size"] = "2.5";
        dict["sub-shadow-offset"] = "1.5";

        return dict;
    }
}
