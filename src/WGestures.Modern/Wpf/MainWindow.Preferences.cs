using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WGestures.Modern.Core;

namespace WGestures.Modern;

internal sealed partial class MainWindow
{
    private void RenderPreferences()
    {
        CheckBox Setting(string title, bool value, Action<bool> set) =>
            Check(title, value, v => { set(v); Changed(); });
        FrameworkElement Number(string title, int value, int min, int max, Action<int> set)
        {
            var box = Input(value.ToString(CultureInfo.InvariantCulture));
            int current = value;
            box.LostKeyboardFocus += (_, _) =>
            {
                if (int.TryParse(box.Text, out int v) && v >= min && v <= max)
                {
                    if (v != current) { current = v; set(v); Changed(); }
                }
                else
                {
                    box.Text = current.ToString(CultureInfo.InvariantCulture);
                    Notify("Invalid number", $"{title}: enter {min}–{max}.", true);
                }
            };
            return Field(title, box);
        }
        var themes = Choice(new[] { "System", "Light", "Dark" }, _settings.Theme);
        themes.SelectionChanged += (_, _) =>
        {
            if (themes.SelectedItem is string selected)
            {
                _settings = _settings with { Theme = selected };
                Theme.Apply(selected); Changed();
            }
        };
        Add(_page, Card("Appearance", Field("Theme", themes),
            Setting("Show gesture trail", _settings.ShowPath, v => _settings = _settings with { ShowPath = v }),
            Setting("Show action name", _settings.ShowCommandName, v => _settings = _settings with { ShowCommandName = v }),
            Setting("Fade out gesture trail", _settings.FadeOut, v => _settings = _settings with { FadeOut = v }),
            ColorSetting("Right-button trail", _settings.RightTrailColor, v => _settings = _settings with { RightTrailColor = v }),
            ColorSetting("Middle-button trail", _settings.MiddleTrailColor, v => _settings = _settings with { MiddleTrailColor = v }),
            ColorSetting("X-button trail", _settings.XTrailColor, v => _settings = _settings with { XTrailColor = v }),
            ColorSetting("Unrecognized trail", _settings.UnrecognizedTrailColor, v => _settings = _settings with { UnrecognizedTrailColor = v })));
        var triggers = new StackPanel();
        foreach (var (bit,label) in new[] { (1,"Right button"), (2,"Middle button"), (4,"X1"), (8,"X2") })
            Add(triggers, Setting(label, (_settings.TriggerButtons & bit) != 0,
                v => _settings = _settings with { TriggerButtons = v ? _settings.TriggerButtons | bit : _settings.TriggerButtons & ~bit }), 4);
        Add(_page, Card("Input", Text("Trigger buttons", 13, true), triggers,
            Setting("Eight-direction recognition", _settings.EightDirections, v => _settings = _settings with { EightDirections = v }),
            Setting("Disable in fullscreen", _settings.DisableFullscreen, v => _settings = _settings with { DisableFullscreen = v }),
            Setting("Prefer window under cursor", _settings.PreferCursorWindow, v => _settings = _settings with { PreferCursorWindow = v }),
            Setting("Windows-key gestures", _settings.WindowsKeyGestures, v => _settings = _settings with { WindowsKeyGestures = v }),
            Setting("Initial stay timeout", _settings.InitialStayTimeout, v => _settings = _settings with { InitialStayTimeout = v }),
            Number("Initial timeout (ms)", _settings.InitialStayMilliseconds, 50, 3000, v => _settings = _settings with { InitialStayMilliseconds = v }),
            Setting("Gesture stay timeout", _settings.StayTimeout, v => _settings = _settings with { StayTimeout = v }),
            Number("Stay timeout (ms)", _settings.StayMilliseconds, 50, 10000, v => _settings = _settings with { StayMilliseconds = v })));
        Add(_page, Card("Screen interactions",
            Setting("Hot corners", _settings.HotCorners, v => _settings = _settings with { HotCorners = v }),
            Setting("Rub edges", _settings.RubEdges, v => _settings = _settings with { RubEdges = v })));
        var pauseKey = Number("Pause shortcut key (virtual-key code; 0 disables)", _settings.PauseKey, 0, 254,
            v => _settings = _settings with { PauseKey = v });
        var mods = new StackPanel();
        foreach (var (bit,label) in new[] { (1,"Alt"), (2,"Ctrl"), (4,"Shift"), (8,"Windows") })
            Add(mods, Setting(label, (_settings.PauseModifiers & bit) != 0,
                v => _settings = _settings with { PauseModifiers = v ? _settings.PauseModifiers | bit : _settings.PauseModifiers & ~bit }),4);
        var record = Input("Click here and press a pause shortcut"); record.IsReadOnly = true;
        record.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            int code = KeyInterop.VirtualKeyFromKey(key);
            if (code is < 1 or > 254 || code is 16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165) return;
            int mask = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mask |= 1;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mask |= 2;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mask |= 4;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mask |= 8;
            if (mask == 0) { Notify("Choose a modifier", "A global shortcut must include Ctrl, Alt, Shift or Windows.", true); return; }
            _settings = _settings with { PauseKey = code, PauseModifiers = mask };
            Changed(); Render(); Notify("Shortcut recorded", key.ToString());
        };
        Add(_page, Card("Startup and pause",
            Setting("Start engine when I sign in", _settings.AutoStart, v => _settings = _settings with { AutoStart = v }),
            Field("Record pause shortcut", record), pauseKey, Text("Pause shortcut modifiers", 13, true), mods,
            Text("A–Z = 65–90; F1–F12 = 112–123. The system tray also supports pause/resume.", 12, muted: true)));
        Add(_page, Card("Script actions", Text("Enable command-line and Lua actions only for a configuration you trust.", muted: true),
            Setting("Allow command-line and Lua actions", _settings.AllowScripts, v => _settings = _settings with { AllowScripts = v })));
        Add(_page, Card("Legacy preferences", Text("Import the original XML config.plist. Scripts and auto-start remain opt-in.", muted: true),
            Button("Import config.plist…", () =>
            {
                var picker = new OpenFileDialog { Filter = "Property list (*.plist)|*.plist", CheckFileExists = true };
                if (picker.ShowDialog(this) != true) return;
                if (!Confirm("Import preferences", "Replace your workspace preferences with the selected legacy file?")) return;
                _settings = EngineSettings.ImportLegacyPlist(picker.FileName) with { Theme = _settings.Theme };
                Changed(); Render(); Notify("Preferences imported", "Apply to activate them.");
            })));
    }

    private FrameworkElement ColorSetting(string label, int argb, Action<int> set)
    {
        uint value = unchecked((uint)argb);
        var swatch = new Border { Width = 35, Height = 35, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value)) };
        swatch.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        swatch.BorderThickness = new Thickness(1);
        var button = Button("Change color", () =>
        {
            var sliders = new StackPanel();
            var preview = new Border { Height = 48, CornerRadius = new CornerRadius(8), Background = swatch.Background };
            Add(sliders, preview);
            var channels = new[] { (double)(value >> 24), (double)((value >> 16) & 255), (double)((value >> 8) & 255), (double)(value & 255) };
            var names = new[] { "Opacity (alpha)", "Red", "Green", "Blue" };
            var controls = new Slider[4];
            var hex = Text("#" + value.ToString("X8"), 14, true);
            void Refresh()
            {
                preview.Background = new SolidColorBrush(Color.FromArgb((byte)channels[0], (byte)channels[1], (byte)channels[2], (byte)channels[3]));
                uint packed = ((uint)channels[0] << 24) | ((uint)channels[1] << 16) | ((uint)channels[2] << 8) | (uint)channels[3];
                hex.Text = "#" + packed.ToString("X8");
            }
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var slider = new Slider { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true, Value = channels[i] };
                controls[i] = slider;
                slider.ValueChanged += (_, e) => { channels[index] = e.NewValue; Refresh(); };
                Add(sliders, Field(names[i], slider));
            }
            Add(sliders, hex);
            if (EditDialog(label, sliders, "Save color", () =>
            {
                uint packed = ((uint)channels[0] << 24) | ((uint)channels[1] << 16) | ((uint)channels[2] << 8) | (uint)channels[3];
                set(unchecked((int)packed)); Changed();
            })) Render();
        });
        return Field(label, Actions(swatch, Text("#" + value.ToString("X8"), 13), button));
    }
}
