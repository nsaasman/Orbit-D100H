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
        public string Name => Code == 0 ? Text.Get("ChooseKey") : ChoiceName(Code);
        public override string ToString() => Name;
    }

    private static readonly int[] ModifierOrder = [0x11, 0xA2, 0xA3, 0x10, 0xA0, 0xA1, 0x12, 0xA4, 0xA5, 0x5B, 0x5C];
    private readonly Dictionary<int, CheckBox> modifierBoxes = [];
    private readonly ComboBox key = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button record = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock summary = new() { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<KeyChoice> choices;
    private bool loading;
    private CancellationTokenSource? capture;
    private Task recording = Task.CompletedTask;
    private LocalizedText? recordingError;
    private readonly int button;
    public event Action? Changed;
    public event Action<bool>? RecordingChanged;

    public ShortcutEditor(int button)
    {
        this.button = button;
        choices = Enumerable.Range(1, 254).Where(k => !ShortcutKeys.IsModifier(k) && KeyInterop.KeyFromVirtualKey(k) != Key.None)
            .Select(k => new KeyChoice(k)).OrderBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        choices.Insert(0, new(0));
        key.ItemsSource = choices;
        key.SelectedIndex = -1;
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
            foreach (var (code, side, column) in new[] { (group.Left, "Left", 1), (group.Right, "Right", 2), (group.Generic, "Generic", 3) })
            {
                if (code == 0) continue;
                var box = new CheckBox { Tag = side, Margin = new Thickness(0, 4, 12, 4),
                    Visibility = code == group.Generic ? Visibility.Collapsed : Visibility.Visible };
                modifierBoxes[code] = box;
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
        RefreshLanguage();
    }

    public int[] Keys
    {
        get => key.SelectedItem is KeyChoice { Code: > 0 } selected ? ShortcutKeys.Create(selected.Code,
            ModifierOrder.Where(code => modifierBoxes[code].IsChecked == true)) : [];
        set
        {
            loading = true;
            recordingError = null;
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

    private void Record(object sender, RoutedEventArgs e)
    {
        if (capture is not null) { CancelCapture(); return; }
        recording = RecordAsync();
    }

    private async Task RecordAsync()
    {
        using var cancellation = new CancellationTokenSource();
        capture = cancellation;
        recordingError = null;
        record.Content = Text.Get("Cancel");
        key.IsEnabled = false;
        foreach (var box in modifierBoxes.Values) box.IsEnabled = false;
        summary.Text = Text.Get("RecordingHelp");
        RecordingChanged?.Invoke(true);
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
        catch (Win32Exception ex) { recordingError = new("RecordingError", Text.Error(ex)); }
        finally
        {
            capture = null;
            record.Content = Text.Get("RecordShortcut");
            key.IsEnabled = true;
            foreach (var box in modifierBoxes.Values) box.IsEnabled = true;
            UpdateSummary();
            RecordingChanged?.Invoke(false);
        }
    }

    public void CancelCapture() => capture?.Cancel();
    public async Task CancelCaptureAsync() { CancelCapture(); await recording; }
    public void RefreshLanguage()
    {
        System.Windows.Automation.AutomationProperties.SetName(key, Text.Get("ButtonMainKey", button));
        foreach (var (code, box) in modifierBoxes)
        {
            box.Content = Text.Get((string)box.Tag);
            System.Windows.Automation.AutomationProperties.SetName(box, Text.Get("ButtonModifier", button, KeyName(code)));
        }
        System.Windows.Automation.AutomationProperties.SetName(record, Text.Get("ButtonModifier", button, Text.Get("RecordShortcut")));
        record.Content = Text.Get(capture is null ? "RecordShortcut" : "Cancel");
        RefreshLayout();
    }
    public void RefreshLayout() { key.Items.Refresh(); UpdateSummary(); }
    private void Notify() { if (loading) return; recordingError = null; UpdateSummary(); Changed?.Invoke(); }
    private void UpdateSummary() => summary.Text = recordingError?.ToString() ?? (capture is not null ? Text.Get("RecordingHelp") :
        Keys.Length == 0 ? Text.Get("ChooseKeyHelp") : string.Join(" + ", Keys.Select(KeyName)));
    private static string KeyName(int value) => value switch
    {
        0x10 => Text.Get("ModifierGeneric", "Shift"), 0x11 => Text.Get("ModifierGeneric", "Ctrl"), 0x12 => Text.Get("ModifierGeneric", "Alt"),
        0xA0 => Text.Get("ModifierLeft", "Shift"), 0xA1 => Text.Get("ModifierRight", "Shift"),
        0xA2 => Text.Get("ModifierLeft", "Ctrl"), 0xA3 => Text.Get("ModifierRight", "Ctrl"),
        0xA4 => Text.Get("ModifierLeft", "Alt"), 0xA5 => Text.Get("ModifierRight", "Alt"),
        0x5B => Text.Get("ModifierLeft", "Win"), 0x5C => Text.Get("ModifierRight", "Win"),
        0x08 => Text.Get("Backspace"), 0x0D => Text.Get("Enter"), 0x1B => Text.Get("Escape"), 0x20 => Text.Get("Space"),
        0x2E => Text.Get("Delete"), 0x2D => Text.Get("Insert"), 0x24 => Text.Get("Home"), 0x23 => Text.Get("End"),
        0x21 => Text.Get("PageUp"), 0x22 => Text.Get("PageDown"),
        0x25 => Text.Get("ArrowLeft"), 0x26 => Text.Get("ArrowUp"), 0x27 => Text.Get("ArrowRight"), 0x28 => Text.Get("ArrowDown"),
        _ => SymbolName(value)
    };

    private static string SymbolName(int value)
    {
        var name = KeyInterop.KeyFromVirtualKey(value).ToString();
        if (name.StartsWith("Oem", StringComparison.Ordinal) || name.StartsWith("Abnt", StringComparison.Ordinal))
            return KeyboardLayout.Character((ushort)value, KeyboardLayout.Get()) ?? name;
        return name == "None" ? Text.Get("KeyCode", value) : name;
    }
    private static string ChoiceName(int value)
    {
        var name = KeyName(value);
        var raw = KeyInterop.KeyFromVirtualKey(value).ToString();
        return raw.StartsWith("Oem", StringComparison.Ordinal) ? $"{name} ({raw})" : name;
    }
}
