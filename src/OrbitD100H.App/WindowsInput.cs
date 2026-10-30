using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OrbitD100H.App;

internal static class WindowsInput
{
    internal const nuint SyntheticTag = 0x44313030;
    private static readonly HeldKeys Held = new(key => (GetAsyncKeyState(key) & 0x8000) != 0, Key);
    internal static bool HasPendingRelease => Held.HasPendingRelease;
    internal static nint ForegroundWindow => GetForegroundWindow();
    internal static (nint Window, string? Program) Target(bool underMouse)
    {
        nint window;
        if (underMouse)
        {
            if (!GetCursorPos(out var point)) return (0, null);
            window = GetAncestor(WindowFromPoint(point), 2);
        }
        else window = GetForegroundWindow();
        if (window == 0) return (0, null);
        GetWindowThreadProcessId(window, out var pid);
        using var process = OpenProcess(0x1000, false, pid);
        if (process.IsInvalid) return (window, null);
        var path = new StringBuilder(32768);
        var length = path.Capacity;
        return (window, QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : null);
    }

    internal static bool RoutesUnderMouse() => SystemParametersInfo(0x201C, 0, out var routing, 0) && routing == 2;

    internal static void Wheel(int delta)
    {
        if (delta == 0) return;
        Send(new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { MouseData = unchecked((uint)delta), Flags = 0x0800 } } });
    }

    internal static void Press(int button, int[] keys)
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var layout = KeyboardLayout.Get(thread);
        Held.Press(button, keys.Select(value =>
        {
            var key = KeyboardLayout.SpecificKey(checked((ushort)value));
            return (key, KeyboardLayout.Stroke(key, layout));
        }));
    }

    internal static void Tap(int[] keys)
    {
        try { Press(0, keys); }
        finally { Release(0); }
    }

    internal static void Release(int button) => Held.Release(button);
    internal static void Release() => Held.ReleaseAll();

    private static void Key(KeyboardStroke stroke, bool up)
    {
        Send(new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput {
            VirtualKey = stroke.VirtualKey, Scan = stroke.ScanCode, Flags = stroke.Flags | (up ? 2u : 0u), ExtraInfo = SyntheticTag } } });
    }

    private static void Send(Input input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows weigert invoer. Controleer of het doelprogramma als administrator draait.");
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { public int X, Y; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out uint value, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(Microsoft.Win32.SafeHandles.SafeProcessHandle process,
        uint flags, StringBuilder name, ref int length);
}
