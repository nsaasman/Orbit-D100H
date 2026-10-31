using System.Globalization;
using System.Resources;

namespace OrbitD100H.Core;

public static class Text
{
    public static ResourceManager Resources { get; } = new("OrbitD100H.Core.Resources.Strings", typeof(Text).Assembly);
    private static CultureInfo culture = CultureInfo.GetCultureInfo("en");
    public static CultureInfo Culture => Volatile.Read(ref culture);
    public static string ResolveLanguage(string? preference, string? windowsLanguage)
    {
        if (preference is "nl" or "en" or "de" or "fr" or "es") return preference;
        var language = windowsLanguage?.Split('-', '_')[0].ToLowerInvariant();
        return language is "nl" or "en" or "de" or "fr" or "es" ? language : "en";
    }
    public static void SetLanguage(string language) =>
        Volatile.Write(ref culture, CultureInfo.GetCultureInfo(ResolveLanguage(language, null)));
    public static string Get(string key, params object[] arguments)
    {
        var current = Culture;
        var format = Resources.GetString(key, current) ?? throw new MissingManifestResourceException(key);
        return string.Format(current, format, arguments);
    }
    public static T Describe<T>(T exception, string key, params object[] arguments) where T : Exception
    {
        exception.Data[typeof(LocalizedText)] = new LocalizedText(key, arguments);
        return exception;
    }
    public static LocalizedText Error(Exception exception) =>
        exception.Data[typeof(LocalizedText)] as LocalizedText ?? new("Raw", exception.Message);
}

public sealed record LocalizedText(string Key, params object[] Arguments)
{
    public override string ToString() => Text.Get(Key, Arguments);
}
