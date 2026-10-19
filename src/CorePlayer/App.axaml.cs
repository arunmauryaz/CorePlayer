using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CorePlayer.ViewModels;
using CorePlayer.Views;

namespace CorePlayer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var window = new MainWindow
            {
                DataContext = vm,
            };
            desktop.MainWindow = window;

            if (desktop.Args != null && desktop.Args.Length > 0 && !string.IsNullOrWhiteSpace(desktop.Args[0]))
            {
                var targetFile = desktop.Args[0];
                window.Opened += (s, e) =>
                {
                    try
                    {
                        System.IO.File.AppendAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Opening file from args: {targetFile}\n");
                        vm.OpenFile(targetFile);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.AppendAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Error in vm.OpenFile: {ex}\n");
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}