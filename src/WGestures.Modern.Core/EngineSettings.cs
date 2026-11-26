using System.Text.Json;

namespace WGestures.Modern.Core;

public sealed record EngineSettings
{
    public int TriggerButtons { get; init; } = 15;
    public bool EightDirections { get; init; } = true;
    public bool HotCorners { get; init; } = false;
    public bool RubEdges { get; init; } = false;
    public bool DisableFullscreen { get; init; } = true;
    public bool PreferCursorWindow { get; init; } = true;
    public bool ShowPath { get; init; } = true;
    public bool ShowCommandName { get; init; } = true;
    public bool FadeOut { get; init; } = true;
    public bool InitialStayTimeout { get; init; }
    public int InitialStayMilliseconds { get; init; } = 200;
    public bool StayTimeout { get; init; }
    public int StayMilliseconds { get; init; } = 500;
    public bool WindowsKeyGestures { get; init; }
    public bool AllowScripts { get; init; }

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyxic-Lab", "Gestures");
    public static string DraftPath => Path.Combine(DataDirectory, "workspace.wg2");
    public static string ActivePath => Path.Combine(DataDirectory, "active.wg2");
    public static string SettingsPath => Path.Combine(DataDirectory, "engine.json");
    public static string StatusPath => Path.Combine(DataDirectory, "engine-status.log");
    public const string ReadyEvent = @"Local\Nyxic.Gestures.Engine.Ready.v1";
    public const string StopEvent = @"Local\Nyxic.Gestures.Engine.Stop.v1";
    public const string EngineMutex = @"Local\Nyxic.Gestures.Engine.Instance.v1";

    public static EngineSettings Read(string? path = null)
    {
        path ??= SettingsPath;
        var value = File.Exists(path)
            ? JsonSerializer.Deserialize<EngineSettings>(File.ReadAllText(path)) ?? new()
            : new EngineSettings();
        value.Validate();
        return value;
    }

    public void Validate()
    {
        if (TriggerButtons < 1 || TriggerButtons > 15 ||
            InitialStayMilliseconds is < 50 or > 3000 || StayMilliseconds is < 50 or > 10000)
            throw new InvalidDataException("Invalid engine input settings.");
    }

    public void Save(string? path = null)
    {
        Validate();
        path ??= SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
