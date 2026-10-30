namespace OrbitD100H.Core;

public static class ShortcutKeys
{
    public static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5;

    public static int[] Create(int key, bool control, bool shift, bool alt, bool windows)
    {
        var result = new List<int>(5);
        if (control) result.Add(0x11);
        if (shift) result.Add(0x10);
        if (alt) result.Add(0x12);
        if (windows) result.Add(0x5B);
        return Create(key, result);
    }

    public static int[] Create(int key, IEnumerable<int> modifiers)
    {
        if (key is < 1 or > 254 || IsModifier(key)) throw new ArgumentOutOfRangeException(nameof(key));
        var selected = modifiers.ToArray();
        if (selected.Length > 8 || selected.Any(m => !IsModifier(m)) || selected.Distinct().Count() != selected.Length)
            throw new ArgumentException("Ongeldige modifiers.", nameof(modifiers));
        return [.. selected, key];
    }
}
