using System.Text.Json;
using OrbitD100H.Core;

if (args.Contains("--self-test")) { SelfTest.Run(); return 0; }
var evidence = new List<object>();
void Log(string message)
{
    lock (evidence)
    {
        var at = DateTimeOffset.Now;
        Console.WriteLine($"{at:HH:mm:ss.fff} {message}");
        evidence.Add(new { at, message });
    }
}

string? output = null;
var outputIndex = Array.IndexOf(args, "--output");
if (outputIndex >= 0 && outputIndex + 1 < args.Length) output = Path.GetFullPath(args[outputIndex + 1]);
var result = 0;
try
{
    var collections = NativeHid.FindCollections();
    Log($"D100H-collecties: {collections.Count}");
    for (var i = 0; i < collections.Count; i++)
    {
        var c = collections[i];
        Log($"Collectie {i}: usage={c.UsagePage:X4}/{c.Usage:X4}; input={c.InputLength}; output={c.OutputLength}; FB input={c.VendorInput}; FB output={c.VendorOutput}");
    }
    if (args.Contains("--monitor-consumer") || args.Contains("--monitor-vendor"))
    {
        var vendor = args.Contains("--monitor-vendor");
        var source = vendor ? collections.Single(c => c.CanUseVendorProtocol)
            : collections.Single(c => c.UsagePage == 0x0C && c.Usage == 1);
        using var stream = NativeHid.OpenInput(source);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(ReadSeconds(args)));
        var buffer = new byte[source.InputLength];
        var reports = 0;
        Log($"Passieve D100H-{(vendor ? "knoppenmeting" : "volumemeting")} gestart. Geen keepalive, geen blokkade en geen synthetische invoer.");
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, stop.Token);
                if (count == 0) throw new IOException("Consumer-verbinding gesloten.");
                if (vendor)
                {
                    if (Protocol.TryDecode(buffer.AsSpan(0, count), out var ev) && ev.Kind == EventKind.Press)
                    { reports++; Log($"KNOP: {ev.Button}"); }
                }
                else
                {
                    reports++;
                    Log($"CONSUMER: {Convert.ToHexString(buffer.AsSpan(0, Math.Min(count, 3)))}");
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { Log($"{(vendor ? "Knopdrukken" : "Consumer-rapporten")}: {reports}"); }
    }
    else if (args.Contains("--listen"))
    {
        var vendors = collections.Where(c => c.CanUseVendorProtocol).ToArray();
        if (vendors.Length != 1) throw new InvalidOperationException($"Precies één vendorcollectie vereist; gevonden: {vendors.Length}.");
        var seconds = ReadSeconds(args);
        using var mutex = new Mutex(true, @"Local\D100HControl", out var created);
        if (!created) throw new InvalidOperationException("Een D100H-proces is al actief.");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { await Listen(vendors[0], stop, Log); }
        finally { Console.CancelKeyPress -= cancel; }
    }
    else if (!collections.Any(c => c.CanUseVendorProtocol)) result = 2;
}
catch (Exception e) { Log($"FOUT: {e.GetType().Name}: {e.Message}"); result = 1; }
finally
{
    if (output is not null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { exitCode = result, evidence }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Meetresultaten: {output}");
    }
}
return result;

static int ReadSeconds(string[] arguments)
{
    var seconds = 90;
    var index = Array.IndexOf(arguments, "--seconds");
    if (index >= 0 && (index + 1 >= arguments.Length || !int.TryParse(arguments[index + 1], out seconds)
        || seconds is < 1 or > 600)) throw new ArgumentException("--seconds moet tussen 1 en 600 liggen.");
    return seconds;
}

static async Task Listen(HidCollection collection, CancellationTokenSource stop, Action<string> log)
{
    using var stream = NativeHid.Open(collection);
    log("Vendorcollectie geopend. Alleen keepalive; geen synthetische invoer.");
    var presses = new HashSet<int>();
    var releases = new HashSet<int>();
    var clockwise = 0;
    var counterclockwise = 0;
    var acknowledgements = 0;
    var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var reader = Task.Run(async () =>
    {
        var buffer = new byte[collection.InputLength];
        while (!stop.IsCancellationRequested)
        {
            var count = await stream.ReadAsync(buffer, stop.Token);
            if (count == 0) throw new IOException("HID-verbinding gesloten.");
            if (!Protocol.TryDecode(buffer.AsSpan(0, count), out var ev))
            {
                // The unused payload can contain device memory. Never log those bytes.
                log($"Onbekend rapport: lengte={count}, ID={buffer[0]:X2}, code={Convert.ToHexString(buffer.AsSpan(1, Math.Min(8, count - 1)))}");
                continue;
            }
            if (ev.Kind == EventKind.KeepAlive)
            {
                acknowledgements++;
                if (confirmed.TrySetResult()) log("Vendor mode bevestigd door keepalive-antwoord.");
            }
            else
            {
                if (ev.Kind == EventKind.Press) presses.Add(ev.Button);
                if (ev.Kind == EventKind.Release) releases.Add(ev.Button);
                if (ev.Direction == 1) clockwise++;
                if (ev.Direction == -1) counterclockwise++;
                log($"EVENT {ev.Kind}: knop={ev.Button}, richting={ev.Direction}");
            }
        }
    });
    var writer = Task.Run(async () =>
    {
        var keepAlive = Protocol.CreateKeepAlive(collection.OutputLength);
        using var timer = new PeriodicTimer(Protocol.KeepAliveInterval);
        while (!stop.IsCancellationRequested)
        {
            using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            writeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            await stream.WriteAsync(keepAlive, writeTimeout.Token);
            await timer.WaitForNextTickAsync(stop.Token);
        }
    });
    try
    {
        var startupTimeout = Task.Delay(TimeSpan.FromSeconds(5), stop.Token);
        var first = await Task.WhenAny(confirmed.Task, reader, writer, startupTimeout);
        if (first == reader) await reader;
        if (first == writer) await writer;
        if (!confirmed.Task.IsCompletedSuccessfully && !stop.IsCancellationRequested)
            throw new TimeoutException("Geen keepalive-antwoord binnen vijf seconden; vendor mode niet bewezen.");
        await await Task.WhenAny(reader, writer, Task.Delay(Timeout.Infinite, stop.Token));
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    finally
    {
        stop.Cancel();
        try { await Task.WhenAll(reader, writer); }
        catch (OperationCanceledException) { }
        finally
        {
            log($"Samenvatting: keepalive={acknowledgements}; rechtsom={clockwise}; linksom={counterclockwise}; ingedrukt=[{string.Join(',', presses.Order())}]; losgelaten=[{string.Join(',', releases.Order())}]");
            log("Keepalive gestopt. Apparaat hoort zelfstandig naar standaardbediening terug te keren.");
        }
    }
}
