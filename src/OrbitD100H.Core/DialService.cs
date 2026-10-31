using System.ComponentModel;
using System.Diagnostics;

namespace OrbitD100H.Core;

public static class DialService
{
    public static async Task RunAsync(Action<DialEvent> received, Action<bool, LocalizedText> status, CancellationToken stop,
        Func<bool>? hapticFeedback = null)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                var devices = NativeHid.FindCollections().Where(d => d.CanUseVendorProtocol).ToArray();
                if (devices.Length != 1)
                    status(false, new(devices.Length == 0 ? "DeviceMissing" : "MultipleDevices"));
                else await Session(devices[0], received, status, stop, hapticFeedback);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception e) when (e is IOException or Win32Exception or UnauthorizedAccessException or TimeoutException or OperationCanceledException)
            { status(false, new("ConnectionInterrupted", Text.Error(e))); }
            try { await Task.Delay(250, stop); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        }
        status(false, new("Closed"));
    }

    private static async Task Session(HidCollection device, Action<DialEvent> received,
        Action<bool, LocalizedText> status, CancellationToken stop, Func<bool>? hapticFeedback)
    {
        using var stream = NativeHid.Open(device);
        await RunSessionAsync(stream, device.InputLength, device.OutputLength, received, status, stop, hapticFeedback);
    }

    internal static async Task RunSessionAsync(Stream stream, int inputLength, int outputLength,
        Action<DialEvent> received, Action<bool, LocalizedText> status, CancellationToken stop,
        Func<bool>? hapticFeedback = null)
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var lastAcknowledgement = Stopwatch.GetTimestamp();
        var connected = 0;
        var lastHapticSent = (bool?)null;
        var lastHapticAcknowledged = (bool?)null;
        var hapticSentAt = 0L;
        var hapticWarning = false;
        var hapticSync = new object();
        status(false, new("ConnectingDevice"));
        var reader = Task.Run(async () =>
        {
            var buffer = new byte[inputLength];
            while (true)
            {
                var length = await stream.ReadAsync(buffer, cancel.Token);
                if (length == 0) throw Text.Describe(new IOException(Text.Get("DeviceDisconnected")), "DeviceDisconnected");
                if (Protocol.TryDecodeHapticAcknowledgement(buffer.AsSpan(0, length), out var hapticEnabled))
                {
                    lock (hapticSync)
                    {
                        if (lastHapticSent == hapticEnabled)
                            lastHapticAcknowledged = hapticEnabled;
                    }
                    continue;
                }
                if (!Protocol.TryDecode(buffer.AsSpan(0, length), out var e)) continue;
                if (e.Kind == EventKind.KeepAlive)
                {
                    Interlocked.Exchange(ref lastAcknowledgement, Stopwatch.GetTimestamp());
                    if (Interlocked.Exchange(ref connected, 1) == 0) status(true, new("Connected"));
                }
                else if (Volatile.Read(ref connected) != 0) received(e);
            }
        });
        var writer = Task.Run(async () =>
        {
            var report = Protocol.CreateKeepAlive(outputLength);
            // Fixed cadence: Bluetooth write time must not extend the keepalive interval.
            using var timer = new PeriodicTimer(Protocol.KeepAliveInterval);
            while (true)
            {
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref lastAcknowledgement)) > TimeSpan.FromSeconds(5))
                    throw Text.Describe(new TimeoutException(Text.Get("NoKeepalive")), "NoKeepalive");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                await stream.WriteAsync(report, timeout.Token);
                if (Volatile.Read(ref connected) != 0 && hapticFeedback is not null)
                {
                    var desired = hapticFeedback();
                    bool sendHaptic;
                    lock (hapticSync)
                    {
                        sendHaptic = lastHapticSent != desired;
                        if (sendHaptic)
                        {
                            lastHapticSent = desired;
                            lastHapticAcknowledged = null;
                            hapticSentAt = Stopwatch.GetTimestamp();
                        }
                    }
                    if (sendHaptic)
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(2));
                        await stream.WriteAsync(Protocol.CreateHapticFeedback(desired, outputLength), timeout.Token);
                    }
                    var warn = false;
                    var clearWarning = false;
                    lock (hapticSync)
                    {
                        if (lastHapticSent == desired && lastHapticAcknowledged == desired && hapticWarning)
                        { hapticWarning = false; clearWarning = true; }
                        else if (lastHapticSent == desired && lastHapticAcknowledged != desired
                            && Stopwatch.GetElapsedTime(hapticSentAt) > TimeSpan.FromSeconds(3) && !hapticWarning)
                        { hapticWarning = true; warn = true; }
                    }
                    if (warn) status(true, new("HapticUnconfirmed"));
                    else if (clearWarning) status(true, new("Connected"));
                }
                await timer.WaitForNextTickAsync(cancel.Token);
            }
        });
        try { await await Task.WhenAny(reader, writer); }
        finally
        {
            cancel.Cancel();
            status(false, new("ConnectionClosed"));
            try { await Task.WhenAll(reader, writer); }
            catch (OperationCanceledException) { }
        }
    }
}
