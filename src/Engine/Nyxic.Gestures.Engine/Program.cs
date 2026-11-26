using System.Diagnostics;
using System.Drawing;
using WGestures.Core;
using WGestures.Core.Impl.Windows;
using WGestures.Core.Persistence.Impl.Windows;
using WGestures.Modern.Core;
using WGestures.View.Impl.Windows;

namespace Nyxic.Gestures.Engine;

internal static class Program
{
    internal static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(EngineSettings.DataDirectory);
            if (File.Exists(EngineSettings.StatusPath) && new FileInfo(EngineSettings.StatusPath).Length > 1024 * 1024)
                File.Delete(EngineSettings.StatusPath);
            File.AppendAllText(EngineSettings.StatusPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args is ["--stop"])
            {
                try
                {
                    using var signal = EventWaitHandle.OpenExisting(EngineSettings.StopEvent);
                    signal.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }
                for (int i = 0; i < 80; i++)
                {
                    try { using var running = Mutex.OpenExisting(EngineSettings.EngineMutex); }
                    catch (WaitHandleCannotBeOpenedException) { return 0; }
                    Thread.Sleep(125);
                }
                return 3;
            }
            if (args is ["--validate", var path, var settingsPath])
            {
                _ = new WorkspaceIntentStore(GestureWorkspace.Open(path), EngineSettings.Read(settingsPath));
                return 0;
            }
            using var instance = new Mutex(true, EngineSettings.EngineMutex, out bool created);
            if (!created) return 2;
            if (Process.GetProcessesByName("WGestures").Any())
                throw new InvalidOperationException("Exit the original WGestures before starting Nyxic Gestures.");
            using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, EngineSettings.StopEvent);
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EngineSettings.ReadyEvent);
            stop.Reset();
            ready.Reset();
            var settings = EngineSettings.Read();
            var store = new WorkspaceIntentStore(GestureWorkspace.Open(EngineSettings.ActivePath), settings);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var context = new EngineContext(store, settings, stop, ready);
            Application.Run(context);
            return 0;
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            return 1;
        }
    }
}

internal sealed class EngineContext : ApplicationContext
{
    private readonly EventWaitHandle _stop;
    private readonly EventWaitHandle _ready;
    private readonly Win32MousePathTracker2 _tracker;
    private readonly GestureParser _parser;
    private readonly CanvasWindowGestureView _view;
    private readonly Thread _worker;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _pause;
    private bool _disposed;

    internal EngineContext(WorkspaceIntentStore store, EngineSettings settings, EventWaitHandle stop, EventWaitHandle ready)
    {
        _stop = stop;
        _ready = ready;
        _tracker = new Win32MousePathTracker2
        {
            TriggerButton = (GestureTriggerButton)settings.TriggerButtons,
            DisableInFullscreen = settings.DisableFullscreen,
            PreferWindowUnderCursorAsTarget = settings.PreferCursorWindow,
            StayTimeout = settings.StayTimeout,
            StayTimeoutMillis = settings.StayMilliseconds,
            InitialStayTimeout = settings.InitialStayTimeout,
            InitialStayTimeoutMillis = settings.InitialStayMilliseconds,
            EnableWindowsKeyGesturing = settings.WindowsKeyGestures
        };
        _parser = new GestureParser(_tracker, new Win32GestrueIntentFinder(store))
        {
            Enable8DirGesture = settings.EightDirections,
            EnableHotCorners = settings.HotCorners,
            EnableRubEdge = settings.RubEdges
        };
        _view = new CanvasWindowGestureView(_parser)
        {
            ShowPath = settings.ShowPath, ShowCommandName = settings.ShowCommandName, ViewFadeOut = settings.FadeOut
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => OpenSettings());
        _pause = new ToolStripMenuItem("Pause gestures", null, (_, _) => _parser.TogglePause());
        menu.Items.Add(_pause);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Nyxic Gestures", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
        _tracker.RequestPauseResume += _ => _parser.TogglePause();
        _tracker.HookReady += () => { _ready.Set(); Program.Log("Input hooks installed."); };
        _worker = new Thread(() =>
        {
            try { _parser.Start(); }
            catch (Exception ex) { Program.Log(ex.ToString()); }
            finally { _ready.Reset(); _stop.Set(); }
        }) { IsBackground = true, Name = "Nyxic gesture recognition" };
        _worker.SetApartmentState(ApartmentState.STA);
        _timer = new System.Windows.Forms.Timer { Interval = 200 };
        _timer.Tick += (_, _) =>
        {
            if (_stop.WaitOne(0) || !_worker.IsAlive) { ExitThread(); return; }
            _pause.Text = _parser.IsPaused ? "Resume gestures" : "Pause gestures";
            _tray.Text = _parser.IsPaused ? "Nyxic Gestures (paused)" : "Nyxic Gestures";
        };
        _worker.Start();
        _timer.Start();
    }

    private static void OpenSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "WGestures.Modern.exe");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _timer.Stop();
            _ready.Reset();
            _tray.Visible = false;
            try
            {
                _parser.Stop();
                if (!_worker.Join(5000)) Program.Log("Gesture thread did not stop within five seconds.");
                else
                {
                    _view.Dispose();
                    _parser.Dispose();
                    _tracker.Dispose();
                }
            }
            catch (Exception ex) { Program.Log(ex.ToString()); }
            _tray.Dispose();
            _timer.Dispose();
            Program.Log("Engine stopped.");
        }
        base.Dispose(disposing);
    }
}
