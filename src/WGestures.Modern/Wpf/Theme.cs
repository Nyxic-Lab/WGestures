using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using WGestures.Modern.Core;

namespace WGestures.Modern;

internal static class Theme
{
    internal static WindowsCompatibilityProfile Profile { get; private set; } = WindowsCompatibility.Current();
    private static bool _fluentFailed;
    private static ResourceDictionary? _classicStyles;
    internal static bool IsClassic => !Profile.UseFluent || _fluentFailed;
    internal static string CurrentMode => IsClassic ? "Classic WPF compatibility" : "Fluent";

    internal static void Configure(bool forceCompatibility)
    {
        Profile = WindowsCompatibility.Current(forceCompatibility);
        _fluentFailed = false;
        Program.Log($"Windows {Profile.WindowsVersion}; selected UI mode: {Profile.UiMode}; forced={forceCompatibility}");
    }

    internal static void Apply(string mode)
    {
        var app = Application.Current;
        // Never activate the experimental Fluent ThemeMode API on Windows 10.
        // This avoids the modern window/effect path, while retaining WPF's native controls.
        if (!IsClassic)
        {
            try
            {
#pragma warning disable WPF0001
                app.ThemeMode = mode switch
                {
                    "Light" => ThemeMode.Light, "Dark" => ThemeMode.Dark, _ => ThemeMode.System
                };
#pragma warning restore WPF0001
            }
            catch (Exception ex)
            {
                _fluentFailed = true;
                Program.Log("Fluent theme fallback: " + ex);
                try
                {
#pragma warning disable WPF0001
                    app.ThemeMode = ThemeMode.None;
#pragma warning restore WPF0001
                }
                catch (Exception resetError) { Program.Log("Theme reset: " + resetError); }
            }
        }

        bool dark = mode == "Dark";
        if (mode == "System")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch { dark = false; }
        }
        void Set(string name, string hex, Brush? highContrast = null)
        {
            var brush = SystemParameters.HighContrast && highContrast is not null
                ? highContrast : new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            app.Resources[name] = brush;
        }
        Set("ShellBackground", dark ? "#14171C" : "#F4F6F9", SystemColors.WindowBrush);
        Set("NavBackground", dark ? "#1C2027" : "#EAEFF5", SystemColors.WindowBrush);
        Set("CardBackground", dark ? "#20252D" : "#FFFFFF", SystemColors.WindowBrush);
        Set("CardBorder", dark ? "#383F4B" : "#DCE2EA", SystemColors.ActiveBorderBrush);
        Set("MainText", dark ? "#F0F3F8" : "#1B2433", SystemColors.WindowTextBrush);
        Set("MutedText", dark ? "#B4BECC" : "#556274", SystemColors.WindowTextBrush);
        Set("AccentBrush", dark ? "#90B2FF" : "#2858C4", SystemColors.HighlightBrush);
        Set("AccentText", dark ? "#12213A" : "#FFFFFF", SystemColors.HighlightTextBrush);
        Set("PadBackground", dark ? "#182235" : "#ECF2FF", SystemColors.WindowBrush);

        if (IsClassic)
        {
            // Keep system-native input/list templates on a light, high-contrast field
            // surface even in dark shell mode; old Aero templates contain fixed colors.
            app.Resources["CompatFieldBackground"] = SystemColors.WindowBrush;
            app.Resources["CompatFieldText"] = SystemColors.WindowTextBrush;
            app.Resources["CompatFieldBorder"] = SystemColors.ActiveBorderBrush;
            Set("CompatNavHover", dark ? "#292F3A" : "#E0E7F1", SystemColors.WindowBrush);
            Set("CompatNavSelected", dark ? "#344765" : "#D7E5FA", SystemColors.WindowBrush);
            if (_classicStyles is null)
            {
                _classicStyles = BuildClassicStyles();
                app.Resources.MergedDictionaries.Add(_classicStyles);
                Program.Log("Classic WPF controls enabled.");
            }
        }
    }

    private static ResourceDictionary BuildClassicStyles()
    {
        var result = new ResourceDictionary();
        Style StyleFor(Type type)
        {
            var style = new Style(type);
            result.Add(type, style);
            return style;
        }
        static void Set(Style style, DependencyProperty property, string resource) =>
            style.Setters.Add(new Setter(property, new DynamicResourceExtension(resource)));
        void FieldStyle(Type type)
        {
            var style = StyleFor(type);
            Set(style, Control.BackgroundProperty, "CompatFieldBackground");
            Set(style, Control.ForegroundProperty, "CompatFieldText");
            Set(style, Control.BorderBrushProperty, "CompatFieldBorder");
        }

        FieldStyle(typeof(TextBox));
        FieldStyle(typeof(ComboBox));
        var comboItem = StyleFor(typeof(ComboBoxItem));
        Set(comboItem, Control.ForegroundProperty, "CompatFieldText");
        var comboSelected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        comboSelected.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(SystemColors.HighlightTextBrushKey)));
        comboSelected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension(SystemColors.HighlightBrushKey)));
        comboItem.Triggers.Add(comboSelected);

        var grid = StyleFor(typeof(DataGrid));
        Set(grid, Control.BackgroundProperty, "CompatFieldBackground");
        Set(grid, Control.ForegroundProperty, "CompatFieldText");
        Set(grid, DataGrid.RowBackgroundProperty, "CompatFieldBackground");
        Set(grid, DataGrid.AlternatingRowBackgroundProperty, "CompatFieldBackground");
        var header = StyleFor(typeof(DataGridColumnHeader));
        Set(header, Control.ForegroundProperty, "CompatFieldText");
        var check = StyleFor(typeof(CheckBox));
        Set(check, Control.ForegroundProperty, "MainText");

        // Lightweight templates for chrome we own. No DWM, Mica or Windows App SDK.
        var button = StyleFor(typeof(Button));
        Set(button, Control.BackgroundProperty, "CardBackground");
        Set(button, Control.ForegroundProperty, "MainText");
        Set(button, Control.BorderBrushProperty, "CardBorder");
        button.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        button.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(Button), new CornerRadius(6), true)));

        var nav = StyleFor(typeof(ListBoxItem));
        Set(nav, Control.ForegroundProperty, "MainText");
        nav.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        nav.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        nav.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        nav.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(ListBoxItem), new CornerRadius(6), false)));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CompatNavHover")));
        nav.Triggers.Add(hover);
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CompatNavSelected")));
        selected.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBrush")));
        nav.Triggers.Add(selected);
        return result;
    }

    private static ControlTemplate Chrome(Type type, CornerRadius radius, bool isButton)
    {
        var border = new FrameworkElementFactory(typeof(Border), "Chrome");
        border.SetValue(Border.CornerRadiusProperty, radius);
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, isButton ? HorizontalAlignment.Center : HorizontalAlignment.Stretch);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        var template = new ControlTemplate(type) { VisualTree = border };
        if (isButton)
        {
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, .86, "Chrome"));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .72, "Chrome"));
            template.Triggers.Add(pressed);
        }
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .55, "Chrome"));
        template.Triggers.Add(disabled);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("AccentBrush"), "Chrome"));
        template.Triggers.Add(focus);
        return template;
    }
}
