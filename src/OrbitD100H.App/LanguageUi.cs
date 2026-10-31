using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using OrbitD100H.Core;

namespace OrbitD100H.App;

internal static class LanguageUi
{
    private const string PreferencePath = @"Software\D100HControl";
    internal static event Action? Changed;

    internal static LocalizedText? Initialize()
    {
        string? preference = null;
        LocalizedText? error = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PreferencePath);
            preference = key?.GetValue("Language") as string;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { error = new("LanguageReadError", Text.Error(e)); }
        Text.SetLanguage(Text.ResolveLanguage(preference, CultureInfo.CurrentUICulture.Name));
        ApplyResources();
        return error;
    }

    internal static LocalizedText? Change(string language)
    {
        Text.SetLanguage(language);
        ApplyResources();
        Changed?.Invoke();
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PreferencePath, writable: true);
            key.SetValue("Language", Text.Culture.Name, RegistryValueKind.String);
            return null;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { return new("LanguageSaveError", Text.Error(e)); }
    }

    internal static void ApplyResources()
    {
        var resources = Text.Resources.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
        foreach (System.Collections.DictionaryEntry item in resources)
            Application.Current.Resources["L_" + (string)item.Key] = Text.Resources.GetString((string)item.Key, Text.Culture)!;
    }
}
