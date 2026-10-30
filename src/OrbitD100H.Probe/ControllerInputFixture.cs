using System.ComponentModel;

namespace OrbitD100H.App;

// The probe compiles Controller against this in-memory input sink; self-tests never inject Windows input.
internal static class WindowsInput
{
    internal static readonly List<(KeyboardStroke Stroke, bool Up)> Events = [];
    private static readonly HeldKeys Held = new(_ => false, (stroke, up) =>
    {
        if (FailInput) throw new Win32Exception(5);
        Events.Add((stroke, up));
    });
    internal static bool FailInput { get; set; }
    internal static bool Routing { get; set; } = true;
    internal static List<int> Wheels { get; } = [];
    internal static bool HasPendingRelease => Held.HasPendingRelease;
    internal static nint ForegroundWindow { get; set; } = 10;
    internal static string? Program { get; set; }
    internal static (nint Window, string? Program) Target(bool underMouse) => (ForegroundWindow, Program);
    internal static bool RoutesUnderMouse() => Routing;
    internal static void Wheel(int delta) => Wheels.Add(delta);
    internal static void Press(int button, int[] keys)
    {
        var layout = KeyboardLayout.Get();
        Held.Press(button, keys.Select(value =>
        {
            var key = KeyboardLayout.SpecificKey((ushort)value);
            return (key, KeyboardLayout.Stroke(key, layout));
        }));
    }
    internal static void Release(int button) => Held.Release(button);
    internal static void Release() => Held.ReleaseAll();
    internal static void Tap(int[] keys)
    {
        try { Press(0, keys); }
        finally { Release(0); }
    }
    internal static void Reset()
    {
        FailInput = false;
        Release(); Events.Clear(); Wheels.Clear(); ForegroundWindow = 10; Program = null; Routing = true;
    }
}
