using System.Runtime.InteropServices;
using System.Text;

namespace OrbitD100H.App;

internal readonly record struct KeyboardStroke(ushort VirtualKey, ushort ScanCode, uint Flags);

internal static class KeyboardLayout
{
    internal static nint Get(uint thread = 0) => GetKeyboardLayout(thread);

    // Generic modifiers use their left-hand key, matching their existing scan codes.
    internal static ushort SpecificKey(ushort key) => key switch
    { 0x10 => 0xA0, 0x11 => 0xA2, 0x12 => 0xA4, _ => key };

    internal static string? Character(ushort key, nint layout)
    {
        var scan = MapVirtualKeyEx(key, 4, layout);
        var text = new StringBuilder(8);
        // Flag 4 preserves the keyboard's dead-key state (Windows 10 1607+).
        var count = ToUnicodeEx(key, scan & 0xFF, new byte[256], text, text.Capacity, 4, layout);
        if (count == 0 || text.Length == 0) return null;
        var value = text.ToString(0, Math.Min(Math.Abs(count), text.Length));
        return value.Any(char.IsControl) ? null : value;
    }

    internal static KeyboardStroke Stroke(ushort key, nint layout)
    {
        // Media keys retain the existing virtual-key route.
        if (key is >= 0xA6 and <= 0xB7) return new(key, 0, 1);
        var scan = MapVirtualKeyEx(key, 4, layout);
        if (scan == 0 || (scan & 0xFF00) == 0xE100) return new(key, 0, 0);
        return new(0, (ushort)(scan & 0xFF), 8u | ((scan & 0xFF00) == 0xE000 ? 1u : 0u));
    }

    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static extern uint MapVirtualKeyEx(uint code, uint type, nint layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, StringBuilder text, int length, uint flags, nint layout);
}
