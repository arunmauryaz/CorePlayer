using System;
using System.IO;
using System.Runtime.InteropServices;
using CorePlayer.Engine;
using HanumanInstitute.LibMpv;
using Xunit;

namespace CorePlayer.Tests;

public class PlaybackEngineTests
{
    static PlaybackEngineTests()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NativeLibrary.SetDllImportResolver(typeof(MpvContext).Assembly, (name, assembly, path) =>
            {
                if (name.Contains("mpv", StringComparison.OrdinalIgnoreCase))
                {
                    var baseDll = Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll");
                    if (File.Exists(baseDll)) return NativeLibrary.Load(baseDll);
                    var srcDll = @"d:\CORE PLAYER\src\CorePlayer\bin\Debug\net8.0\libmpv-2.dll";
                    if (File.Exists(srcDll)) return NativeLibrary.Load(srcDll);
                }
                return IntPtr.Zero;
            });
        }
    }

    [Fact]
    public void RunCommand_WithDirectPath_DoesNotFailOnWindowsBackslashes()
    {
        using var context = new MpvContext();
        var testPath = @"d:\CORE PLAYER\test_video.mp4";

        var exCommand = Record.Exception(() =>
        {
            context.RunCommand(default, "loadfile", testPath);
        });
        Assert.Null(exCommand);
    }

    [Fact]
    public void MpvProperty_ThrowsPropertyUnavailable_WhenNotPlaying()
    {
        using var context = new MpvContext();
        var ex = Record.Exception(() => context.TimePos.Get());
        Assert.NotNull(ex);
        Assert.IsType<MpvException>(ex);
        Assert.Contains("property unavailable", ex.Message);
    }

    [Fact]
    public void CorePlaybackEngine_SafeGet_ProtectsAgainstPropertyUnavailable()
    {
        using var engine = new CorePlaybackEngine();
        Assert.Null(engine.Resolution);
        Assert.Null(engine.VideoCodec);
        Assert.Null(engine.AudioCodec);
        Assert.False(engine.IsPlaying);
        Assert.Equal(TimeSpan.Zero, engine.Position);
        Assert.Equal(TimeSpan.Zero, engine.Duration);
    }

    [Fact]
    public void CorePlaybackEngine_LoadVideo_StartsPlayback()
    {
        var testPath = @"d:\CORE PLAYER\test_video.mp4";
        if (!File.Exists(testPath)) return;

        using var engine = new CorePlaybackEngine();
        engine.Load(testPath);
        System.Threading.Thread.Sleep(1000);

        Assert.NotNull(engine.CurrentFilePath);
        Assert.True(engine.Duration.TotalSeconds > 0 || engine.IsPlaying);
    }

    [Fact]
    public void CorePlaybackEngine_AttachContext_WorksSeamlessly()
    {
        using var externalContext = new MpvContext();
        using var engine = new CorePlaybackEngine(externalContext);
        var testPath = @"d:\CORE PLAYER\test_video.mp4";

        engine.Load(testPath);
        Assert.Equal(testPath, engine.CurrentFilePath);

        using var secondContext = new MpvContext();
        var exAttach = Record.Exception(() => engine.AttachContext(secondContext));
        Assert.Null(exAttach);
    }

    [Fact]
    public void CorePlaybackEngine_VideoControls_SeekRotateAndPictureAdjustments()
    {
        using var engine = new CorePlaybackEngine();
        var testPath = @"d:\CORE PLAYER\test_video.mp4";
        engine.Load(testPath);

        // Test seeking
        var exExact = Record.Exception(() => engine.ExactSeek(2.0));
        Assert.Null(exExact);

        var exFast = Record.Exception(() => engine.FastSeek(1.0));
        Assert.Null(exFast);

        // Test rotation
        var exRot = Record.Exception(() => engine.SetRotation(VideoRotation.Rotate90));
        Assert.Null(exRot);
        Assert.Equal(VideoRotation.Rotate90, engine.Rotation);

        // Test picture adjustments
        var exPic = Record.Exception(() => engine.SetPictureAdjustment(10, 5, -5, 0, 0));
        Assert.Null(exPic);
        Assert.Equal(10, engine.Brightness);

        // Test aspect ratio
        var exAspect = Record.Exception(() => engine.SetAspectRatio("16:9"));
        Assert.Null(exAspect);
        Assert.Equal("16:9", engine.AspectRatio);
    }

    [Fact]
    public void CorePlaybackEngine_AudioControls_NightModeAndSpeed()
    {
        using var engine = new CorePlaybackEngine();
        var testPath = @"d:\CORE PLAYER\test_video.mp4";
        engine.Load(testPath);

        // Test speed
        engine.SetSpeed(1.5);
        Assert.Equal(1.5, engine.Speed);

        // Test volume
        engine.SetVolume(120);
        Assert.Equal(120, engine.Volume);

        // Test night mode
        Assert.False(engine.IsNightModeActive);
        engine.ToggleNightMode();
        Assert.True(engine.IsNightModeActive);
        engine.ToggleNightMode();
        Assert.False(engine.IsNightModeActive);
    }
}
