namespace OrbitD100H.Core;

public sealed class ButtonState
{
    private readonly HashSet<int> pressed = [];
    private readonly HashSet<int> blocked = [];
    public bool Update(DialEvent e)
    {
        if (e.Button is < 1 or > 8) return false;
        if (e.Kind == EventKind.Press) return pressed.Add(e.Button) && !blocked.Contains(e.Button);
        if (e.Kind != EventKind.Release) return false;
        var wasPressed = pressed.Remove(e.Button);
        return !blocked.Remove(e.Button) && wasPressed;
    }
    public bool ShouldTrigger(DialEvent e)
    {
        return Update(e) && (e.Kind == EventKind.Press ? e.Button != 8 : e.Button == 8);
    }
    public void Interrupt() => blocked.UnionWith(pressed);
    public void Clear() { pressed.Clear(); blocked.Clear(); }
}
