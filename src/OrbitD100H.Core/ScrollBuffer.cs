namespace OrbitD100H.Core;

public sealed class ScrollBuffer
{
    private long pending;
    private long lastTick;
    private long deadline;
    private nint target;
    public bool HasPending => pending != 0;

    public void Add(int delta, nint window, long now)
    {
        if (window != target) Clear();
        if (pending == 0) lastTick = now;
        target = window;
        pending += delta;
        deadline = now + 96;
    }

    public int Take(nint currentWindow, long now)
    {
        if (currentWindow != target) { Clear(); return 0; }
        if (pending == 0 || now <= lastTick) return 0;
        var amount = now >= deadline ? pending : (long)Math.Round(
            pending * (double)(now - lastTick) / (deadline - lastTick), MidpointRounding.AwayFromZero);
        amount = Math.Clamp(amount, int.MinValue, int.MaxValue);
        pending -= amount;
        lastTick = now;
        return (int)amount;
    }

    public void Clear() { pending = 0; target = 0; }
}
