using System.Text.Json.Nodes;
using WGestures.Modern.Core;

var fixture = Path.Combine(AppContext.BaseDirectory, "gestures.wg2");
var original = File.ReadAllText(fixture);
var workspace = GestureWorkspace.Parse(original);
int checks = 0;

void Check(bool passed, string label)
{
    if (!passed) throw new InvalidOperationException(label);
    Console.WriteLine("PASS " + label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (ArgumentException) { Check(true, label); return; }
    catch (InvalidDataException) { Check(true, label); return; }
    throw new InvalidOperationException("Expected rejection: " + label);
}

var profiles = workspace.Profiles;
var global = profiles.Single(p => p.Key == GestureWorkspace.GlobalKey);
Check(global.Gestures.Count == 52, "52 bundled global gestures");
Check(profiles.Count == 4, "global and three bundled app profiles");
Check(profiles.Sum(p => p.Gestures.Count) == 66, "66 total gestures");
Check(workspace.Corners.Count == 8, "eight corner / edge slots");
Check(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(workspace.Serialize())),
    "unmodified source survives roundtrip, including opaque command metadata");

var row = global.Gestures.First(r => r.Path.Length > 0);
var before = JsonNode.Parse(workspace.Serialize())!;
workspace.EditGesture(row.ProfileKey, row.Index, "Regression test", row.Path);
var after = JsonNode.Parse(workspace.Serialize())!;
var edited = workspace.Profiles[0].Gestures.Single(r => r.Index == row.Index);
Check(edited.Name == "Regression test", "name edited");
Check(edited.Path == row.Path && edited.Trigger == row.Trigger && edited.Modifier == row.Modifier,
    "gesture identity preserved during rename");
before["Global"]!["GestureIntents"]![row.Index]!["Name"] = "Regression test";
Check(JsonNode.DeepEquals(before, after), "name-only edit preserves every other JSON value");

var unusedPath = new string('↗', 31);
workspace.EditGesture(row.ProfileKey, row.Index, edited.Name, unusedPath);
Check(workspace.Profiles[0].Gestures.Single(r => r.Index == row.Index).Path == unusedPath,
    "direction arrows roundtrip");
Reject(() => workspace.EditGesture(row.ProfileKey, row.Index, "", row.Path), "empty name rejected");
Reject(() => workspace.EditGesture(row.ProfileKey, row.Index, "Test", "not arrows"), "invalid direction rejected");

var pair = global.Gestures.GroupBy(r => (r.Trigger, r.Modifier)).First(g => g.Count() > 1);
var a = pair.First();
var b = pair.Skip(1).First();
Reject(() => workspace.EditGesture(a.ProfileKey, a.Index, a.Name, b.Path), "duplicate gesture rejected");

var unknown = JsonNode.Parse(original)!;
unknown["FileVersion"] = "99";
Reject(() => GestureWorkspace.Parse(unknown.ToJsonString()), "unsupported format rejected");

var directory = Path.Combine(Path.GetTempPath(), "nyxic-gestures-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var draft = Path.Combine(directory, "workspace.wg2");
    workspace.SaveDraft(draft);
    Check(JsonNode.DeepEquals(JsonNode.Parse(workspace.Serialize()), JsonNode.Parse(GestureWorkspace.Open(draft).Serialize())),
        "saved draft can be reopened without data loss");
    workspace.SaveDraft(draft);
    Check(Directory.GetFiles(directory).Length == 1, "atomic replacement leaves no temporary files");
    Check(File.ReadAllText(fixture) == original, "original fixture is never changed");
}
finally
{
    if (Directory.Exists(directory)) Directory.Delete(directory, true);
}
// Complete editor / engine contract checks.
var managed = GestureWorkspace.Parse(original);
managed.ValidateForEngine();
Check(true, "bundled profile and command catalog validate for engine");
var key = managed.AddProfile(@"C:\test\editor.exe", "Test editor");
Check(managed.Profiles.Any(p => p.Key == key && p.InheritsGlobal), "new profile inherits global gestures");
var shortcut = CommandCatalog.Create("HotKey");
shortcut["Keys"] = new JsonArray(JsonValue.Create(86));
var index = managed.UpsertGesture(key, null, "Paste", "↓", 1, 0, false, shortcut);
Check(managed.Profiles.Single(p => p.Key == key).Gestures.Single().Name == "Paste", "add real gesture");
managed.UpsertGesture(key, index, "Paste alternate", "↘", 2, 1, true, shortcut);
var changed = managed.Profiles.Single(p => p.Key == key).Gestures.Single();
Check(changed.Trigger == 2 && changed.Modifier == 1 && changed.Path == "↘" &&
    managed.GetExecuteOnModifier(key, index), "edit trigger, modifier, direction and execute-on-modifier");
var second = managed.UpsertGesture(key, null, "Copy", "↑", 1, 0, false, CommandCatalog.Create("HotKey"));
managed.MoveGesture(key, second, -1);
Check(managed.Profiles.Single(p => p.Key == key).Gestures[0].Name == "Copy", "gesture order changes");
managed.DeleteGesture(key, index);
Check(managed.Profiles.Single(p => p.Key == key).Gestures.Count == 1, "delete gesture");
managed.UpdateProfile(key, "Renamed", false, false);
Check(!managed.Profiles.Single(p => p.Key == key).Enabled && !managed.Profiles.Single(p => p.Key == key).InheritsGlobal,
    "profile enable/inherit settings persist");
managed.SetCornerCommand(0, CommandCatalog.Create("DoNothing"));
Check(managed.Corners[0].Command == "DoNothing", "corner action edit");
managed.DeleteProfile(key);
Check(managed.Profiles.All(p => p.Key != key), "delete application profile");
Reject(() => managed.DeleteProfile(GestureWorkspace.GlobalKey), "global profile cannot be removed");
var invalid = CommandCatalog.Create("HotKey");
invalid["$type"] = "System.IO.FileInfo, System.Private.CoreLib";
Reject(() => CommandCatalog.Normalize(invalid), "arbitrary CLR metadata rejected");
var foreign = CommandCatalog.Create("HotKey");
foreign["Context"] = new JsonObject { ["$type"] = "Unknown, Unknown" };
Check(!CommandCatalog.Normalize(foreign).ContainsKey("Context"), "unknown command fields never reach runtime");
foreach (var def in CommandCatalog.Definitions)
    Check(CommandCatalog.Identify(CommandCatalog.Normalize(CommandCatalog.Create(def.Id))) == def.Id, "command model: " + def.Id);
managed.ValidateForEngine();
Check(true, "edited configuration validates for runtime");
var settings = new EngineSettings { TriggerButtons = 0 };
Reject(settings.Validate, "no active trigger buttons rejected");

Console.WriteLine($"{checks} checks passed.");
