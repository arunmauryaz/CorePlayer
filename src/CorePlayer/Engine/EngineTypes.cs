namespace CorePlayer.Engine;

public enum HardwareDecodeMode
{
    AutoSafe,
    D3D11VA,
    D3D11VACopy,
    NVDec,
    VAAPI,
    Software
}

public enum QualityProfile
{
    Ultra,
    Balanced,
    BatterySaver
}

public enum SeekMode
{
    Exact,
    FastKeyframe
}

public enum VideoRotation
{
    Normal = 0,
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270
}

public class EngineOptions
{
    public HardwareDecodeMode HardwareMode { get; set; } = HardwareDecodeMode.AutoSafe;
    public QualityProfile Quality { get; set; } = QualityProfile.Balanced;
    public long DemuxerMaxBytes { get; set; } = 150 * 1024 * 1024; // 150 MiB
    public long DemuxerMaxBackBytes { get; set; } = 50 * 1024 * 1024; // 50 MiB
    public double ReadaheadSeconds { get; set; } = 20.0;
    public bool AudioPitchCorrection { get; set; } = true;
    public bool EnableHdrTonemapping { get; set; } = true;
    public bool EnableDebanding { get; set; } = false;
    public bool AudioNormalization { get; set; } = false;
    public string SubtitleFont { get; set; } = "Segoe UI";
    public int SubtitleFontSize { get; set; } = 48;
}
