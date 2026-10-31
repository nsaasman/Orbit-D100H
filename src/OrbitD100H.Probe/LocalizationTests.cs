using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OrbitD100H.App;
using OrbitD100H.Core;

internal static class LocalizationTests
{
    internal static int Run()
    {
        var checks = 0;
        void Assert(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Test mislukt: " + description);
            checks++;
        }
        var previous = Text.Culture.Name;
        try
        {
            var neutral = Text.Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
            var keys = neutral.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
            foreach (var language in new[] { "nl", "en", "de", "fr", "es" })
            {
                Text.SetLanguage(language);
                var set = language == "en" ? neutral : Text.Resources.GetResourceSet(Text.Culture, true, false)!;
                Assert(set is not null && keys.SequenceEqual(set.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order()),
                    "volledige resourceverzameling: " + language);
                foreach (var key in keys)
                {
                    var value = set!.GetString(key)!;
                    var format = CompositeFormat.Parse(value);
                    Assert(!string.IsNullOrWhiteSpace(value) && format.MinimumArgumentCount == CompositeFormat.Parse(neutral.GetString(key)!).MinimumArgumentCount,
                        "vertaalde tekst en placeholders: " + language + "/" + key);
                    static string[] Placeholders(string text) => Regex.Matches(text, @"\{(\d+)(?:[,}:])")
                        .Select(m => m.Groups[1].Value).Distinct().Order().ToArray();
                    Assert(Placeholders(value).SequenceEqual(Placeholders(neutral.GetString(key)!)), "dezelfde placeholderindices: " + language + "/" + key);
                    _ = Text.Get(key, Enumerable.Repeat<object>(42, format.MinimumArgumentCount).ToArray());
                }
                Assert(Text.ResolveLanguage(null, language + "-XX") == language, "regionale Windows-taal: " + language);
                Assert(Text.ResolveLanguage(language, "it-IT") == language, "expliciete voorkeur: " + language);
            }
            Assert(Text.ResolveLanguage(null, "it-IT") == "en", "onbekende Windows-taal valt terug op Engels");
            Assert(Text.ResolveLanguage("invalid", "fr-CA") == "fr", "ongeldige voorkeur volgt Windows");
            Assert(Text.ResolveLanguage(null, null) == "en", "ontbrekende talen vallen terug op Engels");
            Text.SetLanguage("unsupported");
            Assert(Text.Culture.Name == "en" && Text.Get("Save") == "Save settings", "Engelse terugvaltekst");

            WindowsInput.Reset();
            var settings = new Settings();
            var controller = new Controller(settings);
            controller.Connection(true, new("HapticUnconfirmed"));
            controller.Receive(new(EventKind.Rotate, Direction: 1));
            controller.Pause(true);
            var error = Text.Describe(new InvalidDataException(), "InvalidButton", 3);
            var retained = new LocalizedText("SettingsLoadError", Text.Error(error), @"C:\settings.json");
            Text.SetLanguage("nl");
            var dutch = controller.Status;
            var dutchEvent = controller.LastEvent;
            var dutchError = retained.ToString();
            Text.SetLanguage("de");
            Assert(controller.Status != dutch && controller.Status.Contains(Text.Get("HapticUnconfirmed")), "bestaande gepauzeerde verbinding wisselt mee");
            Assert(controller.LastEvent != dutchEvent && controller.LastEvent == Text.Get("RotateClockwise"), "laatste gebeurtenis wisselt zonder nieuwe invoer");
            Assert(controller.ActiveProfile == Text.Get("General") && settings.General.Name == "Algemeen", "algemene profielnaam vertaalt zonder opgeslagen data te wijzigen");
            Assert(retained.ToString() != dutchError && retained.ToString().Contains(Text.Get("InvalidButton", 3)), "opgeslagen fout en geneste oorzaak wisselen mee");
            Assert(controller.IsPaused, "taalwissel bewaart pauzestand");
            controller.Connection(false, new("ConnectionInterrupted", new LocalizedText("NoKeepalive")));
            Text.SetLanguage("es");
            Assert(controller.Status.Contains(Text.Get("NoKeepalive")), "verbindingsverlies en oorzaak wisselen mee");
            controller.DisposeAsync().AsTask().GetAwaiter().GetResult();
            WindowsInput.Reset();
        }
        finally { Text.SetLanguage(previous); }
        return checks;
    }
}
