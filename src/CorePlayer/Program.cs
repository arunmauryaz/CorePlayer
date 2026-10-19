using Avalonia;
using Avalonia.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CorePlayer;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure libmpv native dll is resolved from runtimes/win-x64/native
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            NativeLibrary.SetDllImportResolver(typeof(HanumanInstitute.LibMpv.MpvContext).Assembly, (name, assembly, path) =>
            {
                if (name.Contains("mpv", StringComparison.OrdinalIgnoreCase))
                {
                    var nativeSubdir = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "libmpv-2.dll");
                    if (File.Exists(nativeSubdir))
                    {
                        return NativeLibrary.Load(nativeSubdir);
                    }
                    var baseDll = Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll");
                    if (File.Exists(baseDll))
                    {
                        return NativeLibrary.Load(baseDll);
                    }
                }
                return IntPtr.Zero;
            });
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
            });
        }

        return builder;
    }
}
