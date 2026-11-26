using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WGestures.Modern.Core;

namespace WGestures.Modern;

internal static class Theme
{
    internal static WindowsCompatibilityProfile Profile { get; private set; } = WindowsCompatibility.Current();
    private static bool _fluentFailed;
    private static ResourceDictionary? _designStyles;
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

        // The same application-owned controls and palette on both Windows versions.
        // OS-specific theming only affects system chrome and controls we leave native.
        Set("FieldBackground", dark ? "#181D26" : "#F9FAFC", SystemColors.WindowBrush);
        Set("FieldText", dark ? "#F0F3F8" : "#1B2433", SystemColors.WindowTextBrush);
        Set("FieldBorder", dark ? "#485365" : "#CCD5E2", SystemColors.ActiveBorderBrush);
        Set("HoverSurface", dark ? "#2B3545" : "#E9EFF9", SystemColors.WindowBrush);
        Set("SelectedSurface", dark ? "#293E5D" : "#E2ECFF", SystemColors.WindowBrush);
        Set("AccentSoft", dark ? "#233952" : "#E7EFFF", SystemColors.WindowBrush);
        Set("SuccessBrush", dark ? "#8CE0B1" : "#18774A", SystemColors.WindowTextBrush);
        Set("ErrorBrush", dark ? "#FFA8A8" : "#B42332", SystemColors.WindowTextBrush);
        Set("SwitchOff", dark ? "#626D7E" : "#A3ADBD", SystemColors.ControlDarkBrush);
        Set("SwitchThumb", "#FFFFFF", SystemColors.WindowBrush);
        _designStyles ??= BuildDesignStyles();
        if (!app.Resources.MergedDictionaries.Contains(_designStyles))
            app.Resources.MergedDictionaries.Add(_designStyles);
        Program.Log("Shared Nyxic design system enabled (" + CurrentMode + ").");
    }

    private static ResourceDictionary BuildDesignStyles()
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
        static void Focus(Style style)
        {
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBrush")));
            style.Triggers.Add(focus);
        }

        var text = StyleFor(typeof(TextBox));
        Set(text, Control.BackgroundProperty, "FieldBackground");
        Set(text, Control.ForegroundProperty, "FieldText");
        Set(text, Control.BorderBrushProperty, "FieldBorder");
        Set(text, TextBoxBase.CaretBrushProperty, "FieldText");
        Set(text, TextBoxBase.SelectionBrushProperty, "AccentBrush");
        text.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        text.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(11, 8, 11, 8)));
        text.Setters.Add(new Setter(Control.TemplateProperty, TextField()));
        Focus(text);

        var combo = StyleFor(typeof(ComboBox));
        Set(combo, Control.BackgroundProperty, "FieldBackground");
        Set(combo, Control.ForegroundProperty, "FieldText");
        Set(combo, Control.BorderBrushProperty, "FieldBorder");
        combo.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(11, 6, 30, 6)));
        combo.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        combo.Setters.Add(new Setter(Control.TemplateProperty, ComboField()));
        Focus(combo);

        var comboItem = StyleFor(typeof(ComboBoxItem));
        Set(comboItem, Control.ForegroundProperty, "FieldText");
        comboItem.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        comboItem.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
        comboItem.Setters.Add(new Setter(Control.MarginProperty, new Thickness(3, 1, 3, 1)));
        comboItem.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(ComboBoxItem), new CornerRadius(6), false)));
        var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HoverSurface")));
        comboItem.Triggers.Add(highlighted);
        var comboSelected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        comboSelected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("SelectedSurface")));
        comboItem.Triggers.Add(comboSelected);

        var check = StyleFor(typeof(CheckBox));
        Set(check, Control.ForegroundProperty, "MainText");
        check.Setters.Add(new Setter(Control.TemplateProperty, ToggleSwitch()));
        check.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 34.0));

        var button = StyleFor(typeof(Button));
        Set(button, Control.BackgroundProperty, "CardBackground");
        Set(button, Control.ForegroundProperty, "MainText");
        Set(button, Control.BorderBrushProperty, "FieldBorder");
        button.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        button.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(Button), new CornerRadius(8), true)));

        var nav = StyleFor(typeof(ListBoxItem));
        Set(nav, Control.ForegroundProperty, "MainText");
        nav.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        nav.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        nav.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        nav.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 5, 9, 5)));
        nav.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 2, 0, 2)));
        nav.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        nav.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(ListBoxItem), new CornerRadius(8), false)));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HoverSurface")));
        nav.Triggers.Add(hover);
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("SelectedSurface")));
        selected.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("AccentBrush")));
        nav.Triggers.Add(selected);
        return result;
    }

    private static FrameworkElementFactory BorderFactory(string name, double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border), name);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        return border;
    }

    private static ControlTemplate TextField()
    {
        var border = BorderFactory("Field", 7);
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(UIElement.FocusableProperty, false);
        host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
            new TemplateBindingExtension(ScrollViewer.HorizontalScrollBarVisibilityProperty));
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,
            new TemplateBindingExtension(ScrollViewer.VerticalScrollBarVisibilityProperty));
        border.AppendChild(host);
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .55, "Field"));
        template.Triggers.Add(disabled);
        return template;
    }

    private static ControlTemplate ComboField()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var toggle = new FrameworkElementFactory(typeof(ToggleButton), "DropToggle");
        var toggleStyle = new Style(typeof(ToggleButton));
        toggleStyle.Setters.Add(new Setter(Control.TemplateProperty, Chrome(typeof(ToggleButton), new CornerRadius(7), true)));
        toggle.SetValue(FrameworkElement.StyleProperty, toggleStyle);
        toggle.SetValue(Control.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        toggle.SetValue(Control.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        toggle.SetValue(Control.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        toggle.SetValue(UIElement.FocusableProperty, false);
        toggle.SetValue(ButtonBase.ClickModeProperty, ClickMode.Press);
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ComboBox.IsDropDownOpen))
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
        root.AppendChild(toggle);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
        content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
        content.SetValue(ContentPresenter.ContentTemplateSelectorProperty, new TemplateBindingExtension(ItemsControl.ItemTemplateSelectorProperty));
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(11, 4, 30, 4));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(UIElement.IsHitTestVisibleProperty, false);
        root.AppendChild(content);
        var arrow = new FrameworkElementFactory(typeof(TextBlock));
        arrow.SetValue(TextBlock.TextProperty, "⌄");
        arrow.SetValue(TextBlock.FontSizeProperty, 17.0);
        arrow.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
        arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 11, 3));
        arrow.SetValue(UIElement.IsHitTestVisibleProperty, false);
        root.AppendChild(arrow);
        var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(ComboBox.IsDropDownOpen))
        { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay });
        popup.SetBinding(Popup.PlacementTargetProperty, new Binding { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        var drop = new FrameworkElementFactory(typeof(Border));
        drop.SetResourceReference(Border.BackgroundProperty, "CardBackground");
        drop.SetResourceReference(Border.BorderBrushProperty, "FieldBorder");
        drop.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        drop.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        drop.SetValue(Border.PaddingProperty, new Thickness(3));
        drop.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 0));
        drop.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        drop.SetBinding(FrameworkElement.MaxHeightProperty, new Binding(nameof(ComboBox.MaxDropDownHeight)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        presenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Contained);
        scroll.AppendChild(presenter); drop.AppendChild(scroll); popup.AppendChild(drop); root.AppendChild(popup);
        var template = new ControlTemplate(typeof(ComboBox)) { VisualTree = root };
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .55, "DropToggle"));
        template.Triggers.Add(disabled);
        return template;
    }

    private static ControlTemplate ToggleSwitch()
    {
        var root = new FrameworkElementFactory(typeof(DockPanel), "SwitchRoot");
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var track = new FrameworkElementFactory(typeof(Border), "Track");
        track.SetValue(DockPanel.DockProperty, Dock.Right);
        track.SetValue(FrameworkElement.WidthProperty, 40.0);
        track.SetValue(FrameworkElement.HeightProperty, 22.0);
        track.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        track.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 2, 0));
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        track.SetResourceReference(Border.BackgroundProperty, "SwitchOff");
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        track.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        var thumb = new FrameworkElementFactory(typeof(Border), "Thumb");
        thumb.SetValue(FrameworkElement.WidthProperty, 16.0);
        thumb.SetValue(FrameworkElement.HeightProperty, 16.0);
        thumb.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        thumb.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        thumb.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        thumb.SetResourceReference(Border.BackgroundProperty, "SwitchThumb");
        track.AppendChild(thumb); root.AppendChild(track);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(content);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = root };
        var active = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        active.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentBrush"), "Track"));
        active.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Thumb"));
        template.Triggers.Add(active);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("MainText"), "Track"));
        template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .55, "SwitchRoot"));
        template.Triggers.Add(disabled);
        return template;
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
