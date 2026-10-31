using System.ComponentModel;
using System.Runtime.InteropServices;
using OrbitD100H.Core;

namespace OrbitD100H.App;

internal static class KeyboardRecorder
{
    private const uint Quit = 0x12;
    private delegate nint HookProc(int code, nuint message, nint data);

    internal static Task<int[]?> CaptureAsync(CancellationToken stop)
    {
        var completion = new TaskCompletionSource<int[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Run(completion, stop)) { IsBackground = true, Name = "D100H hotkey recorder" };
        thread.Start();
        return completion.Task;
    }

    private static void Run(TaskCompletionSource<int[]?> completion, CancellationToken stop)
    {
        nint hook = 0;
        int[]? result = null;
        Exception? failure = null;
        HookProc? callback = null;
        try
        {
            // Create this thread's message queue before cancellation can post to it.
            PeekMessage(out _, 0, 0, 0, 0);
            var thread = GetCurrentThreadId();
            using var cancellation = stop.Register(() => PostThreadMessage(thread, Quit, 0, 0));
            using var timeout = new Timer(_ => PostThreadMessage(thread, Quit, 0, 0), null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);
            if (stop.IsCancellationRequested) return;
            var initial = Enumerable.Range(1, 254).Where(k => k is not (0x10 or 0x11 or 0x12)
                && (GetAsyncKeyState(k) & 0x8000) != 0).ToArray();
            var state = new ShortcutCaptureState(initial);
            callback = (code, message, data) =>
            {
                if (code < 0 || message is not (0x100 or 0x101 or 0x104 or 0x105))
                    return CallNextHookEx(hook, code, message, data);
                try
                {
                    var key = Marshal.PtrToStructure<KeyboardData>(data);
                    if (key.ExtraInfo == WindowsInput.SyntheticTag) return CallNextHookEx(hook, code, message, data);
                    var decision = state.Process((int)key.VirtualKey, (int)key.ScanCode, (key.Flags & 1) != 0,
                        message is 0x101 or 0x105);
                    if (decision.Completed) { result = decision.Keys; PostThreadMessage(thread, Quit, 0, 0); }
                    return decision.Suppress ? 1 : CallNextHookEx(hook, code, message, data);
                }
                catch (Exception e)
                {
                    failure = e;
                    PostThreadMessage(thread, Quit, 0, 0);
                    return CallNextHookEx(hook, code, message, data);
                }
            };
            hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            if (hook == 0) throw Text.Describe(new Win32Exception(Marshal.GetLastWin32Error(), Text.Get("HookError")), "HookError");
            while (true)
            {
                var status = GetMessage(out _, 0, 0, 0);
                if (status == 0) break;
                if (status < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception e) { failure = e; }
        finally
        {
            if (hook != 0) UnhookWindowsHookEx(hook);
            GC.KeepAlive(callback);
            if (failure is not null) completion.TrySetException(failure);
            else completion.TrySetResult(stop.IsCancellationRequested ? null : result);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData
    { public uint VirtualKey, ScanCode, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hook, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nuint message, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
