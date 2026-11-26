using System.Diagnostics;
using System.Drawing;
using WGestures.Core;
using WGestures.Common.OsSpecific.Windows;
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
            bool compatibilitySmoke = args is ["--smoke-hooks-compat"];
            bool smoke = args is ["--smoke-hooks"] || compatibilitySmoke;
            var compatibility = WindowsCompatibility.Current(compatibilitySmoke);
            if (!compatibility.MeetsMinimum)
                throw new PlatformNotSupportedException("Nyxic Gestures requires Windows 10 version 1607 (build 14393) or later.");
            using var instance = new Mutex(true, EngineSettings.EngineMutex, out bool created);
            if (!created) return 2;
            if (Process.GetProcessesByName("WGestures").Any())
                throw new InvalidOperationException("Exit the original WGestures before starting Nyxic Gestures.");
            using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, EngineSettings.StopEvent);
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EngineSettings.ReadyEvent);
            stop.Reset();
            ready.Reset();
            var settings = smoke ? new EngineSettings() : EngineSettings.Read();
            var workspace = smoke
                ? GestureWorkspace.Parse("""{"FileVersion":"3","Global":{"Name":"Global","IsGesturingEnabled":false,"GestureIntents":[]},"Apps":{},"HotCornerCommands":[null,null,null,null,null,null,null,null]}""")
                : GestureWorkspace.Open(EngineSettings.ActivePath);
            var store = new WorkspaceIntentStore(workspace, settings);
            var dpiMode = compatibility.UsePerMonitorV2 ? HighDpiMode.PerMonitorV2 : HighDpiMode.PerMonitor;
            bool dpiConfigured = Application.SetHighDpiMode(dpiMode);
            Log($"Windows {compatibility.WindowsVersion}; requested DPI mode: {dpiMode}; configured={dpiConfigured}");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var context = new EngineContext(store, settings, stop, ready);
            bool smokePassed = false;
            using var smokeTimer = new System.Windows.Forms.Timer { Interval = 200 };
            int elapsed = 0;
            if (smoke)
            {
                smokeTimer.Tick += (_, _) =>
                {
                    elapsed += 200;
                    if (ready.WaitOne(0))
                    {
                        smokePassed = true;
                        stop.Set();
                        smokeTimer.Stop();
                    }
                    else if (elapsed >= 8000)
                    {
                        stop.Set();
                        smokeTimer.Stop();
                    }
                };
                smokeTimer.Start();
            }
            Application.Run(context);
            return smoke && !smokePassed ? 4 : 0;
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
    private GlobalHotKeyManager? _hotkey;
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
        _parser.CommandFailed += (name, error) => Program.Log("Command failed [" + name + "]: " + error);
        _view = new CanvasWindowGestureView(_parser)
        {
            ShowPath = settings.ShowPath, ShowCommandName = settings.ShowCommandName, ViewFadeOut = settings.FadeOut,
            PathMainColor = Color.FromArgb(settings.RightTrailColor), PathMiddleBtnMainColor = Color.FromArgb(settings.MiddleTrailColor),
            PathXBtnMainColor = Color.FromArgb(settings.XTrailColor), PathAlternativeColor = Color.FromArgb(settings.UnrecognizedTrailColor)
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => OpenSettings());
        _pause = new ToolStripMenuItem("Pause gestures", null, (_, _) => _parser.TogglePause());
        menu.Items.Add(_pause);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Nyxic Gestures", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
        if (settings.PauseKey != 0)
        {
            try
            {
                _hotkey = new GlobalHotKeyManager();
                if (!_hotkey.RegisterHotKey("PauseResume", (GlobalHotKeyManager.ModifierKeys)settings.PauseModifiers,
                    (Keys)settings.PauseKey, _ => _parser.TogglePause()))
                    Program.Log("Pause shortcut could not be registered (already in use).");
            }
            catch (Exception ex) { Program.Log("Pause shortcut: " + ex.Message); }
        }
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
            _hotkey?.Dispose();
            _tray.Dispose();
            _timer.Dispose();
            Program.Log("Engine stopped.");
        }
        base.Dispose(disposing);
    }
}
