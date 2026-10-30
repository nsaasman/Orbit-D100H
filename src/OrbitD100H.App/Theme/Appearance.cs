using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace OrbitD100H.App.Theme;

internal static class Appearance
{
    // Keep the existing key so the product rename preserves the theme preference.
    private const string PreferencePath = @"Software\D100HControl";
    private const string WindowsThemePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static bool IsDark;

    public static bool Initialize(out string? error)
    {
        var light = WindowsUsesLightTheme(out error);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PreferencePath);
            if (key?.GetValue("DarkMode") is int preference && preference is 0 or 1)
            { light = preference == 0; error = null; }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { error = "Themapreferentie kan niet worden gelezen: " + e.Message; }
        Apply(light);
        return !light;
    }

    public static string? SetDark(bool dark)
    {
        Apply(!dark);
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PreferencePath, writable: true);
            key.SetValue("DarkMode", dark ? 1 : 0, RegistryValueKind.DWord);
            return null;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { return "Thema kan niet worden onthouden: " + e.Message; }
    }

    public static void ApplyWindowTheme(Window window)
    {
        try
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (handle != 0)
            {
                var dark = IsDark ? 1 : 0;
                _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
                _ = DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static void Apply(bool light)
    {
        IsDark = !light;
        var values = light
            ? new[] { "#F4F7F8", "#FFFFFF", "#17232B", "#53636E", "#C8D3D9", "#FFFFFF", "#007F96", "#FFFFFF", "#D7F3F6", "#FFF1C7", "#25343D", "#34464F", "#17232B", "#60747E", "#FFFFFF", "#D7F3F6", "#3D9BAC" }
            : new[] { "#172027", "#202B33", "#EDF4F6", "#B0C0C8", "#4B5B64", "#18232A", "#41C7D9", "#09272D", "#2B4851", "#594A24", "#10191E", "#1B2930", "#263942", "#6A808A", "#F4FAFB", "#153C43", "#41C7D9" };
        var keys = new[] { "WindowBackground", "Surface", "Ink", "Muted", "Border", "InputBackground", "Accent", "AccentText", "Selection", "Banner", "DeviceCase", "DeviceInset", "DeviceButton", "DeviceStroke", "DeviceText", "SavedBackground", "SavedBorder" };
        for (var i = 0; i < keys.Length; i++)
            Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
    }

    private static bool WindowsUsesLightTheme(out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsThemePath);
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { error = "Windows-thema kan niet worden gelezen; licht thema gebruikt. " + e.Message; return true; }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
