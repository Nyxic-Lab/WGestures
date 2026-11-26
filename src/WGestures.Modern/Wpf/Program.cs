using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WGestures.Modern.Core;

namespace WGestures.Modern;

internal static class Program
{
    private static string _log = Path.Combine(EngineSettings.DataDirectory, "ui-status.log");

    internal static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_log)!);
            if (File.Exists(_log) && new FileInfo(_log).Length > 1024 * 1024)
                File.Delete(_log);
            File.AppendAllText(_log, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        bool smoke = args.Length == 2 && (args[0] == "--smoke-ui" || args[0] == "--smoke-ui-compat");
        bool forceCompatibility = args.Contains("--compatibility", StringComparer.OrdinalIgnoreCase) ||
            (smoke && args[0] == "--smoke-ui-compat");
        if (smoke) _log = Path.GetFullPath(args[1]);
        try
        {
            Log("WPF settings startup");
            Theme.Configure(forceCompatibility);
            if (!Theme.Profile.MeetsMinimum)
                throw new PlatformNotSupportedException("Nyxic Gestures requires Windows 10 version 1607 (build 14393) or later.");
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) =>
            {
                Log("Dispatcher error: " + e.Exception);
                e.Handled = true;
                app.Shutdown(1);
            };
            var settings = new EngineSettings();
            if (!smoke)
            {
                try { settings = EngineSettings.Read(EngineSettings.PreferencesPath); }
                catch (Exception e) { Log("Preferences load: " + e); }
            }
            Theme.Apply(smoke ? "Light" : settings.Theme);
            var window = new MainWindow(settings, smoke);
            app.MainWindow = window;
            if (smoke)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                timer.Tick += async (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        string evidence = Path.Combine(Path.GetDirectoryName(_log)!, Path.GetFileNameWithoutExtension(_log) + "-screens");
                        await window.RunSmokeChecksAsync(evidence);
                        if (forceCompatibility)
                        {
                            if (!Theme.IsClassic) throw new InvalidOperationException("Compatibility mode was not selected.");
                            Log("PASS classic WPF compatibility mode");
                        }
                        Log("PASS WPF window, six pages, theme and twelve action editors");
                        app.Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        Log("WPF smoke failure: " + ex);
                        app.Shutdown(2);
                    }
                };
                window.Loaded += (_, _) => timer.Start();
            }
            window.Show();
            Log("WPF window shown");
            int code = app.Run();
            Log("Settings exit: " + code);
            return code;
        }
        catch (Exception ex)
        {
            Log("Startup failure: " + ex);
            if (!smoke) MessageBox.Show("Nyxic Gestures could not start. See ui-status.log in its local data folder.\n\n" + ex.Message,
                "Nyxic Gestures", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
