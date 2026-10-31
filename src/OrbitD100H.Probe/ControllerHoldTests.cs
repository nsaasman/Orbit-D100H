using OrbitD100H.App;
using OrbitD100H.Core;

internal static class ControllerHoldTests
{
    internal static int Run()
    {
        var checks = 0;
        void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Test mislukt: " + message);
            checks++;
        }
        var settings = new Settings();
        settings.General.Buttons[1] = new() { Kind = ActionKind.Shortcut, Keys = [0xA2, 0xDC] };
        settings.General.Buttons[2] = new() { Kind = ActionKind.Shortcut, Keys = [0x11, 0x43] };
        settings.General.Buttons[3] = new() { Kind = ActionKind.PlayPause };
        settings.General.Buttons[8] = new() { Kind = ActionKind.Shortcut, Keys = [0xA2, 0x56] };
        WindowsInput.Reset();
        var controller = new Controller(settings);
        controller.Connection(true, new("Connected"));
        void Press(int button) => controller.Receive(new(EventKind.Press, button));
        void Release(int button) => controller.Receive(new(EventKind.Release, button));

        Press(1);
        // More than ten seconds of 8 ms ticks, without sleeping or sending real keystrokes.
        for (var i = 0; i < 1500; i++) controller.Poll();
        Assert(WindowsInput.Events.Count == 2 && WindowsInput.Events.All(e => !e.Up), "controller laat combinatie tijdens vasthouden niet vanzelf los");
        Press(1);
        Assert(WindowsInput.Events.Count == 2, "controller negeert dubbele druk");
        Release(1);
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events.TakeLast(2).All(e => e.Up), "controller koppelt key-up aan fysieke release");

        WindowsInput.Events.Clear();
        Press(1); Press(2); Release(1);
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events.Count(e => e.Up) == 1, "controller houdt gedeelde Ctrl vast voor tweede knop");
        Release(2);
        Assert(WindowsInput.Events.Count == 6 && WindowsInput.Events.TakeLast(2).All(e => e.Up), "controller geeft gedeelde Ctrl vrij bij laatste knop");

        WindowsInput.Events.Clear();
        Press(3);
        Assert(WindowsInput.Events.Count == 2 && WindowsInput.Events[1].Up, "mediaactie blijft korte aanslag");
        Release(3);
        Assert(WindowsInput.Events.Count == 2, "mediarelease herhaalt actie niet");
        Press(8);
        Assert(WindowsInput.Events.Count == 2, "draaiknopdruk begint geen vasthoudactie");
        Release(8);
        Assert(WindowsInput.Events.Count == 6 && WindowsInput.Events.TakeLast(2).All(e => e.Up), "draaiknopdruk blijft aanslag bij loslaten");

        foreach (var interruption in new[] { "pauze", "opname", "verbinding", "opslaan", "focus" })
        {
            WindowsInput.Events.Clear();
            Press(1);
            switch (interruption)
            {
                case "pauze": controller.Pause(true); controller.Pause(false); break;
                case "opname": controller.SetCapturing(true); controller.SetCapturing(false); break;
                case "verbinding": controller.Connection(false, new("Closed")); controller.Connection(true, new("Connected")); break;
                case "opslaan": controller.Update(settings); break;
                case "focus": WindowsInput.ForegroundWindow = 11; controller.Poll(); break;
            }
            Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events.TakeLast(2).All(e => e.Up), "onderbreking laat alle toetsen los: " + interruption);
            Press(1);
            Assert(WindowsInput.Events.Count == 4, "onderbreking heractiveert vastgehouden knop niet: " + interruption);
            Release(1); Press(1);
            Assert(WindowsInput.Events.Count == 6 && !WindowsInput.Events.Last().Up, "nieuwe druk na onderbreking werkt: " + interruption);
            Release(1);
        }

        WindowsInput.Events.Clear();
        controller.Pause(true); Press(2); controller.Pause(false); Press(2);
        Assert(WindowsInput.Events.Count == 0, "druk tijdens pauze wordt niet alsnog uitgevoerd bij hervatten");
        Release(2); Press(2); Release(2);
        Assert(WindowsInput.Events.Count == 4, "knop herstelt na druk tijdens pauze");

        WindowsInput.Events.Clear();
        WindowsInput.ForegroundWindow = 10;
        Press(1);
        WindowsInput.ForegroundWindow = 0;
        controller.Poll();
        Assert(WindowsInput.Events.Count == 2, "tijdelijk ontbrekend voorgrondvenster is geen echte doelwisseling");
        WindowsInput.ForegroundWindow = 10;
        Release(1);

        WindowsInput.Events.Clear();
        Press(1);
        var original = WindowsInput.Events.Select(e => e.Stroke).ToArray();
        var changed = settings.Copy();
        changed.General.Buttons[1].Keys = [0x77];
        controller.Update(changed);
        Assert(WindowsInput.Events.TakeLast(2).Select(e => e.Stroke).SequenceEqual(original.Reverse()), "opslaan laat oorspronkelijke scancodes los, niet de nieuwe binding");
        Release(1); Press(1);
        Assert(WindowsInput.Events.Last() == (KeyboardLayout.Stroke(0x77, KeyboardLayout.Get()), false), "nieuwe binding geldt pas bij volgende druk");
        Release(1); controller.Update(settings);

        WindowsInput.Events.Clear();
        WindowsInput.Wheels.Clear();
        var volume = settings.Copy();
        volume.General.Mode = ScrollMode.Smooth;
        volume.Programs.Add(new Profile { Name = "Browser", ProgramPath = @"C:\Apps\browser.exe", DialMode = DialMode.Volume });
        controller.Update(volume);
        WindowsInput.Routing = true;
        WindowsInput.Program = null;
        controller.Receive(new(EventKind.Rotate, Direction: 1));
        Assert(WindowsInput.Wheels.Count == 0, "algemeen vloeiend profiel plaatst draaibeweging in buffer");
        WindowsInput.Program = @"C:\Apps\browser.exe";
        WindowsInput.Routing = false;
        controller.Receive(new(EventKind.Rotate, Direction: 1));
        var volumeUp = KeyboardLayout.Stroke(KeyboardLayout.SpecificKey(0xAF), KeyboardLayout.Get());
        Assert(controller.ActiveProfile == "Browser" && WindowsInput.Events.Count == 2 && WindowsInput.Events[0].Stroke == volumeUp && !WindowsInput.Events[0].Up,
            "programmaprofiel volume rechtsom stuurt één volume omhoog aanslag");
        controller.Poll();
        Assert(WindowsInput.Wheels.Count == 0, "volume wist eerdere vloeiende scroll uit buffer");
        controller.Receive(new(EventKind.Rotate, Direction: -1));
        var volumeDown = KeyboardLayout.Stroke(KeyboardLayout.SpecificKey(0xAE), KeyboardLayout.Get());
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events[2].Stroke == volumeDown && WindowsInput.Wheels.Count == 0,
            "volume linksom stuurt één volume omlaag aanslag zonder scroll");
        volume.Programs[0].Invert = true;
        controller.Update(volume);
        WindowsInput.Events.Clear();
        controller.Receive(new(EventKind.Rotate, Direction: 1));
        Assert(WindowsInput.Events.Count == 2 && WindowsInput.Events[0].Stroke == volumeDown,
            "omkeren wisselt volume omhoog en omlaag");
        volume.Programs[0].DialMode = DialMode.Scroll;
        controller.Receive(new(EventKind.Rotate, Direction: -1));
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events[2].Stroke == volumeUp,
            "niet-opgeslagen moduswijziging verandert controllersnapshot niet");
        controller.Update(volume);
        WindowsInput.Routing = true;
        controller.Receive(new(EventKind.Rotate, Direction: 1));
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Wheels.Count == 1,
            "opslaan past programmaprofielmodus toe en hervat scrollen");
        WindowsInput.Routing = true;
        WindowsInput.Program = null;
        controller.Update(settings);

        WindowsInput.Events.Clear();
        WindowsInput.FailInput = true;
        Press(1);
        WindowsInput.FailInput = false;
        controller.Poll(); Press(1);
        Assert(WindowsInput.Events.Count == 0, "invoerfout heractiveert vastgehouden apparaatknop niet");
        Release(1); Press(1); Release(1);
        Assert(WindowsInput.Events.Count == 4, "nieuwe druk werkt na invoerfout");

        WindowsInput.Events.Clear();
        Press(1);
        WindowsInput.FailInput = true;
        controller.Pause(true);
        Assert(WindowsInput.HasPendingRelease, "controller bewaart mislukte vrijgave");
        WindowsInput.FailInput = false;
        controller.Poll();
        Assert(!WindowsInput.HasPendingRelease && WindowsInput.Events.Count == 4, "timer probeert mislukte vrijgave opnieuw, ook tijdens pauze");
        Release(1); controller.Pause(false);

        WindowsInput.Events.Clear();
        Press(1);
        controller.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Assert(WindowsInput.Events.Count == 4 && WindowsInput.Events.TakeLast(2).All(e => e.Up), "normaal afsluiten geeft vastgehouden toetsen vrij");
        WindowsInput.Reset();
        return checks;
    }
}
