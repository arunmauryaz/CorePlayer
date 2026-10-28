using Avalonia;
using Avalonia.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CorePlayer;

public sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure libmpv native binary is resolved across Windows, Linux, and macOS
        NativeLibrary.SetDllImportResolver(typeof(HanumanInstitute.LibMpv.MpvContext).Assembly, (name, assembly, path) =>
        {
            if (name.Contains("mpv", StringComparison.OrdinalIgnoreCase))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string[] winCandidates =
                    [
                        Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll"),
                        Path.Combine(AppContext.BaseDirectory, "mpv-2.dll"),
                        Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "libmpv-2.dll")
                    ];
                    foreach (var c in winCandidates)
                    {
                        if (File.Exists(c)) return NativeLibrary.Load(c);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string[] macCandidates =
                    [
                        Path.Combine(AppContext.BaseDirectory, "libmpv.2.dylib"),
                        Path.Combine(AppContext.BaseDirectory, "libmpv.dylib"),
                        "/opt/homebrew/lib/libmpv.2.dylib",
                        "/opt/homebrew/lib/libmpv.dylib",
                        "/usr/local/lib/libmpv.2.dylib",
                        "/usr/local/lib/libmpv.dylib"
                    ];
                    foreach (var c in macCandidates)
                    {
                        if (File.Exists(c)) return NativeLibrary.Load(c);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    string[] linuxCandidates =
                    [
                        Path.Combine(AppContext.BaseDirectory, "libmpv.so.2"),
                        Path.Combine(AppContext.BaseDirectory, "libmpv.so.1"),
                        Path.Combine(AppContext.BaseDirectory, "libmpv.so"),
                        "/usr/lib/x86_64-linux-gnu/libmpv.so.2",
                        "/usr/lib/x86_64-linux-gnu/libmpv.so.1",
                        "/usr/lib/x86_64-linux-gnu/libmpv.so",
                        "/usr/lib/libmpv.so.2",
                        "/usr/lib/libmpv.so.1",
                        "/usr/lib/libmpv.so",
                        "/usr/lib64/libmpv.so.2",
                        "/usr/lib64/libmpv.so.1",
                        "/usr/lib64/libmpv.so"
                    ];
                    foreach (var c in linuxCandidates)
                    {
                        if (File.Exists(c)) return NativeLibrary.Load(c);
                    }
                }
            }
            return IntPtr.Zero;
        });

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED DOMAIN EXCEPTION:\n{e.ExceptionObject}\nIsTerminating: {e.IsTerminating}\n";
            File.AppendAllText("crash.log", msg);
            Console.Error.WriteLine(msg);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNOBSERVED TASK EXCEPTION:\n{e.Exception}\n";
            File.AppendAllText("crash.log", msg);
            Console.Error.WriteLine(msg);
        };

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
                RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software]
            });
        }

        return builder;
    }
}
