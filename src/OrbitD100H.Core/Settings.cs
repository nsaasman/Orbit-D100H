using System.Text.Json;

namespace OrbitD100H.Core;

public enum ScrollMode { Steps, Smooth }
public enum DialMode { Scroll = 0, Volume = 1 }
public enum ActionKind { None, Shortcut, PlayPause, VolumeUp, VolumeDown, Mute }

public sealed class ButtonAction
{
    public ActionKind Kind { get; set; }
    public int[] Keys { get; set; } = [];
    public int[] GetKeys() => Kind switch
    {
        ActionKind.Shortcut => Keys,
        ActionKind.PlayPause => [0xB3],
        ActionKind.VolumeUp => [0xAF],
        ActionKind.VolumeDown => [0xAE],
        ActionKind.Mute => [0xAD],
        _ => []
    };
}

public sealed class Profile
{
    public string Name { get; set; } = "Algemeen";
    public string? ProgramPath { get; set; }
    public ScrollMode Mode { get; set; }
    public DialMode DialMode { get; set; }
    public int Speed { get; set; } = 3;
    public bool Invert { get; set; }
    public Dictionary<int, ButtonAction> Buttons { get; set; } = [];
    public int WheelDelta(int direction) => checked(direction * (Invert ? 1 : -1) * Speed * 120);
    public override string ToString() => Name;
}

public sealed class Settings
{
    public int Version { get; set; } = 1;
    public bool HapticFeedbackEnabled { get; set; } = true;
    public Profile General { get; set; } = new();
    public List<Profile> Programs { get; set; } = [];
    public Profile Select(string? path) => path is null ? General :
        Programs.FirstOrDefault(p => string.Equals(p.ProgramPath, path, StringComparison.OrdinalIgnoreCase)) ?? General;
    public Settings Copy() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        if (Version != 1 || General is null || Programs is null || General.ProgramPath is not null)
            throw new InvalidDataException("Ongeldige of niet ondersteunde instellingen.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Programs)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.ProgramPath) || !Path.IsPathFullyQualified(p.ProgramPath)
                || !p.ProgramPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !paths.Add(p.ProgramPath))
                throw new InvalidDataException("Een programmaprofiel heeft een ongeldig of dubbel programmapad.");
        }
        foreach (var p in Programs.Prepend(General))
        {
            if (string.IsNullOrWhiteSpace(p.Name) || p.Speed is < 1 or > 12 || !Enum.IsDefined(p.Mode) || !Enum.IsDefined(p.DialMode) || p.Buttons is null)
                throw new InvalidDataException("Een profiel heeft ongeldige scrollinstellingen.");
            foreach (var (button, action) in p.Buttons)
                if (button is < 1 or > 8 || action is null || !Enum.IsDefined(action.Kind) || action.Keys is null
                    || action.Keys.Length > 9 || action.Keys.Any(k => k is < 1 or > 254)
                    || action.Keys.Distinct().Count() != action.Keys.Length
                    || (action.Kind == ActionKind.Shortcut && action.Keys.Length == 0))
                    throw new InvalidDataException($"Knop {(button is >= 1 and <= 8 ? ButtonLayout.Number(button) : button)} heeft een ongeldige actie. Kies bij een sneltoets ook een hoofdtoets.");
        }
    }
}

public sealed class SettingsStore(string? directory = null)
{
    // Keep the existing storage path so the product rename preserves saved settings.
    public string FilePath { get; } = Path.Combine(directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D100HControl"), "settings.json");
    public string? LoadError { get; private set; }

    public Settings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath))
                ?? throw new InvalidDataException("Het instellingenbestand is leeg.");
            settings.Validate();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            LoadError = $"Instellingen niet geladen: {e.Message} Het oorspronkelijke bestand blijft bewaard: {FilePath}";
            return new();
        }
    }

    public void Save(Settings settings)
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
