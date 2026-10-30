using System.ComponentModel;
using OrbitD100H.App;
using OrbitD100H.Core;

internal static class HoldTests
{
    internal static int Run()
    {
        var checks = 0;
        void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Test mislukt: " + message);
            checks++;
        }
        var layout = KeyboardLayout.Get();
        (ushort Key, KeyboardStroke Stroke) Key(ushort value)
        {
            var key = KeyboardLayout.SpecificKey(value);
            return (key, KeyboardLayout.Stroke(key, layout));
        }
        var ctrl = Key(0xA2);
        var slash = Key(0xDC);
        var c = Key(0x43);
        var v = Key(0x56);
        var events = new List<(KeyboardStroke Stroke, bool Up)>();
        var physical = new HashSet<ushort>();
        KeyboardStroke? failUp = null;
        KeyboardStroke? failDown = null;
        var keys = new HeldKeys(physical.Contains, (stroke, up) =>
        {
            if ((up && failUp == stroke) || (!up && failDown == stroke)) throw new Win32Exception(5);
            events.Add((stroke, up));
        });

        keys.Press(1, [ctrl, slash]);
        Assert(events.SequenceEqual([(ctrl.Stroke, false), (slash.Stroke, false)]), "vasthouden geeft alleen key-down");
        keys.Press(1, [ctrl, slash]);
        Assert(events.Count == 2, "dubbel drukrapport geeft geen herhaling");
        keys.Release(7);
        Assert(events.Count == 2, "onbekend loslaatrapport laat vastgehouden toetsen ongemoeid");
        keys.Release(1);
        Assert(events.Skip(2).SequenceEqual([(slash.Stroke, true), (ctrl.Stroke, true)]), "loslaten geeft key-up in omgekeerde volgorde");
        keys.Release(1);
        Assert(events.Count == 4, "dubbel loslaten geeft geen tweede key-up");

        events.Clear();
        keys.Press(1, [Key(0x11), ctrl, c]);
        keys.Release(1);
        Assert(events.SequenceEqual([(ctrl.Stroke, false), (c.Stroke, false), (c.Stroke, true), (ctrl.Stroke, true)]), "aliassen binnen dezelfde combinatie tellen als één toets");

        events.Clear();
        keys.Press(1, [ctrl, c]);
        keys.Press(2, [Key(0x11), v]);
        Assert(events.SequenceEqual([(ctrl.Stroke, false), (c.Stroke, false), (v.Stroke, false)]), "algemene en linker Ctrl delen werkelijke toets");
        keys.Release(1);
        Assert(events.Last() == (c.Stroke, true) && events.Count == 4, "eerste eigenaar laat gedeelde Ctrl niet los");
        keys.Release(2);
        Assert(events.Skip(4).SequenceEqual([(v.Stroke, true), (ctrl.Stroke, true)]), "laatste eigenaar laat gedeelde Ctrl los");

        events.Clear();
        keys.Press(1, [ctrl, c]);
        keys.Press(0, [ctrl, v]);
        keys.Release(0);
        Assert(events.Last() == (v.Stroke, true) && !events.Contains((ctrl.Stroke, true)), "korte aanslag laat bestaande vasthoudactie intact");
        keys.ReleaseAll();
        Assert(events.TakeLast(2).SequenceEqual([(c.Stroke, true), (ctrl.Stroke, true)]), "globale onderbreking laat resterende combinatie los");

        events.Clear();
        physical.Add(ctrl.Key);
        keys.Press(1, [ctrl, c]);
        keys.Press(2, [Key(0x11), v]);
        keys.ReleaseAll();
        Assert(!events.Any(e => e.Stroke == ctrl.Stroke), "fysiek vooraf vastgehouden Ctrl krijgt geen synthetische down of up");
        Assert(events.Count == 4, "overige toetsen worden bij fysieke Ctrl normaal beheerd");
        physical.Clear();

        foreach (var pair in new[] { (Left: (ushort)0xA2, Right: (ushort)0xA3), (Left: (ushort)0xA4, Right: (ushort)0xA5),
            (Left: (ushort)0x5B, Right: (ushort)0x5C), (Left: (ushort)0xA0, Right: (ushort)0xA1) })
        {
            events.Clear();
            var left = Key(pair.Left);
            var right = Key(pair.Right);
            keys.Press(1, [left]);
            keys.Press(2, [right]);
            keys.Release(1);
            Assert(events.SequenceEqual([(left.Stroke, false), (right.Stroke, false), (left.Stroke, true)]), "linker en rechter modifier blijven onafhankelijk: " + pair.Left);
            keys.Release(2);
            Assert(events.Last() == (right.Stroke, true), "rechter modifier gebruikt oorspronkelijke scancode: " + pair.Right);
        }
        Assert(Key(0x10) == Key(0xA0) && Key(0x12) == Key(0xA4), "algemene Shift en Alt gebruiken bestaande linker scancode");

        events.Clear();
        keys.Press(1, [ctrl, c]);
        failUp = c.Stroke;
        try { keys.ReleaseAll(); throw new InvalidOperationException("Vrijgavefout niet doorgegeven."); }
        catch (Win32Exception) { checks++; }
        Assert(keys.HasPendingRelease && events.Last() == (ctrl.Stroke, true), "mislukte key-up blijft bewaard en andere vrijgaven gaan door");
        failUp = null;
        keys.ReleaseAll();
        Assert(!keys.HasPendingRelease && events.Last() == (c.Stroke, true), "opruimpoging herhaalt mislukte key-up");
        Assert(events.Count(e => e == (ctrl.Stroke, true)) == 1, "opruimpoging herhaalt geslaagde key-up niet");

        events.Clear();
        failDown = c.Stroke;
        try { keys.Press(1, [ctrl, c]); throw new InvalidOperationException("Indrukfout niet doorgegeven."); }
        catch (Win32Exception) { checks++; }
        failDown = null;
        keys.ReleaseAll();
        Assert(events.SequenceEqual([(ctrl.Stroke, false), (ctrl.Stroke, true)]), "gedeeltelijk mislukte combinatie kan volledig worden opgeruimd");

        var buttons = new ButtonState();
        Assert(buttons.Update(new(EventKind.Press, 1)), "druk begint een nieuwe knoptoestand");
        Assert(!buttons.Update(new(EventKind.Press, 1)), "herhaalde druk verandert toestand niet");
        buttons.Interrupt();
        Assert(!buttons.Update(new(EventKind.Press, 1)), "onderbreking heractiveert vastgehouden knop niet");
        Assert(!buttons.Update(new(EventKind.Release, 1)), "loslaten na onderbreking geeft geen actie");
        Assert(buttons.Update(new(EventKind.Press, 1)), "opnieuw indrukken na loslaten werkt");
        Assert(buttons.Update(new(EventKind.Release, 1)), "loslaten van actieve knop wordt geregistreerd");
        Assert(!buttons.Update(new(EventKind.Release, 1)), "loslaten zonder druk wordt genegeerd");
        buttons.Update(new(EventKind.Press, 8));
        buttons.Interrupt();
        Assert(!buttons.ShouldTrigger(new(EventKind.Release, 8)), "onderbreking voorkomt achterstallige draaiknopactie");
        buttons.Update(new(EventKind.Press, 2));
        buttons.Interrupt();
        buttons.Interrupt();
        Assert(!buttons.Update(new(EventKind.Press, 2)) && !buttons.Update(new(EventKind.Release, 2)), "druk tijdens pauze blijft geblokkeerd tot loslaten");
        Assert(buttons.Update(new(EventKind.Press, 2)), "hervatten accepteert pas een nieuwe druk");
        return checks;
    }
}
