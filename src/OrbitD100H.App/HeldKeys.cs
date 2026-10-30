using System.ComponentModel;

namespace OrbitD100H.App;

internal sealed class HeldKeys(Func<ushort, bool> isDown, Action<KeyboardStroke, bool> send)
{
    private sealed class HeldKey(KeyboardStroke stroke, bool injected)
    {
        public KeyboardStroke Stroke { get; } = stroke;
        public bool Injected { get; } = injected;
        public int Owners { get; set; }
    }

    private readonly Dictionary<int, List<HeldKey>> owners = [];
    private readonly List<HeldKey> held = [];
    internal bool HasPendingRelease => held.Any(key => key.Owners == 0 && key.Injected);

    internal void Press(int owner, IEnumerable<(ushort Key, KeyboardStroke Stroke)> keys)
    {
        if (owners.ContainsKey(owner)) return;
        var acquired = new List<HeldKey>();
        owners.Add(owner, acquired);
        foreach (var (key, stroke) in keys)
        {
            var entry = held.FirstOrDefault(h => h.Stroke == stroke);
            if (entry is null)
            {
                // Preserve keys that the user is already physically holding.
                var injected = !isDown(key);
                if (injected) send(stroke, false);
                entry = new(stroke, injected);
                held.Add(entry);
            }
            if (acquired.Contains(entry)) continue;
            entry.Owners++;
            acquired.Add(entry);
        }
    }

    internal void Release(int owner)
    {
        if (owners.Remove(owner, out var acquired))
            foreach (var key in acquired) key.Owners--;
        ReleaseUnused();
    }

    internal void ReleaseAll()
    {
        owners.Clear();
        foreach (var key in held) key.Owners = 0;
        ReleaseUnused();
    }

    private void ReleaseUnused()
    {
        Exception? failure = null;
        foreach (var key in Enumerable.Reverse(held).Where(key => key.Owners == 0).ToArray())
        {
            try
            {
                if (key.Injected) send(key.Stroke, true);
                held.Remove(key);
            }
            catch (Win32Exception e) { failure ??= e; }
        }
        if (failure is not null) throw failure;
    }
}
