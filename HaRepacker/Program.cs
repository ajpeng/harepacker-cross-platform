using Avalonia;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using MapleLib;
using MapleLib.Configuration;

namespace HaRepacker
{
    public static class Program
    {
        private static WzFileManager? _wzFileManager;
        public static WzFileManager? WzFileManager
        {
            get => _wzFileManager;
            set => _wzFileManager = value;
        }

        private static ConfigurationManager? _configurationManager;
        public static ConfigurationManager? ConfigurationManager => _configurationManager;

        public const string AppName = "HaRepacker";

        [STAThread]
        public static void Main(string[] args)
        {
            CultureInfo ci = GetMainCulture(CultureInfo.CurrentCulture);
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;

            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 3, Environment.ProcessorCount * 3);

            bool firstRun = PrepareApplication();
            string? wzToLoad = args.Length > 0 ? args[0] : null;

            BuildAvaloniaApp(wzToLoad, firstRun).StartWithClassicDesktopLifetime(args);
            EndApplication();
        }

        public static AppBuilder BuildAvaloniaApp(string? wzToLoad = null, bool firstRun = false)
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .AfterSetup(_ => App.InitializeStartupArgs(wzToLoad, firstRun));

        private static CultureInfo GetMainCulture(CultureInfo ci)
        {
            var hyphen = ci.Name.IndexOf('-');
            if (hyphen < 0) return ci;
            return ci.Name.AsSpan(0, hyphen) switch
            {
                "ko" => new CultureInfo("ko"),
                "ja" => new CultureInfo("ja"),
                "en" => new CultureInfo("en"),
                "zh" => ci.ThreeLetterWindowsLanguageName == "CHS"
                    ? new CultureInfo("zh-CHS")
                    : new CultureInfo("zh-CHT"),
                _ => ci
            };
        }

        public static bool PrepareApplication()
        {
            _configurationManager = new ConfigurationManager();
            bool loaded = _configurationManager.Load();
            if (!loaded) return true;

            bool firstRun = _configurationManager.ApplicationSettings.FirstRun;
            if (firstRun)
            {
                _configurationManager.ApplicationSettings.FirstRun = false;
                _configurationManager.Save();
            }
            return firstRun;
        }

        public static void EndApplication()
        {
            _wzFileManager?.Dispose();
            _configurationManager?.Save();
        }

        public static string GetLocalFolderPath()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appdata, AppName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
