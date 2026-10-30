using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;

// Simulates delayed Bluetooth writes without touching a real device.
internal sealed class SlowHidStream : Stream
{
    private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
    private int writing;
    private int concurrentWriteDetected;
    public ConcurrentQueue<long> WriteTimes { get; } = new();
    public ConcurrentQueue<byte[]> Writes { get; } = new();
    public ConcurrentQueue<long> KeepAliveTimes { get; } = new();
    public bool AcknowledgeHaptics { get; set; } = true;
    public bool WrongHapticAcknowledgement { get; set; }
    public bool SendOppositeHapticAcknowledgement { get; set; }
    public bool FailWrites { get; set; }
    public TimeSpan WriteDelay { get; set; } = TimeSpan.FromMilliseconds(600);
    public bool ConcurrentWriteDetected => Volatile.Read(ref concurrentWriteDetected) != 0;
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var report = await input.Reader.ReadAsync(cancellationToken);
        report.CopyTo(buffer);
        return report.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref writing, 1) != 0) Volatile.Write(ref concurrentWriteDetected, 1);
        try
        {
            var report = buffer.ToArray();
            Writes.Enqueue(report);
            var code = Convert.ToHexString(report.AsSpan(1, 8));
            if (code == "24569EF228E145A1") KeepAliveTimes.Enqueue(Stopwatch.GetTimestamp());
            WriteTimes.Enqueue(Stopwatch.GetTimestamp());
            if (FailWrites) throw new IOException("simulated write failure");
            await Task.Delay(WriteDelay, cancellationToken);
            if (code == "24569EF228E145A1")
            {
                var reply = report.ToArray();
                Convert.FromHexString("21F45EB71C3587AB").CopyTo(reply, 1);
                await input.Writer.WriteAsync(reply, cancellationToken);
                if (KeepAliveTimes.Count == 3) ThirdWrite.TrySetResult();
            }
            else if (AcknowledgeHaptics && code is "39B056AFD36F9ABF" or "5F4E1D1371B29A76")
            {
                var reply = report.ToArray();
                Convert.FromHexString(WrongHapticAcknowledgement ? "A1F76ADB93F65A78" :
                    code == "39B056AFD36F9ABF" ? "A1F76ADB93F65A78" : "99BF42CA2354B0AF").CopyTo(reply, 1);
                await input.Writer.WriteAsync(reply, cancellationToken);
                if (SendOppositeHapticAcknowledgement)
                {
                    var opposite = reply.ToArray();
                    Convert.FromHexString(code == "39B056AFD36F9ABF" ? "99BF42CA2354B0AF" : "A1F76ADB93F65A78").CopyTo(opposite, 1);
                    await input.Writer.WriteAsync(opposite, cancellationToken);
                }
            }
        }
        finally { Volatile.Write(ref writing, 0); }
    }

    public TaskCompletionSource ThirdWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
