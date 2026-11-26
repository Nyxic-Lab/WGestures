using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WGestures.Modern.Core;

namespace WGestures.Modern;

public sealed partial class MainWindow : Window
{
    private GestureWorkspace? _workspace;
    private string _source = "No document loaded";
    private bool _dirty;
    private string _page = "overview";
    private string _profile = GestureWorkspace.GlobalKey;
    private string _query = "";
    private bool _ready;
    private readonly string _draftPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nyxic-Lab", "Gestures", "workspace.wg2");
    private string BundledPath => Path.Combine(AppContext.BaseDirectory, "defaults", "gestures.wg2");

    public MainWindow()
    {
        InitializeComponent();
        _ready = true;
        try { _settings = EngineSettings.Read(EngineSettings.PreferencesPath); ApplyTheme(); }
        catch (Exception ex) { Notify("Could not load preferences", ex.Message, InfoBarSeverity.Error); }
        try
        {
            var path = File.Exists(_draftPath) ? _draftPath : BundledPath;
            OpenWorkspace(path);
        }
        catch (Exception e)
        {
            Notify("Could not load a gesture workspace", e.Message, InfoBarSeverity.Error);
        }
        Nav.SelectedItem = Nav.MenuItems[0];
        Render();
    }

    private void OpenWorkspace(string path)
    {
        var loaded = GestureWorkspace.Open(path);
        _workspace = loaded;
        _source = path;
        _dirty = false;
        _profile = GestureWorkspace.GlobalKey;
        _query = "";
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_ready || args.SelectedItemContainer?.Tag is not string section) return;
        _page = section;
        Render();
    }

    private void Render()
    {
        PageHost.Children.Clear();
        (PageTitle.Text, PageDescription.Text) = _page switch
        {
            "gestures" => ("Gestures", "Create, edit and organize mouse gestures."),
            "apps" => ("Applications", "Manage application profiles and inherited gestures."),
            "corners" => ("Corners & edges", "Assign actions to screen corners and edges."),
            "settings" => ("Preferences", "Configure input behavior, gesture feedback and appearance."),
            "about" => ("About", "A community-maintained continuation of the original WGestures."),
            _ => ("Overview", "Make everyday Windows interactions a little faster.")
        };
        UpdateSource();
        if (_page == "settings") { RenderPreferences(); return; }
        if (_page == "about") { RenderAbout(); return; }
        if (_workspace is null)
        {
            PageHost.Children.Add(Heading("Open a gesture configuration", 20));
            PageHost.Children.Add(Paragraph("Choose an original WGestures version-3 .wg2 file to start."));
            PageHost.Children.Add(ImportButton());
            return;
        }

        switch (_page)
        {
            case "gestures": RenderGestures(); break;
            case "apps": RenderApps(); break;
            case "corners": RenderCorners(); break;
            default: RenderOverview(); break;
        }
    }

    private static TextBlock Heading(string text, double size = 18) => new()
    {
        Text = text, FontSize = size, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Opacity = 0.8
    };

    private Border Card(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var child in children) panel.Children.Add(child);
        return new Border { Style = (Style)Root.Resources["Surface"], Child = panel };
    }

    private Button Action(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, MinHeight = 36 };
        if (primary) button.Style = (Style)Root.Resources["PrimaryAction"];
        button.Click += (_, _) => action();
        return button;
    }

    private Button ImportButton()
    {
        var button = new Button { Content = "Import configuration…", MinHeight = 36 };
        button.Click += async (_, _) =>
        {
            if (_dirty)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Root.XamlRoot, Title = "Unsaved draft",
                    Content = "Save your changes before importing, or discard them.",
                    PrimaryButtonText = "Discard and import", CloseButtonText = "Cancel"
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            }
            try
            {
                var picker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                picker.FileTypeFilter.Add(".wg2");
                picker.FileTypeFilter.Add(".json");
                var file = await picker.PickSingleFileAsync();
                if (file is null) return;
                OpenWorkspace(file.Path);
                Render();
                Notify("Configuration loaded", "The original file remains unchanged.", InfoBarSeverity.Success);
            }
            catch (Exception e)
            {
                Notify("Import failed", e.Message, InfoBarSeverity.Error);
            }
        };
        return button;
    }

    private void SaveDraft()
    {
        if (_workspace is null) return;
        try
        {
            _workspace.SaveDraft(_draftPath);
            _settings.Save(EngineSettings.PreferencesPath);
            _source = _draftPath;
            _dirty = false;
            UpdateSource();
            Notify("Draft saved", "The legacy application and its configuration were not modified.", InfoBarSeverity.Success);
        }
        catch (Exception e) { Notify("Save failed", e.Message, InfoBarSeverity.Error); }
    }

    private void UpdateSource()
    {
        SourceText.Text = (_dirty ? "Unsaved edits · " : "Workspace · ") + _source;
        EngineStatus.Text = EngineReady() ? "Engine running" : EngineRunning() ? "Engine starting / stopping" : "Engine stopped";
    }

    private void Notify(string title, string message, InfoBarSeverity severity)
    {
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private void Navigate(string key)
    {
        var item = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => Equals(i.Tag, key));
        if (item is not null)
        {
            if (ReferenceEquals(Nav.SelectedItem, item)) Render();
            else Nav.SelectedItem = item;
        }
    }

    private Button ExportButton() => AsyncAction("Export configuration…", async () =>
    {
        if (_workspace is null) return;
        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeChoices.Add("WGestures configuration", new List<string> { ".wg2" });
        picker.SuggestedFileName = "Nyxic-gestures";
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        if (string.Equals(file.Path, EngineSettings.ActivePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use Apply & start to change the active configuration.");
        _workspace.Export(file.Path);
        Notify("Configuration exported", file.Path, InfoBarSeverity.Success);
    });

    private void RenderOverview()
    {
        var profiles = _workspace!.Profiles;
        var global = profiles.Single(p => p.Key == GestureWorkspace.GlobalKey);
        var metrics = new Grid { ColumnSpacing = 10 };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        var cards = new[]
        {
            Card(Heading(global.Gestures.Count.ToString(), 28), Paragraph("Global gestures")),
            Card(Heading((profiles.Count - 1).ToString(), 28), Paragraph("App profiles")),
            Card(Heading(profiles.Sum(p => p.Gestures.Count).ToString(), 28), Paragraph("Total gestures"))
        };
        for (int i = 0; i < cards.Length; i++)
        {
            Grid.SetColumn(cards[i], i);
            metrics.Children.Add(cards[i]);
        }
        PageHost.Children.Add(metrics);
        PageHost.Children.Add(EngineCard());
        PageHost.Children.Add(Card(Heading("Your gestures"), Paragraph("Browse global gestures and per-application assignments."),
            Action("Open gesture library", () => Navigate("gestures"), true)));
        PageHost.Children.Add(Card(Heading("Configuration"), Paragraph("Import original WGestures version-3 files or export your configuration."),
            ImportButton(), ExportButton(), Action("Save workspace", SaveDraft)));
    }

    private void RenderGestures()
    {
        var profiles = _workspace!.Profiles;
        var picker = new ComboBox { Header = "Profile", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var p in profiles) picker.Items.Add(new ComboBoxItem { Content = p.Name + $"  ({p.Gestures.Count})", Tag = p.Key });
        picker.SelectedIndex = Math.Max(0, profiles.ToList().FindIndex(p => p.Key == _profile));
        _profile = profiles[picker.SelectedIndex].Key;
        var search = new TextBox { Header = "Search gestures", PlaceholderText = "Name, direction, action…", Text = _query };
        var list = new ListView { ItemTemplate = (DataTemplate)Root.Resources["GestureRowTemplate"],
            SelectionMode = ListViewSelectionMode.Single, MinHeight = 180, MaxHeight = 440,
            HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(list, "Gesture library");
        var results = Paragraph("");
        var editor = new StackPanel { Spacing = 12 };
        var editorCard = Card(Heading("Gesture details"), editor);
        var add = Action("New gesture", () => { list.SelectedItem = null; ShowGestureEditor(editor, null, Refresh); }, true);
        var listCard = Card(picker, search, add, results, list);
        var columns = new Grid { ColumnSpacing = 14, RowSpacing = 14 };
        columns.Children.Add(listCard);
        columns.Children.Add(editorCard);
        void Layout(double width)
        {
            columns.ColumnDefinitions.Clear();
            columns.RowDefinitions.Clear();
            if (width >= 750)
            {
                columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
                Grid.SetColumn(editorCard, 1); Grid.SetRow(editorCard, 0);
            }
            else
            {
                columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(editorCard, 0); Grid.SetRow(editorCard, 1);
            }
        }
        Layout(PageHost.ActualWidth - 56);
        columns.SizeChanged += (_, e) =>
        {
            if ((e.NewSize.Width >= 750) != (columns.ColumnDefinitions.Count == 2)) Layout(e.NewSize.Width);
        };
        PageHost.Children.Add(columns);
        PageHost.Children.Add(Card(Heading("Configuration"), Action("Save workspace", SaveDraft), ExportButton(),
            AsyncAction("Apply & start / reload", ApplyAndStartAsync, true)));

        void Refresh(int? selectIndex)
        {
            var profile = _workspace!.Profiles.First(p => p.Key == _profile);
            var rows = profile.Gestures.Where(r => string.IsNullOrWhiteSpace(_query) ||
                (r.Name + " " + r.Summary + " " + r.ModifierLabel).Contains(_query, StringComparison.OrdinalIgnoreCase)).ToArray();
            list.ItemsSource = rows;
            results.Text = $"{rows.Length} matching gestures";
            if (picker.SelectedItem is ComboBoxItem item) item.Content = profile.Name + $"  ({profile.Gestures.Count})";
            if (rows.Length > 0) list.SelectedItem = rows.FirstOrDefault(r => r.Index == selectIndex) ?? rows[0];
            else { editor.Children.Clear(); editor.Children.Add(Paragraph("No matching gestures. Select New gesture to add one.")); }
        }
        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedItem is ComboBoxItem { Tag: string key }) { _profile = key; Refresh(null); }
        };
        search.TextChanged += (_, _) => { _query = search.Text; Refresh(null); };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is GestureRow row) ShowGestureEditor(editor, row, Refresh);
            else { editor.Children.Clear(); editor.Children.Add(Paragraph("Select or create a gesture.")); }
        };
        Refresh(null);
    }

    private void RenderApps()
    {
        PageHost.Children.Add(AsyncAction("Add application…", async () =>
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".exe");
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var key = _workspace!.AddProfile(file.Path, Path.GetFileNameWithoutExtension(file.Name));
            Changed(); _profile = key; Render();
            Notify("Application added", file.Name, InfoBarSeverity.Success);
        }, true));
        foreach (var profile in _workspace!.Profiles)
        {
            var key = profile.Key;
            var name = new TextBox { Header = "Profile name", Text = profile.Name, IsEnabled = key != GestureWorkspace.GlobalKey };
            var enabled = new ToggleSwitch { Header = "Enable gestures for this profile", IsOn = profile.Enabled };
            var inherits = new ToggleSwitch { Header = "Inherit global gestures", IsOn = profile.InheritsGlobal, IsEnabled = key != GestureWorkspace.GlobalKey };
            var children = new List<UIElement> {
                Heading(profile.Name), Paragraph(profile.ExecutablePath ?? "Shared default profile"),
                Paragraph($"{profile.Gestures.Count} gestures"), name, enabled, inherits,
                Action("Save profile", () => { try { _workspace.UpdateProfile(key, name.Text, enabled.IsOn, inherits.IsOn); Changed(); Render(); } catch (Exception e) { Notify("Invalid profile", e.Message, InfoBarSeverity.Error); } }),
                Action("View gestures", () => { _profile = key; _query = ""; Navigate("gestures"); })
            };
            if (key != GestureWorkspace.GlobalKey)
                children.Add(AsyncAction("Remove profile", async () =>
                {
                    if (!await ConfirmAsync("Remove application profile", $"Remove {profile.Name} and its custom gestures?", "Remove")) return;
                    _workspace.DeleteProfile(key);
                    if (_profile == key) _profile = GestureWorkspace.GlobalKey;
                    Changed(); Render();
                }));
            PageHost.Children.Add(Card(children.ToArray()));
        }
    }

    private void RenderCorners()
    {
        PageHost.Children.Add(Paragraph("Enable hot corners or rub edges in Preferences before activating their actions."));
        var slots = new ComboBox { Header = "Screen location", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var slot in _workspace!.Corners) slots.Items.Add(slot.Name + " · " + slot.Command);
        var host = new StackPanel { Spacing = 10 };
        void Edit()
        {
            host.Children.Clear();
            int index = slots.SelectedIndex;
            if (index < 0) return;
            var editor = CreateCommandEditor(_workspace.GetCornerCommand(index));
            host.Children.Add(editor.View);
            host.Children.Add(Action("Save action", () =>
            {
                try
                {
                    _workspace.SetCornerCommand(index, editor.Read());
                    Changed();
                    Notify("Action saved", "Apply configuration to activate changes.", InfoBarSeverity.Success);
                }
                catch (Exception e) { Notify("Invalid corner action", e.Message, InfoBarSeverity.Error); }
            }, true));
        }
        slots.SelectionChanged += (_, _) => Edit();
        slots.SelectedIndex = 0;
        PageHost.Children.Add(Card(slots, host));
        PageHost.Children.Add(AsyncAction("Apply & start / reload", ApplyAndStartAsync, true));
    }

    private void RenderPreferences()
    {
        var theme = new ComboBox { Header = "Appearance", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var option in new[] { "System", "Light", "Dark" }) theme.Items.Add(option);
        theme.SelectedItem = _settings.Theme;
        theme.SelectionChanged += (_, _) =>
        {
            if (theme.SelectedItem is string selection) { _settings = _settings with { Theme = selection }; ApplyTheme(); Changed(); }
        };
        ToggleSwitch Toggle(string label, bool value, Action<bool> set)
        {
            var toggle = new ToggleSwitch { Header = label, IsOn = value };
            toggle.Toggled += (_, _) => { _settings = _settings with { }; set(toggle.IsOn); Changed(); };
            return toggle;
        }
        var buttons = new StackPanel { Spacing = 6 };
        foreach (var (bit, label) in new[] { (1, "Right button"), (2, "Middle button"), (4, "X1"), (8, "X2") })
        {
            var check = new CheckBox { Content = label, IsChecked = (_settings.TriggerButtons & bit) != 0 };
            check.Checked += (_, _) => { _settings = _settings with { TriggerButtons = _settings.TriggerButtons | bit }; Changed(); };
            check.Unchecked += (_, _) => { _settings = _settings with { TriggerButtons = _settings.TriggerButtons & ~bit }; Changed(); };
            buttons.Children.Add(check);
        }
        var initialMs = new NumberBox { Header = "Initial stay timeout (ms)", Minimum = 50, Maximum = 3000, Value = _settings.InitialStayMilliseconds, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        initialMs.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue)) { _settings = _settings with { InitialStayMilliseconds = (int)e.NewValue }; Changed(); } };
        var stayMs = new NumberBox { Header = "Gesture stay timeout (ms)", Minimum = 50, Maximum = 10000, Value = _settings.StayMilliseconds, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        stayMs.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue)) { _settings = _settings with { StayMilliseconds = (int)e.NewValue }; Changed(); } };
        PageHost.Children.Add(Card(Heading("Appearance"), theme,
            Toggle("Show gesture trail", _settings.ShowPath, v => _settings = _settings with { ShowPath = v }),
            Toggle("Show action name", _settings.ShowCommandName, v => _settings = _settings with { ShowCommandName = v }),
            Toggle("Fade out gesture trail", _settings.FadeOut, v => _settings = _settings with { FadeOut = v })));
        PageHost.Children.Add(Card(Heading("Input"), Paragraph("Trigger buttons"), buttons,
            Toggle("Eight directions", _settings.EightDirections, v => _settings = _settings with { EightDirections = v }),
            Toggle("Disable in fullscreen", _settings.DisableFullscreen, v => _settings = _settings with { DisableFullscreen = v }),
            Toggle("Target window under cursor", _settings.PreferCursorWindow, v => _settings = _settings with { PreferCursorWindow = v }),
            Toggle("Enable Windows-key gestures", _settings.WindowsKeyGestures, v => _settings = _settings with { WindowsKeyGestures = v }),
            Toggle("Initial stay timeout", _settings.InitialStayTimeout, v => _settings = _settings with { InitialStayTimeout = v }), initialMs,
            Toggle("Stay timeout", _settings.StayTimeout, v => _settings = _settings with { StayTimeout = v }), stayMs));
        PageHost.Children.Add(Card(Heading("Screen interactions"),
            Toggle("Hot corners", _settings.HotCorners, v => _settings = _settings with { HotCorners = v }),
            Toggle("Rub edges", _settings.RubEdges, v => _settings = _settings with { RubEdges = v })));
        PageHost.Children.Add(Card(Heading("Script actions"), Paragraph("Only enable command-line and Lua actions for configurations you trust."),
            Toggle("Allow command-line and Lua actions", _settings.AllowScripts, v => _settings = _settings with { AllowScripts = v })));
        PageHost.Children.Add(Card(Heading("Save preferences"), Action("Save workspace", SaveDraft), AsyncAction("Apply & start / reload", ApplyAndStartAsync, true)));
    }

    private void RenderAbout()
    {
        PageHost.Children.Add(Card(Heading("Nyxic Gestures", 24), Paragraph("Independent Windows gesture manager"),
            Paragraph("A continuation of WGestures by YingDev, under the original GPL-2.0 license."),
            Paragraph(".NET 10 · WinUI 3 · Windows App SDK 2.7.0"),
            new HyperlinkButton { Content = "Project source", NavigateUri = new Uri("https://github.com/Nyxic-Lab/WGestures") },
            new HyperlinkButton { Content = "Original project", NavigateUri = new Uri("https://github.com/yingDev/WGestures") }));
    }
}
