using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WGestures.Modern;

internal static class Theme
{
    internal static void Apply(string mode)
    {
        var app = Application.Current;
        // Microsoft marks the code-based ThemeMode API experimental in .NET 10.
        // Keep this opt-in limited to theming; the application remains usable if it fails.
        try
        {
#pragma warning disable WPF0001
            app.ThemeMode = mode switch
            {
                "Light" => ThemeMode.Light, "Dark" => ThemeMode.Dark, _ => ThemeMode.System
            };
#pragma warning restore WPF0001
        }
        catch (Exception ex) { Program.Log("Fluent theme fallback: " + ex); }

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
    }
}
