using System.Collections;
using System.Text.Json;
using WGestures.Core;
using WGestures.Core.Commands;
using WGestures.Core.Commands.Impl;
using WGestures.Core.Persistence;
using WGestures.Modern.Core;

namespace Nyxic.Gestures.Engine;

internal sealed class WorkspaceIntentStore : IGestureIntentStore
{
    private readonly Dictionary<string, ExeApp> _apps = new(StringComparer.OrdinalIgnoreCase);
    public GlobalApp GlobalApp { get; } = new();
    public AbstractCommand[] HotCornerCommands { get; set; }

    public WorkspaceIntentStore(GestureWorkspace workspace, EngineSettings settings)
    {
        workspace.ValidateForEngine();
        foreach (var profile in workspace.Profiles)
        {
            AbstractApp app;
            if (profile.Key == GestureWorkspace.GlobalKey) app = GlobalApp;
            else
            {
                var exe = new ExeApp { ExecutablePath = profile.ExecutablePath!, InheritGlobalGestures = profile.InheritsGlobal };
                _apps.Add(exe.ExecutablePath, exe);
                app = exe;
            }
            app.Name = profile.Name;
            app.IsGesturingEnabled = profile.Enabled;
            foreach (var row in profile.Gestures)
            {
                var gesture = new Gesture((GestureTriggerButton)row.Trigger)
                {
                    Modifier = (GestureModifier)row.Modifier
                };
                foreach (char c in row.Path) gesture.Dirs.Add((Gesture.GestureDir)"↑↗→↘↓↙←↖".IndexOf(c));
                app.Add(new GestureIntent
                {
                    Name = row.Name, Gesture = gesture,
                    ExecuteOnModifier = workspace.GetExecuteOnModifier(profile.Key, row.Index),
                    Command = CreateCommand(workspace.GetCommand(profile.Key, row.Index), settings.AllowScripts)
                });
            }
        }
        HotCornerCommands = Enumerable.Range(0, 8)
            .Select(i => CreateCommand(workspace.GetCornerCommand(i), settings.AllowScripts)).ToArray();
    }

    internal static AbstractCommand CreateCommand(System.Text.Json.Nodes.JsonObject raw, bool allowScripts)
    {
        var command = CommandCatalog.Normalize(raw);
        var id = CommandCatalog.Identify(command)!;
        // Imported CLR type names are never resolved. Only compile-time command types
        // and explicitly accepted fields reach the deserializer.
        command.Remove("$type");
        var json = command.ToJsonString();
        return id switch
        {
            "HotKey" => JsonSerializer.Deserialize<HotKeyCommand>(json)!,
            "WindowControl" => JsonSerializer.Deserialize<WindowControlCommand>(json)!,
            "OpenFile" => JsonSerializer.Deserialize<OpenFileCommand>(json)!,
            "GotoUrl" => JsonSerializer.Deserialize<GotoUrlCommand>(json)!,
            "WebSearch" => JsonSerializer.Deserialize<WebSearchCommand>(json)!,
            "SendText" => JsonSerializer.Deserialize<SendTextCommand>(json)!,
            "Cmd" => allowScripts ? JsonSerializer.Deserialize<CmdCommand>(json)! : new DoNothingCommand(),
            "Script" => allowScripts ? JsonSerializer.Deserialize<ScriptCommand>(json)! : new DoNothingCommand(),
            "ChangeAudioVolume" => JsonSerializer.Deserialize<ChangeAudioVolumeCommand>(json)!,
            "TaskSwitcher" => new TaskSwitcherCommand(),
            "PauseWGestures" => new PauseWGesturesCommand(),
            "DoNothing" => new DoNothingCommand(),
            _ => throw new InvalidDataException("Unknown command type.")
        };
    }
    public bool TryGetExeApp(string key, out ExeApp found) => _apps.TryGetValue(key, out found!);
    public ExeApp GetExeApp(string key) => _apps[key];
    public void Remove(string key) => _apps.Remove(key);
    public void Remove(ExeApp app) => Remove(app.ExecutablePath);
    public void Add(ExeApp app) => _apps.Add(app.ExecutablePath, app);
    // The running engine is a read-only snapshot. Only the editor writes configuration.
    public void Save() => throw new NotSupportedException("The engine does not write settings.");
    public IEnumerator<ExeApp> GetEnumerator() => _apps.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
