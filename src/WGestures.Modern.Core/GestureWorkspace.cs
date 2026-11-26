using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WGestures.Modern.Core;

public sealed record GestureRow(string ProfileKey, int Index, string Name, int Trigger, string Path,
    int Modifier, string Command, int Order)
{
    public string TriggerLabel => Trigger switch
    {
        1 => "Right", 2 => "Middle", 4 => "X1", 8 => "X2", _ => $"Button {Trigger}"
    };
    public string ModifierLabel => Modifier switch
    {
        0 => "None", 1 => "Wheel forward", 2 => "Wheel backward", 4 => "Middle click",
        8 => "Left click", 16 => "Right click", 32 => "X1", 64 => "X2", _ => $"Modifier {Modifier}"
    };
    public string Summary => $"{TriggerLabel}  ·  {(Path.Length == 0 ? "—" : Path)}  ·  {Command}";
}

public sealed record GestureProfile(string Key, string Name, string? ExecutablePath, bool Enabled,
    bool InheritsGlobal, IReadOnlyList<GestureRow> Gestures);
public sealed record CornerAction(string Name, string Command);

/// <summary>
/// A version-3 WG1 document editor. CLR type annotations are opaque JSON strings.
/// No command is instantiated and no legacy file is ever written by this model.
/// </summary>
public sealed class GestureWorkspace
{
    public const string GlobalKey = "";
    private const int MaxBytes = 16 * 1024 * 1024;
    private const string Arrows = "↑↗→↘↓↙←↖";
    private readonly JsonObject _document;

    private GestureWorkspace(JsonObject document)
    {
        _document = document;
        _ = Profiles;
        _ = Corners;
    }

    public static GestureWorkspace Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new InvalidDataException("Configuration exceeds 16 MiB.");
        var document = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject
            ?? throw new InvalidDataException("Expected a JSON object.");
        if (document["FileVersion"]?.GetValue<string>() != "3")
            throw new InvalidDataException("Only original WGestures gesture format version 3 is supported.");
        return new GestureWorkspace(document);
    }

    public static GestureWorkspace Open(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Configuration exceeds 16 MiB.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        return Parse(reader.ReadToEnd());
    }

    private JsonObject App(string key) => key == GlobalKey
        ? Object(_document["Global"], "Global")
        : Object(Object(_document["Apps"], "Apps")[key], "Application");

    private static JsonObject Object(JsonNode? node, string field) =>
        node as JsonObject ?? throw new InvalidDataException($"Invalid or missing {field}.");
    private static JsonArray Array(JsonNode? node, string field) =>
        node as JsonArray ?? throw new InvalidDataException($"Invalid or missing {field}.");
    private static JsonArray Intents(JsonObject profile) => Array(profile["GestureIntents"], "GestureIntents");

    public IReadOnlyList<GestureProfile> Profiles
    {
        get
        {
            var result = new List<GestureProfile> { ReadProfile(GlobalKey, App(GlobalKey)) };
            foreach (var item in Object(_document["Apps"], "Apps"))
            {
                if (string.IsNullOrEmpty(item.Key)) throw new InvalidDataException("Empty application key.");
                result.Add(ReadProfile(item.Key, Object(item.Value, "Application")));
            }
            return result;
        }
    }

    private static GestureProfile ReadProfile(string key, JsonObject app)
    {
        var rows = Intents(app).Select((node, index) =>
        {
            var intent = Object(node, "Intent");
            var gesture = Object(intent["Gesture"], "Gesture");
            var dirs = Array(gesture["Dirs"], "Dirs").Select(d =>
            {
                int direction = d?.GetValue<int>() ?? -1;
                if (direction is < 0 or > 7) throw new InvalidDataException("Unknown gesture direction.");
                return Arrows[direction];
            });
            return new GestureRow(key, index, intent["Name"]?.GetValue<string>() ?? "(Unnamed)",
                gesture["GestureButton"]?.GetValue<int>() ?? throw new InvalidDataException("Missing trigger."),
                new string(dirs.ToArray()),
                gesture["Modifier"]?.GetValue<int>() ?? 0, CommandName(intent["Command"]),
                intent["Order"]?.GetValue<int>() ?? index);
        }).OrderBy(row => row.Order).ThenBy(row => row.Index).ToArray();
        return new GestureProfile(key, key == GlobalKey ? "Global" : app["Name"]?.GetValue<string>() ?? key,
            key == GlobalKey ? null : app["ExecutablePath"]?.GetValue<string>() ?? key,
            app["IsGesturingEnabled"]?.GetValue<bool>() ?? true,
            key != GlobalKey && (app["InheritGlobalGestures"]?.GetValue<bool>() ?? false), rows);
    }

    private static string CommandName(JsonNode? node)
    {
        if (node is null) return "Unassigned";
        var command = Object(node, "Command");
        var type = command["$type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type)) return "Unknown command";
        // Display only; never resolve or instantiate a CLR type from imported metadata.
        var name = type.Split(',')[0].Trim().Split('.').Last();
        return name.EndsWith("Command", StringComparison.Ordinal) ? name[..^7] : name;
    }

    public IReadOnlyList<CornerAction> Corners
    {
        get
        {
            // Original radio button tags define the persisted order.
            var items = Array(_document["HotCornerCommands"], "HotCornerCommands");
            if (items.Count != 8) throw new InvalidDataException("Expected eight corner/edge slots.");
            return items.Select((item, i) => new CornerAction(i < 4 ? $"Corner {i + 1}" : $"Edge {i - 3}",
                CommandName(item))).ToArray();
        }
    }

    public void EditGesture(string profileKey, int index, string name, string path)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 120) throw new ArgumentException("Name must contain 1–120 characters.");
        if (path.Length > 32 || path.Any(c => !Arrows.Contains(c)))
            throw new ArgumentException("Use up to 32 direction arrows.");
        var intents = Intents(App(profileKey));
        var intent = Object(intents[index], "Intent");
        var gesture = Object(intent["Gesture"], "Gesture");
        int trigger = gesture["GestureButton"]!.GetValue<int>();
        int modifier = gesture["Modifier"]?.GetValue<int>() ?? 0;
        if (path.Length == 0 && modifier == 0) throw new ArgumentException("A gesture needs a direction or modifier.");
        if (ReadProfile(profileKey, App(profileKey)).Gestures.Any(row =>
            row.Index != index && row.Trigger == trigger && row.Modifier == modifier && row.Path == path))
            throw new ArgumentException("A gesture with this trigger, path and modifier already exists in this profile.");
        var directions = new JsonArray();
        foreach (char c in path) directions.Add(JsonValue.Create(Arrows.IndexOf(c)));
        gesture["Dirs"] = directions;
        intent["Name"] = name;
    }

    public string Serialize() => _document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    public void SaveDraft(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, Serialize(), new UTF8Encoding(false));
            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
