using System.ComponentModel;
using OrbitD100H.Core;

namespace OrbitD100H.App;

internal sealed class Controller : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource stop = new();
    private readonly ScrollBuffer buffer = new();
    private readonly ButtonState buttons = new();
    private Settings settings;
    private bool connected;
    private bool paused;
    private bool capturing;
    private bool routing;
    private LocalizedText connection = new("Connecting");
    private LocalizedText? error;
    private Task? deviceTask;
    private Task? scrollTask;
    private nint heldWindow;
    public event Action? Changed;
    private LocalizedText lastEvent = new("Waiting");
    public string LastEvent { get { lock (sync) return lastEvent.ToString(); } }
    private Profile? activeProfile;
    public string ActiveProfile { get { lock (sync) return activeProfile?.ProgramPath is null ? Text.Get("General") : activeProfile.Name; } }
    public bool IsPaused { get { lock (sync) return paused; } }
    public bool Routing { get { lock (sync) return routing; } }
    public string Status { get { lock (sync) return error?.ToString() ?? (capturing ? Text.Get("CapturingStatus", connection) : paused ? Text.Get("PausedStatus", connection) : connection.ToString()); } }

    public Controller(Settings settings) { this.settings = settings.Copy(); routing = WindowsInput.RoutesUnderMouse(); }
    public void Start()
    {
        deviceTask = Task.Run(() => DialService.RunAsync(Receive, Connection, stop.Token, () =>
        {
            lock (sync) return settings.HapticFeedbackEnabled;
        }));
        scrollTask = Task.Run(ScrollLoop);
    }

    public void Update(Settings updated)
    {
        lock (sync) { settings = updated.Copy(); buffer.Clear(); error = null; TryRelease(); }
        Changed?.Invoke();
    }

    public void RefreshRouting()
    {
        lock (sync) { routing = WindowsInput.RoutesUnderMouse(); if (!routing) buffer.Clear(); }
        Changed?.Invoke();
    }

    public void Pause(bool value)
    {
        lock (sync)
        {
            paused = value; buffer.Clear();
            TryRelease();
        }
        Changed?.Invoke();
    }

    public void SetCapturing(bool value)
    {
        lock (sync)
        {
            capturing = value;
            buffer.Clear();
            if (value) TryRelease();
        }
        Changed?.Invoke();
    }

    private bool TryRelease()
    {
        buttons.Interrupt();
        heldWindow = 0;
        try { WindowsInput.Release(); return true; }
        catch (Win32Exception e)
        {
            var message = Text.Error(e);
            if (error?.ToString() != message.ToString()) { error = message; Changed?.Invoke(); }
            return false;
        }
    }

    private bool CheckHeldTarget()
    {
        if (heldWindow == 0) return true;
        var foreground = WindowsInput.ForegroundWindow;
        // Windows can briefly return NULL during activation; that is not a confirmed target change.
        return foreground == 0 || foreground == heldWindow || TryRelease();
    }

    internal void Connection(bool state, LocalizedText message)
    {
        lock (sync)
        {
            connected = state; connection = message;
            if (!state) { buffer.Clear(); TryRelease(); }
        }
        Changed?.Invoke();
    }

    internal void Receive(DialEvent e)
    {
        lock (sync)
        {
            lastEvent = e.Kind == EventKind.Rotate ? new(e.Direction == 1 ? "RotateClockwise" : "RotateCounterclockwise")
                : new(e.Kind == EventKind.Press ? "Pressed" : "Released",
                    e.Button == 8 ? new LocalizedText("Dial") : new LocalizedText("Button", ButtonLayout.Number(e.Button)));
            try
            {
                var ready = CheckHeldTarget();
                var transition = buttons.Update(e);
                if (!ready || (WindowsInput.HasPendingRelease && !TryRelease()) || paused || capturing || !connected)
                { buttons.Interrupt(); return; }
                if (e.Kind == EventKind.Rotate)
                {
                    var target = WindowsInput.Target(true);
                    var profile = settings.Select(target.Program);
                    activeProfile = profile;
                    routing = WindowsInput.RoutesUnderMouse();
                    if (profile.DialMode == DialMode.Volume)
                    {
                        buffer.Clear();
                        WindowsInput.Tap([e.Direction * (profile.Invert ? -1 : 1) > 0 ? 0xAF : 0xAE]);
                        error = null;
                        return;
                    }
                    if (!routing || target.Window == 0) { buffer.Clear(); return; }
                    var delta = profile.WheelDelta(e.Direction);
                    if (profile.Mode == ScrollMode.Smooth) buffer.Add(delta, target.Window, Environment.TickCount64);
                    else { buffer.Clear(); WindowsInput.Wheel(delta); }
                }
                else
                {
                    if (e.Kind == EventKind.Release) WindowsInput.Release(e.Button);
                    if (transition && (e.Kind == EventKind.Press ? e.Button != 8 : e.Button == 8))
                    {
                        var target = WindowsInput.Target(false);
                        var profile = settings.Select(target.Program);
                        activeProfile = profile;
                        if (target.Window != 0 && profile.Buttons.TryGetValue(e.Button, out var action))
                        {
                            if (e.Button != 8 && action.Kind == ActionKind.Shortcut)
                            {
                                heldWindow = target.Window;
                                WindowsInput.Press(e.Button, action.GetKeys());
                            }
                            else WindowsInput.Tap(action.GetKeys());
                        }
                    }
                }
                error = null;
            }
            catch (Win32Exception ex) { error = Text.Error(ex); buffer.Clear(); TryRelease(); }
            finally { Changed?.Invoke(); }
        }
    }

    private async Task ScrollLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(8));
        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token)) Poll();
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    internal void Poll()
    {
        lock (sync)
        {
            try
            {
                if ((WindowsInput.HasPendingRelease && !TryRelease()) || !CheckHeldTarget()) return;
                if (!connected || paused || capturing || !buffer.HasPending) return;
                WindowsInput.Wheel(buffer.Take(WindowsInput.Target(true).Window, Environment.TickCount64));
            }
            catch (Win32Exception e) { error = Text.Error(e); buffer.Clear(); TryRelease(); Changed?.Invoke(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Pause(true);
        await stop.CancelAsync();
        await Task.WhenAll(deviceTask ?? Task.CompletedTask, scrollTask ?? Task.CompletedTask);
        lock (sync) TryRelease();
        stop.Dispose();
    }
}
