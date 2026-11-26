using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

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
    public bool AutoStart { get; init; }
    public int PauseKey { get; init; }
    public int PauseModifiers { get; init; } = 7;
    public int RightTrailColor { get; init; } = -14165512;
    public int MiddleTrailColor { get; init; } = -13771520;
    public int XTrailColor { get; init; } = -32704;
    public int UnrecognizedTrailColor { get; init; } = -10061415;
    public string Theme { get; init; } = "System";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyxic-Lab", "Gestures");
    public static string DraftPath => Path.Combine(DataDirectory, "workspace.wg2");
    public static string ActivePath => Path.Combine(DataDirectory, "active.wg2");
    public static string SettingsPath => Path.Combine(DataDirectory, "engine.json");
    public static string PreferencesPath => Path.Combine(DataDirectory, "preferences.json");
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

    public static EngineSettings ImportLegacyPlist(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 1024 * 1024) throw new InvalidDataException("Legacy preferences file exceeds 1 MiB.");
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024
        });
        var root = XDocument.Load(reader).Root?.Element("dict") ?? throw new InvalidDataException("Invalid legacy plist.");
        var values = root.Elements().ToArray();
        if (values.Length % 2 != 0) throw new InvalidDataException("Invalid plist pairs.");
        var dict = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (int i = 0; i < values.Length; i += 2)
        {
            if (values[i].Name != "key") throw new InvalidDataException("Invalid plist key.");
            dict.Add(values[i].Value, values[i + 1]);
        }
        int Int(string key, int fallback) => dict.TryGetValue(key, out var v) && int.TryParse(v.Value, out int n) ? n : fallback;
        bool Bool(string key, bool fallback) => dict.TryGetValue(key, out var v) ? v.Name.LocalName switch { "true" => true, "false" => false, _ => fallback } : fallback;
        var defaults = new EngineSettings();
        int pause = 0, modifiers = 7;
        if (dict.TryGetValue("PauseResumeHotKey", out var data))
        {
            byte[] bytes = Convert.FromBase64String(data.Value);
            if (bytes.Length == 8)
            {
                pause = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
                modifiers = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));
            }
        }
        var result = defaults with
        {
            TriggerButtons = Int("PathTrackerTriggerButton", defaults.TriggerButtons),
            EightDirections = Bool("GestureParserEnable8DirGesture", defaults.EightDirections),
            HotCorners = Bool("GestureParserEnableHotCorners", defaults.HotCorners),
            RubEdges = Bool("GestureParserEnableRubEdges", defaults.RubEdges),
            DisableFullscreen = Bool("GestureParserDisableInFullScreenMode", defaults.DisableFullscreen),
            PreferCursorWindow = Bool("PathTrackerPreferCursorWindow", defaults.PreferCursorWindow),
            ShowPath = Bool("GestureViewShowPath", defaults.ShowPath),
            ShowCommandName = Bool("GestureViewShowCommandName", defaults.ShowCommandName),
            FadeOut = Bool("GestureViewFadeOut", defaults.FadeOut),
            InitialStayTimeout = Bool("PathTrackerInitialStayTimeout", defaults.InitialStayTimeout),
            InitialStayMilliseconds = Int("PathTrackerInitialStayTimoutMillis", defaults.InitialStayMilliseconds),
            StayTimeout = Bool("PathTrackerStayTimeout", defaults.StayTimeout),
            StayMilliseconds = Int("PathTrackerStayTimeoutMillis", defaults.StayMilliseconds),
            WindowsKeyGestures = Bool("EnableWindowsKeyGesturing", defaults.WindowsKeyGestures),
            RightTrailColor = Int("GestureViewMainPathColor", defaults.RightTrailColor),
            MiddleTrailColor = Int("GestureViewMiddleBtnMainColor", defaults.MiddleTrailColor),
            XTrailColor = Int("GestureViewXBtnPathColor", defaults.XTrailColor),
            UnrecognizedTrailColor = Int("GestureViewAlternativePathColor", defaults.UnrecognizedTrailColor),
            PauseKey = pause, PauseModifiers = modifiers
            // Auto-start and executable scripts require separate opt-in after migration.
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (PauseKey is < 0 or > 254 || PauseModifiers is < 0 or > 15 || (PauseKey != 0 && PauseModifiers == 0))
            throw new InvalidDataException("Invalid global pause shortcut.");
        if (Theme is not ("System" or "Light" or "Dark")) throw new InvalidDataException("Unknown theme.");
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
