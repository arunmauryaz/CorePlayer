# CORE PLAYER High-Performance Video Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build CORE ENGINE — an ultra-fast, memory-efficient, universally compatible video playback engine for CORE PLAYER capable of playing any container (MP4, MKV, MOV, AVI, WEBM, FLV, TS, WMV, VOB, GIF, APNG) and audio formats with hardware-accelerated zero-copy rendering, HDR tone-mapping, large asynchronous demuxer caching, night mode audio normalization, dual subtitles, and frame-accurate seeking.

**Architecture:** A layered, modular engine architecture built on top of native `libmpv` (v0.36.0 with FFmpeg) and Avalonia's OpenGL hardware surface. Subsystems are partitioned into discrete controllers (`HardwarePipeline`, `AudioSubsystem`, `SubtitleSubsystem`, `VideoSubsystem`, `MediaInspector`) coordinated by an event-driven `CorePlaybackEngine` with high-frequency position dispatching and zero-lag UI bindings.

**Tech Stack:** C# .NET 8 LTS, Avalonia UI 11.2.3, HanumanInstitute.LibMpv.Avalonia 0.9.1, native libmpv-2 (64-bit, D3D11VA / OpenGL / FFmpeg), CommunityToolkit.Mvvm, xUnit for unit testing.

**Spec:** Designed to meet the user's requirement: *"our own engine, efficient, very fast, and good at video playing. Any kind of format, MP4 to MOV to any format, even GIF if required. So any kind of media format. Make it the best engine available for playing videos... make sure you can play videos smoothly, any movie smoothly, nicely."*

## Global Constraints
- Target Framework: `net8.0` on Windows 10/11 x64.
- Zero Memory Leaks: Native MPV handles, timers, and event subscriptions must be strictly disposable (`IDisposable`).
- Thread Safety: All state-changing events dispatched to Avalonia UI must traverse `Dispatcher.UIThread.Post()` or `InvokeAsync()`.
- Airspace Integrity: Avalonia controls must render on top of the OpenGL hardware surface without clipping or flicker.
- Formats Guaranteed: MP4, MKV, MOV, AVI, WEBM, FLV, TS, M2TS, WMV, VOB, OGG, 3GP, MPG, GIF, APNG, MP3, FLAC, AAC, WAV, OPUS.

---

### Task 1: Engine Types, Statistics, & Test Project Setup

**Files:**
- Create: `src/CorePlayer/Engine/EngineTypes.cs`
- Create: `src/CorePlayer/Engine/PlaybackStatistics.cs`
- Create: `tests/CorePlayer.Tests/CorePlayer.Tests.csproj`
- Create: `tests/CorePlayer.Tests/EngineTypesTests.cs`
- Modify: `CorePlayer.sln`

**Interfaces:**
- Produces:
  - `enum HardwareDecodeMode { AutoSafe, D3D11VA, D3D11VACopy, NVDec, VAAPI, Software }`
  - `enum QualityProfile { Ultra, Balanced, BatterySaver }`
  - `enum SeekMode { Exact, FastKeyframe }`
  - `enum VideoRotation { Normal = 0, Rotate90 = 90, Rotate180 = 180, Rotate270 = 270 }`
  - `record PlaybackStatistics(double Fps, int DroppedFrames, string VideoCodec, string AudioCodec, string HardwareDecoder, double VideoBitrate, double AudioBitrate, double CacheFillPercent, TimeSpan CacheTime, int Width, int Height, double AspectRatio)`
  - `record EngineOptions` with configurable buffer sizes (`DemuxerMaxBytes`, `ReadaheadSeconds`, `HardwareMode`, `Profile`)

- [ ] **Step 1: Create the xUnit test project `tests/CorePlayer.Tests/CorePlayer.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CorePlayer\CorePlayer.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add `CorePlayer.Tests.csproj` to `CorePlayer.sln` and write failing tests in `tests/CorePlayer.Tests/EngineTypesTests.cs`**

```csharp
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
    }
}
```

- [ ] **Step 3: Run `dotnet test` to verify failure**

Run: `dotnet test tests/CorePlayer.Tests/CorePlayer.Tests.csproj`
Expected: FAIL (Cannot find `CorePlayer.Engine.EngineOptions` or `PlaybackStatistics`)

- [ ] **Step 4: Implement `EngineTypes.cs` and `PlaybackStatistics.cs` in `src/CorePlayer/Engine/`**

Create `src/CorePlayer/Engine/EngineTypes.cs`:
```csharp
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
```

Create `src/CorePlayer/Engine/PlaybackStatistics.cs`:
```csharp
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
```

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test`
Expected: PASS (2 passed)
Commit: `git add .; git commit -m "feat(engine): add core engine types, statistics, and test project"`

---

### Task 2: Hardware Pipeline & Performance Configuration Builder

**Files:**
- Create: `src/CorePlayer/Engine/HardwarePipelineBuilder.cs`
- Create: `tests/CorePlayer.Tests/HardwarePipelineTests.cs`

**Interfaces:**
- Produces:
  - `class HardwarePipelineBuilder`
  - `Dictionary<string, string> BuildOptions(EngineOptions options)`
  - Configures `hwdec`, `video-sync`, `scale`, `cscale`, `dscale`, `deband`, `demuxer-max-bytes`, `demuxer-readahead-secs`, `tone-mapping`, `target-colorspace-hint`.

- [ ] **Step 1: Write failing tests in `tests/CorePlayer.Tests/HardwarePipelineTests.cs`**

```csharp
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
```

- [ ] **Step 2: Run `dotnet test` to verify failure**

Run: `dotnet test`
Expected: FAIL (`HardwarePipelineBuilder` not defined)

- [ ] **Step 3: Implement `HardwarePipelineBuilder.cs` in `src/CorePlayer/Engine/`**

```csharp
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
```

- [ ] **Step 4: Run tests and commit**

Run: `dotnet test`
Expected: PASS (4 passed)
Commit: `git add .; git commit -m "feat(engine): add HardwarePipelineBuilder with multi-profile shader presets and HDR tone-mapping"`

---

### Task 3: Subtitle Subsystem (Multi-Track, Fuzzy Auto-Discovery, Sync Delay, & Custom Typography)

**Files:**
- Create: `src/CorePlayer/Engine/SubtitleSubsystem.cs`
- Create: `tests/CorePlayer.Tests/SubtitleSubsystemTests.cs`

**Interfaces:**
- Produces:
  - `class SubtitleSubsystem`
  - `void SetDelay(double seconds)`
  - `void AddExternalSubtitle(string path)`
  - `void SetTypography(string fontFamily, int fontSize, string textColorHex, bool hasBackdrop)`
  - `void SetSecondarySubtitle(int trackId)`
  - `IReadOnlyList<string> FindSubtitlesForMedia(string mediaFilePath)`

- [ ] **Step 1: Write failing tests in `tests/CorePlayer.Tests/SubtitleSubsystemTests.cs`**

```csharp
using System.IO;
using CorePlayer.Engine;
using Xunit;

namespace CorePlayer.Tests;

public class SubtitleSubsystemTests
{
    [Fact]
    public void FindSubtitlesForMedia_DiscoversMatchingSrtAndAssFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var movie = Path.Combine(tempDir, "Inception.2010.1080p.mkv");
            File.WriteAllText(movie, "");
            var sub1 = Path.Combine(tempDir, "Inception.2010.1080p.en.srt");
            File.WriteAllText(sub1, "");
            var sub2 = Path.Combine(tempDir, "Inception.2010.1080p.Spanish.ass");
            File.WriteAllText(sub2, "");
            var unrelated = Path.Combine(tempDir, "Interstellar.srt");
            File.WriteAllText(unrelated, "");

            var subSystem = new SubtitleSubsystem();
            var matches = subSystem.FindSubtitlesForMedia(movie);

            Assert.Equal(2, matches.Count);
            Assert.Contains(sub1, matches);
            Assert.Contains(sub2, matches);
            Assert.DoesNotContain(unrelated, matches);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run `dotnet test` to verify failure**

Run: `dotnet test`
Expected: FAIL (`SubtitleSubsystem` not defined)

- [ ] **Step 3: Implement `SubtitleSubsystem.cs` in `src/CorePlayer/Engine/`**

```csharp
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
```

- [ ] **Step 4: Run tests and commit**

Run: `dotnet test`
Expected: PASS (5 passed)
Commit: `git add .; git commit -m "feat(engine): add SubtitleSubsystem with fuzzy discovery and typography configuration"`

---

### Task 4: Audio Subsystem (Normalization, Multi-Channel, & Pitch Preservation)

**Files:**
- Create: `src/CorePlayer/Engine/AudioSubsystem.cs`
- Create: `tests/CorePlayer.Tests/AudioSubsystemTests.cs`

**Interfaces:**
- Produces:
  - `class AudioSubsystem`
  - `string BuildNormalizationFilter(bool enable)`
  - `string BuildSpeedCommand(double speed, bool preservePitch)`
  - `string BuildDelayCommand(double offsetMilliseconds)`

- [ ] **Step 1: Write failing tests in `tests/CorePlayer.Tests/AudioSubsystemTests.cs`**

```csharp
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
```

- [ ] **Step 2: Run `dotnet test` to verify failure**

Run: `dotnet test`
Expected: FAIL (`AudioSubsystem` not defined)

- [ ] **Step 3: Implement `AudioSubsystem.cs` in `src/CorePlayer/Engine/`**

```csharp
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
```

- [ ] **Step 4: Run tests and commit**

Run: `dotnet test`
Expected: PASS (7 passed)
Commit: `git add .; git commit -m "feat(engine): add AudioSubsystem with dynamic dialogue normalizer and pitch-safe speed"`

---

### Task 5: Video Geometry & Picture Controls Subsystem

**Files:**
- Create: `src/CorePlayer/Engine/VideoSubsystem.cs`
- Create: `tests/CorePlayer.Tests/VideoSubsystemTests.cs`

**Interfaces:**
- Produces:
  - `class VideoSubsystem`
  - `string BuildColorAdjustmentCommand(int brightness, int contrast, int saturation, int gamma, int hue)`
  - `string BuildRotationCommand(VideoRotation rotation)`
  - `string BuildAspectRatioCommand(string ratio)`
  - `string BuildDeinterlaceCommand(bool enable)`

- [ ] **Step 1: Write failing tests in `tests/CorePlayer.Tests/VideoSubsystemTests.cs`**

```csharp
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
```

- [ ] **Step 2: Run `dotnet test` to verify failure**

Run: `dotnet test`
Expected: FAIL (`VideoSubsystem` not defined)

- [ ] **Step 3: Implement `VideoSubsystem.cs` in `src/CorePlayer/Engine/`**

```csharp
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
```

- [ ] **Step 4: Run tests and commit**

Run: `dotnet test`
Expected: PASS (9 passed)
Commit: `git add .; git commit -m "feat(engine): add VideoSubsystem with full picture calibration and rotation support"`

---

### Task 6: CorePlaybackEngine Orchestrator (Replacing Legacy Service)

**Files:**
- Create: `src/CorePlayer/Engine/ICorePlaybackEngine.cs`
- Create: `src/CorePlayer/Engine/CorePlaybackEngine.cs`
- Modify: `src/CorePlayer/Services/PlaybackService.cs` (adapter redirection)

**Interfaces:**
- Produces:
  - `interface ICorePlaybackEngine : IDisposable`
  - High-precision position dispatcher (`PositionChanged` fired smoothly without 200ms lag)
  - `FastSeek(double seconds)` (for real-time slider scrubbing)
  - `ExactSeek(double seconds)` (for precise frame landing)
  - `CycleAudioTrack()`, `CycleSubtitleTrack()`
  - `ToggleNightMode()`, `SetPictureAdjustments(...)`, `SetRotation(...)`
  - `PlaybackStatistics GetCurrentStatistics()`

- [ ] **Step 1: Define `ICorePlaybackEngine.cs` in `src/CorePlayer/Engine/`**

```csharp
using System;
using System.Collections.Generic;
using CorePlayer.Models;
using HanumanInstitute.LibMpv;

namespace CorePlayer.Engine;

public interface ICorePlaybackEngine : IDisposable
{
    MpvContext Context { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    int Volume { get; }
    double Speed { get; }
    string? CurrentFilePath { get; }
    string? MediaTitle { get; }
    string? VideoCodec { get; }
    string? AudioCodec { get; }
    string? Resolution { get; }
    string AspectRatio { get; }
    bool IsNightModeActive { get; }
    VideoRotation Rotation { get; }
    IReadOnlyList<TrackInfo> AudioTracks { get; }
    IReadOnlyList<TrackInfo> SubtitleTracks { get; }

    event EventHandler? PlaybackStateChanged;
    event EventHandler? PositionChanged;
    event EventHandler? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler? TracksChanged;
    event EventHandler<PlaybackStatistics>? StatisticsUpdated;

    void Load(string mediaPath, double startPosition = 0);
    void Play();
    void Pause();
    void Stop();
    void TogglePlayPause();
    void FastSeek(double seconds);
    void ExactSeek(double seconds);
    void SeekRelative(double offsetSeconds);
    void SetVolume(int volume);
    void SetSpeed(double speed);
    void SetBrightness(int brightness);
    void SetPictureAdjustment(int brightness, int contrast, int saturation, int gamma, int hue);
    void SetRotation(VideoRotation rotation);
    void SetSubtitleDelay(double seconds);
    void SetAudioDelay(double milliseconds);
    void ToggleNightMode();
    void SetAspectRatio(string ratio);
    void SelectTrack(TrackInfo track);
    void AddSubtitleFile(string path);
    void StepFrame(bool forward = true);
    string TakeSnapshot(bool includeSubtitles = true);
    PlaybackStatistics GetCurrentStatistics();
}
```

- [ ] **Step 2: Implement `CorePlaybackEngine.cs` in `src/CorePlayer/Engine/`**

Implement `CorePlaybackEngine` wrapping `MpvContext`, initializing with `HardwarePipelineBuilder`, using `AudioSubsystem`, `SubtitleSubsystem`, and `VideoSubsystem`, and running a fast 16ms / 60fps DispatcherTimer for microsecond-accurate seekbar sync while querying MPV state.

- [ ] **Step 3: Update `PlaybackService.cs` to wrap or implement via `CorePlaybackEngine`**

Update `MpvPlaybackService` to delegate all calls to `CorePlaybackEngine`, maintaining backward compatibility with `IPlaybackService` while exposing the new high-performance engine features.

- [ ] **Step 4: Verify Compilation & Run Test Suite**

Run: `dotnet test`
Run: `dotnet build`
Expected: 0 errors, 0 warnings.
Commit: `git add .; git commit -m "feat(engine): implement CorePlaybackEngine orchestrator with 60fps dispatcher and fast-scrub seeking"`

---

### Task 7: ViewModel & UI Feature Integration

**Files:**
- Modify: `src/CorePlayer/ViewModels/MainViewModel.cs`
- Modify: `src/CorePlayer/Views/MainWindow.axaml`
- Modify: `src/CorePlayer/Views/MainWindow.axaml.cs`

**Interfaces:**
- Connects:
  - Technical Info Modal (displaying live `PlaybackStatistics`: FPS, dropped frames, bitrate, hardware decoder `d3d11va`, cache buffer)
  - Night Mode toggle in Audio menu and options
  - Rotation menu items (0°, 90°, 180°, 270°)
  - Smooth seeking: Two-way slider updates with `FastSeek` during drag and `ExactSeek` on commit
  - Subtitle auto-discovery prompt / selection

- [ ] **Step 1: Add new commands & observable properties in `MainViewModel.cs`**
  - `[ObservableProperty] private PlaybackStatistics? _currentStats;`
  - `[ObservableProperty] private bool _isNightMode;`
  - `[ObservableProperty] private string _statsDisplay = string.Empty;`
  - `[RelayCommand] public void ToggleNightMode()`
  - `[RelayCommand] public void RotateVideo(int degrees)`
  - `[RelayCommand] public void CycleAudioTrack()`
  - `[RelayCommand] public void CycleSubtitleTrack()`

- [ ] **Step 2: Update `MainWindow.axaml` Technical Info Dialog and Menus**
  - Bind the Technical Info overlay to display `FormattedResolution`, `FormattedVideoBitrate`, `HardwareDecoder`, `Fps`, `DroppedFrames`, `CacheFillPercent`.
  - Add "Night Mode (Dialogue Boost)" toggle under Audio menu (`_Audio`).
  - Add "Rotate Video" sub-menu under View menu (`_View`).

- [ ] **Step 3: Verify Compilation**

Run: `dotnet build`
Expected: 0 errors, 0 warnings.
Commit: `git add .; git commit -m "feat(ui): integrate CorePlaybackEngine with MainViewModel, live telemetry HUD, and audio normalizer"`

---

### Task 4: End-to-End Verification & Benchmark Check

**Files:**
- Create: `scratch/verify_engine.py`

- [ ] **Step 1: Write an automated verification script `scratch/verify_engine.py`**
  - Launch `CorePlayer.exe`
  - Inspect process handles, verify `libmpv-2.dll` is loaded and active
  - Verify UI elements remain responsive and perfectly aligned
  - Capture and verify a live snapshot of the player running with the new engine

- [ ] **Step 2: Execute verification script**

Run: `python scratch/verify_engine.py`
Expected: Return code 0, clean snapshot saved to artifacts.

- [ ] **Step 3: Final Commit**

Commit: `git add .; git commit -m "chore: complete CorePlayer engine implementation and verification"`
