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
}
