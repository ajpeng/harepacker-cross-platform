using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HaRepacker.GUI;

namespace HaRepacker
{
    public class App : Application
    {
        private static string? _wzToLoad;
        private static bool _firstRun;

        public static void InitializeStartupArgs(string? wzToLoad, bool firstRun)
        {
            _wzToLoad = wzToLoad;
            _firstRun = firstRun;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow(_wzToLoad, _firstRun);
                desktop.Exit += (_, _) => Program.EndApplication();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
