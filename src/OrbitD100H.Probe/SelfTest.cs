using OrbitD100H.Core;

internal static class SelfTest
{
    public static void Run()
    {
        var checks = 0;
        checks += HoldTests.Run();
        checks += ControllerHoldTests.Run();
        void Assert(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException($"Test mislukt: {description}");
            checks++;
        }
        byte[] Frame(string code)
        {
            var frame = new byte[65];
            frame[0] = 0xFB;
            Convert.FromHexString(code).CopyTo(frame, 1);
            Convert.FromHexString("3890C499A360AAAD").CopyTo(frame, 9);
            return frame;
        }
        var keepAlive = Protocol.CreateKeepAlive(65);
        Assert(Convert.ToHexString(keepAlive.AsSpan(0, 17)) == "FB24569EF228E145A13890C499A360AAAD", "wire-format keepalive");
        Assert(keepAlive.AsSpan(17).IndexOfAnyExcept((byte)0) < 0, "ongebruikte bytes nul");
        foreach (var (enabled, code, acknowledgement) in new[]
        {
            (true, "39B056AFD36F9ABF", "A1F76ADB93F65A78"),
            (false, "5F4E1D1371B29A76", "99BF42CA2354B0AF")
        })
        {
            var haptic = Protocol.CreateHapticFeedback(enabled, 65);
            Assert(Convert.ToHexString(haptic.AsSpan(0, 17)) == "FB" + code + "3890C499A360AAAD", $"haptische opdracht {(enabled ? "aan" : "uit")}");
            Assert(haptic.AsSpan(17).IndexOfAnyExcept((byte)0) < 0, "haptisch rapport opgevuld met nullen");
            var response = Frame(acknowledgement);
            Assert(Protocol.TryDecodeHapticAcknowledgement(response, out var actual) && actual == enabled, $"haptische bevestiging {(enabled ? "aan" : "uit")}");
            response[0] = 0;
            Assert(!Protocol.TryDecodeHapticAcknowledgement(response, out _), "haptische bevestiging met verkeerd rapport-ID geweigerd");
            response[0] = 0xFB; response[9] ^= 1;
            Assert(!Protocol.TryDecodeHapticAcknowledgement(response, out _), "haptische bevestiging met verkeerd token geweigerd");
            Assert(!Protocol.TryDecodeHapticAcknowledgement(response.AsSpan(0, 64), out _), "korte haptische bevestiging geweigerd");
        }
        Assert(Protocol.CreateHapticFeedback(false, 128).Length == 128, "haptiek behoudt descriptorlengte");
        try { Protocol.CreateHapticFeedback(true, 64); throw new InvalidOperationException("Te kort haptisch outputrapport aanvaard."); }
        catch (ArgumentOutOfRangeException) { checks++; }
        string[] presses = ["8E8F5E16EC67281E", "22D73CFA751FFBC7", "983AE16D833A42A4", "3D2E482AC73D9006", "E6AE57C883933971", "2E426BB625BB36DD", "FF79B4421864A3F1", "838CC9CA145C72ED"];
        string[] releases = ["028DB3660224D0C7", "96F6B51A60F4FF67", "ABC5377EDA49BE8B", "446E3A0ED925A0A2", "DC24A2910177965F", "250846C7E37615F9", "73D198D9C446DFB2", "85138E54A3497900"];
        for (var i = 0; i < presses.Length; i++)
        {
            Assert(Protocol.TryDecode(Frame(presses[i]), out var press) && press == new DialEvent(EventKind.Press, i + 1), $"knop {i + 1} indrukken");
            Assert(Protocol.TryDecode(Frame(releases[i]), out var release) && release == new DialEvent(EventKind.Release, i + 1), $"knop {i + 1} loslaten");
        }
        Assert(Protocol.TryDecode(Frame("3EDD5F9E876AF42C"), out var right) && right == new DialEvent(EventKind.Rotate, Direction: 1), "rechtsom");
        Assert(Protocol.TryDecode(Frame("B06BE536DBE92E9D"), out var left) && left == new DialEvent(EventKind.Rotate, Direction: -1), "linksom");
        Assert(Protocol.TryDecode(Frame("21F45EB71C3587AB"), out var ack) && ack.Kind == EventKind.KeepAlive, "keepalive-antwoord");
        Assert(!Protocol.TryDecode(new byte[9], out _), "kort rapport geweigerd");
        var wrong = Frame("3EDD5F9E876AF42C");
        wrong[0] = 2;
        Assert(!Protocol.TryDecode(wrong, out _), "ander report-ID geweigerd");
        wrong[0] = 0xFB;
        wrong[9] ^= 1;
        Assert(!Protocol.TryDecode(wrong, out _), "ander token geweigerd");
        Assert(!Protocol.TryDecode(Frame("0000000000000000"), out _), "onbekend event geweigerd");
        Assert(Protocol.CreateKeepAlive(128).Length == 128, "descriptorlengte behouden");
        try { Protocol.CreateKeepAlive(64); throw new InvalidOperationException("Te kort outputrapport aanvaard."); }
        catch (ArgumentOutOfRangeException) { checks++; }
        Assert(ShortcutKeys.Create(0xDC, [0xA2]).SequenceEqual([0xA2, 0xDC]), "Ctrl links + backslash behoudt linker Ctrl");
        Assert(ShortcutKeys.Create(0xDC, [0xA3]).SequenceEqual([0xA3, 0xDC]), "Ctrl rechts + backslash behoudt rechter Ctrl");
        Assert(ShortcutKeys.Create(0xDC, [0xA4]).SequenceEqual([0xA4, 0xDC]), "Alt links behouden");
        Assert(ShortcutKeys.Create(0xDC, [0xA5]).SequenceEqual([0xA5, 0xDC]), "Alt rechts behouden");
        Assert(ShortcutKeys.Create(0xDC, [0x5B]).SequenceEqual([0x5B, 0xDC]), "Win links behouden");
        Assert(ShortcutKeys.Create(0xDC, [0x5C]).SequenceEqual([0x5C, 0xDC]), "Win rechts behouden");
        var keyboard = OrbitD100H.App.KeyboardLayout.Get();
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA2, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x1D, 8), "linker Ctrl: scan 1D zonder extended-bit");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA3, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x1D, 9), "rechter Ctrl: scan 1D met extended-bit");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA4, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x38, 8), "linker Alt: scan 38 zonder extended-bit");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA5, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x38, 9), "rechter Alt: scan 38 met extended-bit");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0x5B, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x5B, 9), "linker Win: eigen scan code");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0x5C, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x5C, 9), "rechter Win: eigen scan code");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA0, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x2A, 8), "linker Shift: eigen scan code");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xA1, keyboard) == new OrbitD100H.App.KeyboardStroke(0, 0x36, 8), "rechter Shift: eigen scan code");
        if ((keyboard.ToInt64() & 0xFFFF) == 0x0409)
            Assert(OrbitD100H.App.KeyboardLayout.Character(0xDC, keyboard) == "\\", "OEM-toets toont backslash volgens Amerikaanse toetsenbordindeling");
        Assert(OrbitD100H.App.KeyboardLayout.Stroke(0xB3, keyboard) == new OrbitD100H.App.KeyboardStroke(0xB3, 0, 1), "media-invoer blijft ongewijzigd");
        Assert(Protocol.TryDecode(Frame("2E426BB625BB36DD"), out var physicalOne) && ButtonLayout.Number(physicalOne.Button) == 1,
            "gemelde fysieke knop 1 mag niet als knop 3 verschijnen");
        Assert(ButtonLayout.Number(6) == 1 && ButtonLayout.Number(1) == 3 && ButtonLayout.Number(3) == 5,
            "geannoteerde knopnummers: links onder 1, boven links 3, boven rechts 5");
        Assert(ButtonLayout.Number(4) == 2 && ButtonLayout.Number(2) == 4 && ButtonLayout.Number(5) == 6
            && ButtonLayout.Number(7) == 7 && ButtonLayout.Number(8) == 8, "overige geannoteerde knopnummers");
        Assert(ButtonLayout.ProtocolId(1) == 6 && ButtonLayout.ProtocolId(3) == 1 && ButtonLayout.ProtocolId(6) == 5,
            "editor kiest juiste bestaande binding voor zichtbaar knopnummer");
        for (var id = 1; id <= 8; id++)
            Assert(ButtonLayout.ProtocolId(ButtonLayout.Number(id)) == id, $"binding blijft gekoppeld aan protocolcode {id}");
        var capture = new ShortcutCaptureState();
        Assert(capture.Process(0x11, 0x1D, false, false).Suppress, "opname slikt linker Ctrl in");
        Assert(capture.Process(0xDC, 0x2B, false, false).Suppress, "opname slikt backslash in");
        Assert(!capture.Process(0xDC, 0x2B, false, false).Completed, "autorepeat voltooit opname niet");
        Assert(!capture.Process(0xDC, 0x2B, false, true).Completed, "opname wacht op loslaten van Ctrl");
        var chord = capture.Process(0x11, 0x1D, false, true);
        Assert(chord.Suppress && chord.Completed && chord.Keys!.SequenceEqual([0xA2, 0xDC]), "opname Ctrl links + backslash");
        Assert(!capture.Process(0x41, 0x1E, false, false).Suppress, "na opname laat recorder toetsen door");
        capture = new();
        capture.Process(0x11, 0x1D, true, false);
        capture.Process(0xDC, 0x2B, false, false);
        capture.Process(0xDC, 0x2B, false, true);
        Assert(capture.Process(0x11, 0x1D, true, true).Keys!.SequenceEqual([0xA3, 0xDC]), "extended-bit onderscheidt rechter Ctrl");
        capture = new([0xA2]);
        Assert(!capture.Process(0xA2, 0x1D, false, false).Suppress, "eerder ingedrukte Ctrl wordt niet ingeslikt");
        capture.Process(0xDC, 0x2B, false, false);
        Assert(capture.Process(0xDC, 0x2B, false, true).Keys!.SequenceEqual([0xA2, 0xDC]), "Ctrl vast voor begin opname werkt");
        Assert(!capture.Process(0xA2, 0x1D, false, true).Suppress, "eerder ingedrukte Ctrl krijgt normaal key-up");
        capture = new();
        capture.Process(0x1B, 1, false, false);
        var cancelled = capture.Process(0x1B, 1, false, true);
        Assert(cancelled.Suppress && cancelled.Completed && cancelled.Keys is null, "Escape annuleert zonder nieuwe binding");
        capture = new();
        capture.Process(0x12, 0x38, true, false);
        capture.Process(0x09, 0x0F, false, false);
        capture.Process(0x09, 0x0F, false, true);
        Assert(capture.Process(0x12, 0x38, true, true).Keys!.SequenceEqual([0xA5, 0x09]), "rechter Alt + Tab vastleggen zonder focusnavigatie");
        capture = new();
        capture.Process(0x5C, 0x5C, true, false);
        capture.Process(0x4B, 0x25, false, false);
        capture.Process(0x4B, 0x25, false, true);
        Assert(capture.Process(0x5C, 0x5C, true, true).Keys!.SequenceEqual([0x5C, 0x4B]), "rechter Windows-toets behouden");
        Assert(!new ShortcutCaptureState().Process(0xE7, 0x5C, false, false).Suppress, "Unicode-tekstpakket is geen fysieke hotkey");
        var settings = new Settings();
        var legacySettings = System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"Version\":1}")!;
        Assert(legacySettings.HapticFeedbackEnabled, "oude instellingen behouden standaard haptische feedback");
        var legacy = System.Text.Json.JsonSerializer.Deserialize<Profile>("{\"Name\":\"Legacy\",\"Mode\":0,\"Speed\":3}")!;
        Assert(legacy.DialMode == DialMode.Scroll, "oud profielbestand behoudt scrollmodus");
        settings.Programs.Add(new Profile { Name = "Browser", ProgramPath = @"C:\Apps\browser.exe", Speed = 2, Mode = ScrollMode.Smooth });
        settings.Validate();
        Assert(settings.Select(null) == settings.General, "ontbrekend proces gebruikt algemeen profiel");
        Assert(settings.Select(@"C:\APPS\BROWSER.EXE").Name == "Browser", "programmapad is hoofdletterongevoelig");
        Assert(settings.Select(@"C:\Apps\ander.exe") == settings.General, "onbekend programma gebruikt algemeen profiel");
        Assert(settings.General.WheelDelta(1) == -360 && settings.General.WheelDelta(-1) == 360, "standaard scrollrichting en snelheid");
        settings.General.Invert = true;
        Assert(settings.General.WheelDelta(1) == 360, "omgekeerde richting");
        var snapshot = settings.Copy();
        settings.General.Speed = 12;
        Assert(snapshot.General.Speed == 3, "bewerkingen wijzigen actief profiel niet voor opslaan");
        settings.General.DialMode = DialMode.Volume;
        Assert(settings.Copy().General.DialMode == DialMode.Volume, "kopie behoudt draaiknopmodus");
        var invalidDialMode = settings.Copy(); invalidDialMode.General.DialMode = (DialMode)2;
        try { invalidDialMode.Validate(); throw new Exception("Ongeldige draaiknopmodus aanvaard."); }
        catch (InvalidDataException) { checks++; }
        Assert(new ButtonAction { Kind = ActionKind.PlayPause }.GetKeys().SequenceEqual([0xB3]), "mediaactie");
        Assert(new ButtonAction { Kind = ActionKind.Shortcut, Keys = [0x11, 0x43] }.GetKeys().SequenceEqual([0x11, 0x43]), "sneltoets");
        Assert(new ButtonAction().GetKeys().Length == 0, "niet toegewezen knop");
        Assert(ShortcutKeys.Create(0x53, true, true, false, false).SequenceEqual([0x11, 0x10, 0x53]), "Ctrl + Shift + S");
        Assert(ShortcutKeys.Create(0x4B, true, false, true, false).SequenceEqual([0x11, 0x12, 0x4B]), "Ctrl + Alt + K");
        Assert(ShortcutKeys.Create(0x53, false, true, false, true).SequenceEqual([0x10, 0x5B, 0x53]), "Win + Shift + S");
        Assert(ShortcutKeys.Create(0x4B, true, true, true, true).SequenceEqual([0x11, 0x10, 0x12, 0x5B, 0x4B]), "vier modifiers en één hoofdtoets");
        Assert(ShortcutKeys.Create(0x1B, false, false, false, false).SequenceEqual([0x1B]), "losse Escape-toets blijft mogelijk");
        try { ShortcutKeys.Create(0x11, false, false, false, false); throw new Exception("Modifier als hoofdtoets aanvaard."); }
        catch (ArgumentOutOfRangeException) { checks++; }
        var buttons = new ButtonState();
        Assert(buttons.ShouldTrigger(new(EventKind.Press, 1)), "knop activeert bij indrukken");
        Assert(!buttons.ShouldTrigger(new(EventKind.Press, 1)), "herhaald indrukken genegeerd");
        Assert(!buttons.ShouldTrigger(new(EventKind.Release, 1)), "gewone knop activeert niet bij loslaten");
        Assert(!buttons.ShouldTrigger(new(EventKind.Press, 8)), "draaiknop activeert niet bij indrukken");
        Assert(buttons.ShouldTrigger(new(EventKind.Release, 8)), "draaiknop activeert bij loslaten");
        Assert(!buttons.ShouldTrigger(new(EventKind.Release, 8)), "dubbel loslaten activeert niet opnieuw");
        buttons.ShouldTrigger(new(EventKind.Press, 8)); buttons.Clear();
        Assert(!buttons.ShouldTrigger(new(EventKind.Release, 8)), "reset voorkomt achterstallige knopactie");
        var scroll = new ScrollBuffer();
        foreach (var delta in new[] { -360, 360, 1, -1, 1440 })
        {
            scroll.Add(delta, 10, 0);
            var total = 0;
            for (var time = 8; time <= 96; time += 8) total += scroll.Take(10, time);
            Assert(total == delta && !scroll.HasPending, $"vloeiend behoudt totale delta {delta}");
        }
        scroll.Add(360, 10, 100); scroll.Add(120, 10, 108);
        Assert(scroll.Take(10, 204) == 480, "opeenvolgende draaistappen worden opgeteld");
        scroll.Add(360, 10, 300);
        Assert(scroll.Take(11, 308) == 0 && !scroll.HasPending, "doelwisseling stopt resterende beweging");
        scroll.Add(360, 10, 400); scroll.Clear();
        Assert(scroll.Take(10, 500) == 0, "pauzeren wist resterende beweging");
        var temporary = Path.Combine(Path.GetTempPath(), "D100H-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var store = new SettingsStore(temporary);
            settings.General.Buttons[1] = new() { Kind = ActionKind.Shortcut, Keys = ShortcutKeys.Create(0x53, true, true, false, false) };
            settings.General.Buttons[2] = new() { Kind = ActionKind.Shortcut, Keys = ShortcutKeys.Create(0xDC, [0xA2]) };
            settings.General.Buttons[3] = new() { Kind = ActionKind.Shortcut, Keys = ShortcutKeys.Create(0xDC, [0xA3]) };
            settings.General.Buttons[4] = new() { Kind = ActionKind.Shortcut, Keys = ShortcutKeys.Create(0xDC, [0xA2, 0xA3, 0xA0, 0xA1, 0xA4, 0xA5, 0x5B, 0x5C]) };
            store.Save(settings);
            Assert(new SettingsStore(temporary).Load().Programs[0].Mode == ScrollMode.Smooth, "instellingen rondreis");
            Assert(new SettingsStore(temporary).Load().General.DialMode == DialMode.Volume, "draaiknopmodus blijft na herstart behouden");
            Assert(new SettingsStore(temporary).Load().General.Buttons[1].GetKeys().SequenceEqual([0x11, 0x10, 0x53]), "toetscombinatie blijft na herstart behouden");
            Assert(new SettingsStore(temporary).Load().General.Buttons[2].GetKeys().SequenceEqual([0xA2, 0xDC]), "Ctrl links + backslash blijft na herstart behouden");
            Assert(new SettingsStore(temporary).Load().General.Buttons[3].GetKeys().SequenceEqual([0xA3, 0xDC]), "Ctrl rechts + backslash blijft na herstart behouden");
            Assert(new SettingsStore(temporary).Load().General.Buttons[4].GetKeys().Length == 9, "acht fysieke modifiers plus hoofdtoets zijn geldig");
            settings.HapticFeedbackEnabled = false; store.Save(settings);
            Assert(!new SettingsStore(temporary).Load().HapticFeedbackEnabled, "uitgeschakelde haptiek blijft na herstart uit");
            Assert(!settings.Copy().HapticFeedbackEnabled, "kopie behoudt uitgeschakelde haptiek");
            settings.HapticFeedbackEnabled = true; store.Save(settings);
            Assert(new SettingsStore(temporary).Load().HapticFeedbackEnabled, "ingeschakelde haptiek blijft na herstart aan");
            settings.General.Speed = 4; store.Save(settings);
            Assert(new SettingsStore(temporary).Load().General.Speed == 4, "bestaand bestand atomair vervangen");
            File.WriteAllText(store.FilePath, "{beschadigd");
            var broken = new SettingsStore(temporary); broken.Load();
            Assert(broken.LoadError is not null, "beschadigde instellingen worden gemeld");
            try { broken.Save(settings); throw new Exception("Beschadigd bestand overschreven."); }
            catch (InvalidOperationException) { checks++; }
            Assert(File.ReadAllText(store.FilePath) == "{beschadigd", "beschadigd bestand behouden");
            settings.Programs.Add(settings.Programs[0]);
            try { settings.Validate(); throw new Exception("Dubbel profiel aanvaard."); }
            catch (InvalidDataException) { checks++; }
        }
        finally
        {
            foreach (var file in Directory.GetFiles(temporary)) File.Delete(file);
            Directory.Delete(temporary);
        }
        using (var slow = new SlowHidStream())
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
        {
            var connected = false;
            var session = DialService.RunSessionAsync(slow, 65, 65, _ => { }, (state, _) => connected |= state, stop.Token);
            try { slow.ThirdWrite.Task.WaitAsync(TimeSpan.FromSeconds(7)).GetAwaiter().GetResult(); }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(connected, "vendorantwoord bij vertraagd transport herkend");
            var intervals = slow.KeepAliveTimes.Zip(slow.KeepAliveTimes.Skip(1), (a, b) => System.Diagnostics.Stopwatch.GetElapsedTime(a, b)).ToArray();
            Assert(intervals.Length == 2 && intervals.All(t => t.TotalMilliseconds is > 700 and < 1400),
                "keepalive-cadans telt Bluetooth-schrijfvertraging niet bij het interval op");
            Assert(!slow.ConcurrentWriteDetected, "keepalive en haptiek delen één schrijver");
        }
        using (var slow = new SlowHidStream())
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(6)))
        {
            var session = DialService.RunSessionAsync(slow, 65, 65, _ => { }, (_, _) => { }, stop.Token, () => true);
            try { slow.ThirdWrite.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            var intervals = slow.KeepAliveTimes.Zip(slow.KeepAliveTimes.Skip(1), (a, b) => System.Diagnostics.Stopwatch.GetElapsedTime(a, b)).ToArray();
            Assert(intervals.Length == 2 && intervals.All(t => t.TotalMilliseconds is > 700 and < 1400),
                "haptische schrijftijd vervormt de vaste keepalive-cadans niet");
            Assert(slow.Writes.Any(w => Convert.ToHexString(w.AsSpan(1, 8)) == "39B056AFD36F9ABF")
                && !slow.ConcurrentWriteDetected, "haptiek behoudt één schrijver tijdens keepalive");
        }
        checks += HapticSessionTests();
        Console.WriteLine($"Geslaagd: {checks} controles voor protocol, profielen, acties, vasthouden, scrollen, opslag en verbindingscadans.");
    }

    private static int HapticSessionTests()
    {
        var checks = 0;
        void Assert(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException($"Test mislukt: {description}");
            checks++;
        }
        static bool HasHaptic(SlowHidStream stream, bool enabled) => stream.Writes.Any(w =>
            Convert.ToHexString(w.AsSpan(1, 8)) == (enabled ? "39B056AFD36F9ABF" : "5F4E1D1371B29A76"));

        using (var stream = new SlowHidStream { WriteDelay = TimeSpan.FromMilliseconds(10) })
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(9)))
        {
            var desired = true;
            var received = new System.Collections.Concurrent.ConcurrentQueue<DialEvent>();
            var statuses = new System.Collections.Concurrent.ConcurrentQueue<(bool Connected, string Message)>();
            var session = DialService.RunSessionAsync(stream, 65, 65, received.Enqueue,
                (connected, message) => statuses.Enqueue((connected, message)), stop.Token, () => desired);
            try
            {
                SpinWait.SpinUntil(() => HasHaptic(stream, true), TimeSpan.FromSeconds(5));
                SpinWait.SpinUntil(() => stream.Writes.Any(w => Convert.ToHexString(w.AsSpan(1, 8)) == "39B056AFD36F9ABF")
                    && stream.Writes.Any(w => Convert.ToHexString(w.AsSpan(1, 8)) == "24569EF228E145A1"), TimeSpan.FromMilliseconds(500));
                Thread.Sleep(700);
                desired = false;
                SpinWait.SpinUntil(() => HasHaptic(stream, false), TimeSpan.FromSeconds(4));
            }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(HasHaptic(stream, true) && HasHaptic(stream, false), "session stuurt beide haptische keuzes na wijziging");
            Assert(stream.Writes.Count(w => Convert.ToHexString(w.AsSpan(1, 8)) == "39B056AFD36F9ABF") == 1
                && stream.Writes.Count(w => Convert.ToHexString(w.AsSpan(1, 8)) == "5F4E1D1371B29A76") == 1,
                "haptische opdracht wordt alleen bij een keuzeverandering verzonden");
            Assert(received.Count == 0, "haptische en keepalive-antwoorden worden niet als knopinvoer gemeld");
            Assert(stream.KeepAliveTimes.Count >= 2 && !stream.ConcurrentWriteDetected,
                "haptiek behoudt keepalive en gebruikt dezelfde schrijver");
            Assert(statuses.Any(s => s.Connected && s.Message == "D100H verbonden"), "haptische bevestigingen gelden niet als keepalive");
        }

        using (var stream = new SlowHidStream { AcknowledgeHaptics = false, WriteDelay = TimeSpan.FromMilliseconds(10) })
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
        {
            var statuses = new System.Collections.Concurrent.ConcurrentQueue<(bool Connected, string Message)>();
            var session = DialService.RunSessionAsync(stream, 65, 65, _ => { },
                (connected, message) => statuses.Enqueue((connected, message)), stop.Token, () => false);
            var stayedConnectedThroughWarning = false;
            try
            {
                var warned = SpinWait.SpinUntil(() => statuses.Any(s => s.Message.Contains("Haptische feedback niet bevestigd")), TimeSpan.FromSeconds(7));
                var observed = statuses.ToArray();
                var firstConnected = Array.FindIndex(observed, s => s.Connected);
                stayedConnectedThroughWarning = warned && firstConnected >= 0
                    && observed.Skip(firstConnected).All(s => s.Connected);
            }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(statuses.Any(s => s.Connected && s.Message.Contains("Haptische feedback niet bevestigd")),
                "ontbrekende haptische bevestiging toont statuswaarschuwing");
            Assert(stayedConnectedThroughWarning,
                "ontbrekende haptische bevestiging verbreekt verbinding niet");
            Assert(stream.Writes.Count(w => Convert.ToHexString(w.AsSpan(1, 8)) == "5F4E1D1371B29A76") == 1,
                "ontbrekende bevestiging veroorzaakt geen herhaalde haptiekopdracht");
            Assert(statuses.Count(s => s.Message.Contains("Haptische feedback niet bevestigd")) == 1,
                "ontbrekende bevestiging toont één waarschuwing");
        }

        using (var stream = new SlowHidStream { WrongHapticAcknowledgement = true, WriteDelay = TimeSpan.FromMilliseconds(10) })
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(7)))
        {
            var statuses = new System.Collections.Concurrent.ConcurrentQueue<(bool Connected, string Message)>();
            var session = DialService.RunSessionAsync(stream, 65, 65, _ => { },
                (connected, message) => statuses.Enqueue((connected, message)), stop.Token, () => false);
            try { SpinWait.SpinUntil(() => statuses.Any(s => s.Message.Contains("Haptische feedback niet bevestigd")), TimeSpan.FromSeconds(6)); }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(statuses.Any(s => s.Connected && s.Message.Contains("Haptische feedback niet bevestigd")),
                "bevestiging van verkeerde haptische keuze wordt genegeerd");
            Assert(statuses.Count(s => s.Message.Contains("Haptische feedback niet bevestigd")) == 1,
                "verkeerde bevestiging toont één waarschuwing");
        }

        using (var stream = new SlowHidStream { SendOppositeHapticAcknowledgement = true, WriteDelay = TimeSpan.FromMilliseconds(10) })
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(7)))
        {
            var statuses = new System.Collections.Concurrent.ConcurrentQueue<(bool Connected, string Message)>();
            var session = DialService.RunSessionAsync(stream, 65, 65, _ => { },
                (connected, message) => statuses.Enqueue((connected, message)), stop.Token, () => true);
            try { Thread.Sleep(TimeSpan.FromSeconds(4.5)); }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(statuses.All(s => !s.Message.Contains("Haptische feedback niet bevestigd")),
                "late opposite ACK does not undo matching confirmation");
        }

        using (var stream = new SlowHidStream { FailWrites = true })
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
        {
            var session = DialService.RunSessionAsync(stream, 65, 65, _ => { }, (_, _) => { }, stop.Token, () => true);
            try { session.GetAwaiter().GetResult(); throw new InvalidOperationException("Schrijffout is niet doorgegeven."); }
            catch (IOException e) when (e.Message == "simulated write failure") { checks++; }
        }

        foreach (var _ in Enumerable.Range(0, 2))
        {
            using var stream = new SlowHidStream { WriteDelay = TimeSpan.FromMilliseconds(10) };
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var session = DialService.RunSessionAsync(stream, 65, 65, _ => { }, (_, _) => { }, stop.Token, () => false);
            try { SpinWait.SpinUntil(() => HasHaptic(stream, false), TimeSpan.FromSeconds(4)); }
            finally
            {
                stop.Cancel();
                try { session.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            Assert(HasHaptic(stream, false), "nieuwe session stuurt huidige haptische keuze");
        }
        return checks;
    }
}
