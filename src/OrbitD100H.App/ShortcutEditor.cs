using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using OrbitD100H.Core;

namespace OrbitD100H.App;

internal sealed class ShortcutEditor : StackPanel
{
    private sealed record KeyChoice(int Code)
    {
        public string Name => Code == 0 ? "Kies een toets…" : ChoiceName(Code);
        public override string ToString() => Name;
    }

    private static readonly int[] ModifierOrder = [0x11, 0xA2, 0xA3, 0x10, 0xA0, 0xA1, 0x12, 0xA4, 0xA5, 0x5B, 0x5C];
    private readonly Dictionary<int, CheckBox> modifierBoxes = [];
    private readonly ComboBox key = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button record = new() { Content = "Combinatie vastleggen", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock summary = new() { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<KeyChoice> choices;
    private bool loading;
    private CancellationTokenSource? capture;
    public event Action? Changed;
    public event Action<bool>? RecordingChanged;

    public ShortcutEditor(string label)
    {
        choices = Enumerable.Range(1, 254).Where(k => !ShortcutKeys.IsModifier(k) && KeyInterop.KeyFromVirtualKey(k) != Key.None)
            .Select(k => new KeyChoice(k)).OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        choices.Insert(0, new(0));
        key.ItemsSource = choices;
        key.SelectedIndex = -1;
        System.Windows.Automation.AutomationProperties.SetName(key, label + " hoofdtoets");
        var modifiers = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        for (var column = 0; column < 4; column++) modifiers.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        foreach (var group in new[] { (Name: "Ctrl", Left: 0xA2, Right: 0xA3, Generic: 0x11),
            (Name: "Shift", Left: 0xA0, Right: 0xA1, Generic: 0x10),
            (Name: "Alt", Left: 0xA4, Right: 0xA5, Generic: 0x12),
            (Name: "Win", Left: 0x5B, Right: 0x5C, Generic: 0) })
        {
            var row = modifiers.RowDefinitions.Count;
            modifiers.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var heading = new TextBlock { Text = group.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetRow(heading, row); modifiers.Children.Add(heading);
            foreach (var (code, title, column) in new[] { (group.Left, "Links", 1), (group.Right, "Rechts", 2), (group.Generic, "Algemeen", 3) })
            {
                if (code == 0) continue;
                var box = new CheckBox { Content = title, Margin = new Thickness(0, 4, 12, 4),
                    Visibility = code == group.Generic ? Visibility.Collapsed : Visibility.Visible };
                modifierBoxes[code] = box;
                System.Windows.Automation.AutomationProperties.SetName(box, $"{label} {group.Name} {title.ToLowerInvariant()}");
                Grid.SetRow(box, row); Grid.SetColumn(box, column); modifiers.Children.Add(box);
                box.Click += (_, _) =>
                {
                    if (box.IsChecked == true && group.Generic != 0)
                    {
                        if (code == group.Generic) { modifierBoxes[group.Left].IsChecked = false; modifierBoxes[group.Right].IsChecked = false; }
                        else { modifierBoxes[group.Generic].IsChecked = false; modifierBoxes[group.Generic].Visibility = Visibility.Collapsed; }
                    }
                    Notify();
                };
            }
        }
        Children.Add(modifiers);
        Children.Add(key);
        Children.Add(record);
        Children.Add(summary);
        key.SelectionChanged += (_, _) => Notify();
        record.Click += Record;
        IsVisibleChanged += (_, _) => { if (!IsVisible) CancelCapture(); };
        Unloaded += (_, _) => CancelCapture();
        UpdateSummary();
    }

    public int[] Keys
    {
        get => key.SelectedItem is KeyChoice { Code: > 0 } selected ? ShortcutKeys.Create(selected.Code,
            ModifierOrder.Where(code => modifierBoxes[code].IsChecked == true)) : [];
        set
        {
            loading = true;
            try
            {
                foreach (var (code, box) in modifierBoxes)
                {
                    box.IsChecked = value.Contains(code);
                    if (code is 0x10 or 0x11 or 0x12)
                        box.Visibility = box.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                }
                var main = value.LastOrDefault(k => !ShortcutKeys.IsModifier(k));
                if (main != 0 && choices.All(c => c.Code != main))
                { choices.Add(new(main)); key.Items.Refresh(); }
                key.SelectedItem = choices.FirstOrDefault(c => c.Code == main);
                CancelCapture();
            }
            finally { loading = false; UpdateSummary(); }
        }
    }

    private async void Record(object sender, RoutedEventArgs e)
    {
        if (capture is not null) { CancelCapture(); return; }
        using var cancellation = new CancellationTokenSource();
        capture = cancellation;
        record.Content = "Annuleren";
        key.IsEnabled = false;
        foreach (var box in modifierBoxes.Values) box.IsEnabled = false;
        summary.Text = "Druk de combinatie in en laat alle toetsen los. Esc annuleert. Maximaal 20 seconden.";
        RecordingChanged?.Invoke(true);
        string? error = null;
        try
        {
            var result = await KeyboardRecorder.CaptureAsync(cancellation.Token);
            if (result is not null && !cancellation.IsCancellationRequested)
            {
                capture = null;
                Keys = result;
                Notify();
            }
        }
        catch (Win32Exception ex) { error = "Opname mislukt: " + ex.Message; }
        finally
        {
            capture = null;
            record.Content = "Combinatie vastleggen";
            key.IsEnabled = true;
            foreach (var box in modifierBoxes.Values) box.IsEnabled = true;
            UpdateSummary();
            if (error is not null) summary.Text = error;
            RecordingChanged?.Invoke(false);
        }
    }

    public void CancelCapture() => capture?.Cancel();
    public void RefreshLayout() { key.Items.Refresh(); UpdateSummary(); }
    private void Notify() { if (loading) return; UpdateSummary(); Changed?.Invoke(); }
    private void UpdateSummary() => summary.Text = Keys.Length == 0 ? "Kies een toets, bijvoorbeeld S." :
        string.Join(" + ", Keys.Select(KeyName));
    private static string KeyName(int value) => value switch
    {
        0x10 => "Shift (algemeen)", 0x11 => "Ctrl (algemeen)", 0x12 => "Alt (algemeen)",
        0xA0 => "Shift links", 0xA1 => "Shift rechts", 0xA2 => "Ctrl links", 0xA3 => "Ctrl rechts",
        0xA4 => "Alt links", 0xA5 => "Alt rechts", 0x5B => "Win links", 0x5C => "Win rechts",
        0x08 => "Backspace", 0x0D => "Enter", 0x1B => "Esc", 0x20 => "Spatie",
        _ => SymbolName(value)
    };

    private static string SymbolName(int value)
    {
        var name = KeyInterop.KeyFromVirtualKey(value).ToString();
        if (name.StartsWith("Oem", StringComparison.Ordinal) || name.StartsWith("Abnt", StringComparison.Ordinal))
            return KeyboardLayout.Character((ushort)value, KeyboardLayout.Get()) ?? name;
        return name == "None" ? $"Toets {value:X2}" : name;
    }
    private static string ChoiceName(int value)
    {
        var name = KeyName(value);
        var raw = KeyInterop.KeyFromVirtualKey(value).ToString();
        return raw.StartsWith("Oem", StringComparison.Ordinal) ? $"{name} ({raw})" : name;
    }
}
