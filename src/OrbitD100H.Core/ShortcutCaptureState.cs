namespace OrbitD100H.Core;

public readonly record struct CaptureDecision(bool Suppress, bool Completed = false, int[]? Keys = null);

public sealed class ShortcutCaptureState
{
    private static readonly int[] Order = [0xA2, 0xA3, 0xA0, 0xA1, 0xA4, 0xA5, 0x5B, 0x5C];
    private readonly HashSet<int> initiallyDown;
    private readonly HashSet<int> modifiers = [];
    private readonly HashSet<int> suppressed = [];
    private int[]? candidate;
    private bool cancelled;
    private bool finished;

    public ShortcutCaptureState(IEnumerable<int>? pressedBeforeStart = null)
    {
        initiallyDown = new(pressedBeforeStart ?? []);
        modifiers.UnionWith(initiallyDown.Where(ShortcutKeys.IsModifier));
    }

    public CaptureDecision Process(int key, int scan, bool extended, bool up)
    {
        if (finished) return new(false);
        key = key switch
        {
            0x11 => extended ? 0xA3 : 0xA2,
            0x12 => extended ? 0xA5 : 0xA4,
            0x10 => scan == 0x36 ? 0xA1 : 0xA0,
            _ => key
        };
        if (key is < 1 or > 254 or 0xE7) return new(false);
        if (up)
        {
            modifiers.Remove(key);
            initiallyDown.Remove(key);
            var block = suppressed.Remove(key);
            finished = (candidate is not null || cancelled) && suppressed.Count == 0;
            return new(block, finished, cancelled ? null : candidate);
        }
        // Repeats of keys held before recording keep their normal matching key-up.
        if (initiallyDown.Contains(key)) return new(false);
        if (!suppressed.Add(key)) return new(true);
        if (ShortcutKeys.IsModifier(key)) modifiers.Add(key);
        else if (candidate is null && !cancelled)
        {
            if (key == 0x1B && modifiers.Count == 0) cancelled = true;
            else candidate = ShortcutKeys.Create(key, Order.Where(modifiers.Contains));
        }
        return new(true);
    }
}
