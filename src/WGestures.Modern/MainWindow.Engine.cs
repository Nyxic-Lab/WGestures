using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WGestures.Modern.Core;

namespace WGestures.Modern;

public sealed partial class MainWindow
{
    private EngineSettings _settings = new();
    private bool _busy;
    private string EngineExe => Path.Combine(AppContext.BaseDirectory, "engine", "Nyxic.Gestures.Engine.exe");

    private static bool EngineReady()
    {
        try
        {
            using var ready = EventWaitHandle.OpenExisting(EngineSettings.ReadyEvent);
            return ready.WaitOne(0);
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }

    private static bool EngineRunning()
    {
        try
        {
            using var instance = Mutex.OpenExisting(EngineSettings.EngineMutex);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
    }

    private async Task StopEngineAsync()
    {
        if (!EngineRunning()) return;
        try
        {
            using var stop = EventWaitHandle.OpenExisting(EngineSettings.StopEvent);
            stop.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        for (int i = 0; i < 80 && EngineRunning(); i++) await Task.Delay(125);
        if (EngineRunning()) throw new InvalidOperationException("The engine has not shut down. Exit it from the tray before retrying.");
    }

    private async Task RunEngineAsync()
    {
        if (!File.Exists(EngineExe)) throw new FileNotFoundException("The gesture engine is missing. Install the complete desktop package.", EngineExe);
        if (Process.GetProcessesByName("WGestures").Any())
            throw new InvalidOperationException("Exit the original WGestures before starting Nyxic Gestures.");
        using var process = Process.Start(new ProcessStartInfo(EngineExe)
        {
            UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(EngineExe)!
        }) ?? throw new InvalidOperationException("Could not launch the engine.");
        for (int i = 0; i < 100; i++)
        {
            if (EngineReady()) return;
            if (process.HasExited)
                throw new InvalidOperationException($"Engine startup failed (exit {process.ExitCode}). See {EngineSettings.StatusPath}.");
            await Task.Delay(100);
        }
        throw new InvalidOperationException("The engine did not report installed input hooks. Check the diagnostic log.");
    }

    private async Task<bool> ConfirmAsync(string title, string message, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, Title = title, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ApplyAndStartAsync()
    {
        if (_busy || _workspace is null) return;
        // Applying is the explicit trust boundary: actions can launch programs and type keys.
        if (!await ConfirmAsync("Apply configuration and start engine?",
            "Gestures will run the actions in this configuration. Only activate a file you trust. Exit the original WGestures before proceeding. " +
            (_settings.AllowScripts ? "Command-line and Lua actions are ENABLED." : "Command-line and Lua actions are disabled."),
            "Apply and start")) return;
        _busy = true;
        string? candidate = null, settingsFile = null;
        string? oldActive = null, oldSettings = null;
        bool committed = false;
        try
        {
            if (!File.Exists(EngineExe)) throw new FileNotFoundException("The complete engine is not installed.");
            if (Process.GetProcessesByName("WGestures").Any())
                throw new InvalidOperationException("Exit the original WGestures first.");
            _workspace.ValidateForEngine();
            _settings.Validate();
            Directory.CreateDirectory(EngineSettings.DataDirectory);
            candidate = Path.Combine(EngineSettings.DataDirectory, "candidate-" + Guid.NewGuid().ToString("N") + ".wg2");
            settingsFile = candidate + ".json";
            _workspace.SaveDraft(candidate);
            _settings.Save(settingsFile);
            var psi = new ProcessStartInfo(EngineExe) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add("--validate");
            psi.ArgumentList.Add(candidate);
            psi.ArgumentList.Add(settingsFile);
            using (var validator = Process.Start(psi) ?? throw new InvalidOperationException("Cannot validate engine configuration."))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await validator.WaitForExitAsync(timeout.Token);
                if (validator.ExitCode != 0)
                    throw new InvalidOperationException($"Engine rejected the configuration. See {EngineSettings.StatusPath}.");
            }
            await StopEngineAsync();
            oldActive = File.Exists(EngineSettings.ActivePath) ? File.ReadAllText(EngineSettings.ActivePath) : null;
            oldSettings = File.Exists(EngineSettings.SettingsPath) ? File.ReadAllText(EngineSettings.SettingsPath) : null;
            committed = true;
            _workspace.SaveDraft(EngineSettings.ActivePath);
            _settings.Save();
            _workspace.SaveDraft(_draftPath);
            _settings.Save(EngineSettings.PreferencesPath);
            await RunEngineAsync();
            _source = _draftPath;
            _dirty = false;
            Render();
            Notify("Gestures are running", "The active configuration was validated and applied; input hooks reported ready.", InfoBarSeverity.Success);
        }
        catch (Exception e)
        {
            if (committed)
            {
                try
                {
                    await StopEngineAsync();
                    RestoreFile(EngineSettings.ActivePath, oldActive);
                    RestoreFile(EngineSettings.SettingsPath, oldSettings);
                }
                catch (Exception rollback) { Notify("Apply failed; rollback incomplete", rollback.Message, InfoBarSeverity.Error); return; }
            }
            Notify("Could not apply configuration", e.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (candidate is not null && File.Exists(candidate)) File.Delete(candidate);
            if (settingsFile is not null && File.Exists(settingsFile)) File.Delete(settingsFile);
            _busy = false;
            UpdateSource();
        }
    }

    private static void RestoreFile(string path, string? contents)
    {
        if (contents is null) { if (File.Exists(path)) File.Delete(path); return; }
        string temporary = path + ".rollback-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllText(temporary, contents); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task StopFromUiAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await StopEngineAsync();
            Notify("Engine stopped", "Mouse gestures are no longer intercepted.", InfoBarSeverity.Success);
        }
        catch (Exception e) { Notify("Could not stop the engine", e.Message, InfoBarSeverity.Error); }
        finally { _busy = false; UpdateSource(); }
    }

    private Button AsyncAction(string label, Func<Task> action, bool primary = false)
    {
        var button = new Button { Content = label, MinHeight = 36 };
        if (primary) button.Style = (Style)Root.Resources["PrimaryAction"];
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception ex) { Notify("Operation failed", ex.Message, InfoBarSeverity.Error); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    private Border EngineCard() => Card(Heading("Gesture engine"),
        Paragraph(EngineReady() ? "Running · Input hooks installed" : "Stopped · Apply your configuration to start"),
        AsyncAction("Apply & start / reload", ApplyAndStartAsync, true),
        AsyncAction("Stop engine", StopFromUiAsync),
        Action("Open diagnostics folder", () =>
        {
            Directory.CreateDirectory(EngineSettings.DataDirectory);
            Process.Start(new ProcessStartInfo(EngineSettings.DataDirectory) { UseShellExecute = true });
        }));

    private void ApplyTheme() => Root.RequestedTheme = _settings.Theme switch
    {
        "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default
    };
}
