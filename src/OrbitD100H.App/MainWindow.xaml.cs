using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbitD100H.Core;
using OrbitD100H.App.Theme;
using Microsoft.Win32;

namespace OrbitD100H.App;

public partial class MainWindow : Window
{
    private readonly Controller controller;
    private readonly SettingsStore store;
    private readonly Settings settings;
    private readonly ObservableCollection<Profile> profiles;
    private readonly Dictionary<int, (ComboBox Kind, ShortcutEditor Shortcut, FrameworkElement Row)> mappings = [];
    private readonly HashSet<ShortcutEditor> activeRecorders = [];
    private int selectedButton = ButtonLayout.ProtocolId(1);
    private Profile? selected;
    private bool loading = true;
    private bool dirty;
    private bool pausedBeforeShutdown;
    private bool arranging;
    private bool fitting;
    private bool? stackedLayout;
    private readonly Action exit;
    internal bool AllowClose { get; set; }
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string[] ActionKeys = ["NoAction", "Shortcut", "PlayPause", "VolumeUp", "VolumeDown", "Mute"];
    private LocalizedText feedback = new("ApplyAfterSave");

    internal MainWindow(Controller controller, SettingsStore store, Settings settings, Action exit, LocalizedText? languageError = null)
    {
        this.controller = controller; this.store = store; this.settings = settings; this.exit = exit;
        var dark = Appearance.Initialize(out var themeError);
        InitializeComponent();
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Text.Culture.Name);
        LanguageCombo.SelectedValue = Text.Culture.Name;
        HapticFeedbackCheck.IsChecked = settings.HapticFeedbackEnabled;
        ScrollDialButton.IsChecked = true;
        DarkThemeCheck.IsChecked = dark;
        DarkThemeCheck.Content = Text.Get(dark ? "DarkTheme" : "LightTheme");
        profiles = new(settings.Programs.Prepend(settings.General));
        ProfilesList.ItemsSource = profiles;
        for (var button = 1; button <= 8; button++)
        {
            var row = new StackPanel { Visibility = button == selectedButton ? Visibility.Visible : Visibility.Collapsed };
            var label = new TextBlock { Text = Text.Get("Button", ButtonLayout.Number(button)), FontSize = 20, FontWeight = FontWeights.SemiBold };
            var position = new TextBlock { Text = DeviceDiagram.PositionName(button), Margin = new Thickness(0, 4, 0, 16) };
            position.SetResourceReference(ForegroundProperty, "Muted");
            var kind = new ComboBox { ItemsSource = ActionKeys.Select(k => Text.Get(k)).ToArray(), HorizontalAlignment = HorizontalAlignment.Left };
            var shortcut = new ShortcutEditor(ButtonLayout.Number(button)) { Visibility = Visibility.Collapsed };
            System.Windows.Automation.AutomationProperties.SetName(kind, Text.Get("ButtonAction", ButtonLayout.Number(button)));
            kind.SelectionChanged += (_, _) => { shortcut.Visibility = kind.SelectedIndex == (int)ActionKind.Shortcut ? Visibility.Visible : Visibility.Collapsed; SetDirty(); };
            shortcut.Changed += SetDirty;
            shortcut.RecordingChanged += active =>
            {
                if (active)
                {
                    foreach (var other in activeRecorders.ToArray()) other.CancelCapture();
                    activeRecorders.Add(shortcut);
                }
                else activeRecorders.Remove(shortcut);
                controller.SetCapturing(activeRecorders.Count != 0);
                SaveButton.IsEnabled = store.LoadError is null && activeRecorders.Count == 0;
            };
            mappings[button] = (kind, shortcut, row);
            row.Children.Add(label); row.Children.Add(position); row.Children.Add(kind); row.Children.Add(shortcut);
            MappingsPanel.Children.Add(row);
        }
        Diagram.ButtonSelected += SelectButton;
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); StartupCheck.IsChecked = key?.GetValue("D100HControl") is string; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { feedback = new("StartupReadError", Text.Error(e)); }
        ProfilesList.SelectedIndex = 0;
        loading = false;
        if (store.LoadProblem is not null) { feedback = store.LoadProblem; SaveButton.IsEnabled = false; }
        else feedback = languageError ?? themeError ?? feedback;
        FeedbackText.Text = feedback.ToString();
        controller.Changed += ControllerChanged;
        Activated += (_, _) => controller.RefreshRouting();
        Closing += HideInsteadOfClose;
        Deactivated += (_, _) => CancelCaptures();
        SourceInitialized += (_, _) => { HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage); Appearance.ApplyWindowTheme(this); UpdateWindowBounds(); };
        RefreshStatus();
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x1A) controller.RefreshRouting();
        if (message is 0x007E or 0x02E0) Dispatcher.BeginInvoke(UpdateWindowBounds);
        if (message == 0x51) foreach (var controls in mappings.Values) controls.Shortcut.RefreshLayout();
        if (message == 0x11) { pausedBeforeShutdown = controller.IsPaused; controller.Pause(true); handled = true; return 1; }
        if (message == 0x16)
        {
            if (wParam == 0) controller.Pause(pausedBeforeShutdown);
            else { controller.Pause(true); AllowClose = true; exit(); }
        }
        return 0;
    }

    private void ControllerChanged() => Dispatcher.BeginInvoke(RefreshStatus);
    private void RefreshStatus()
    {
        ConnectionText.Text = controller.Status;
        PauseButton.Content = Text.Get(controller.IsPaused ? "Resume" : "Pause");
        RoutingBanner.Visibility = controller.Routing ? Visibility.Collapsed : Visibility.Visible;
        ActivityText.Text = Text.Get("Activity", controller.LastEvent, controller.ActiveProfile);
    }

    private void HideInsteadOfClose(object? sender, CancelEventArgs e)
    { CancelCaptures(); if (!AllowClose) { e.Cancel = true; Hide(); } }

    internal void CancelCaptures()
    { foreach (var controls in mappings.Values) controls.Shortcut.CancelCapture(); }

    internal bool CanExit()
    {
        if (AllowClose || !dirty) return true;
        var answer = MessageBox.Show(this, Text.Get("ExitQuestion"), "Orbit D100H",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer == MessageBoxResult.No || (answer == MessageBoxResult.Yes && Save());
    }

    private void SelectProfile(object sender, SelectionChangedEventArgs e)
    {
        CancelCaptures();
        StoreEditor();
        selected = ProfilesList.SelectedItem as Profile;
        if (selected is null) return;
        loading = true;
        ProfileTitle.Text = selected == settings.General ? Text.Get("General") : selected.Name;
        ProfilePath.Text = selected.ProgramPath ?? Text.Get("GeneralDescription");
        VolumeDialButton.IsChecked = selected.DialMode == DialMode.Volume;
        ScrollDialButton.IsChecked = selected.DialMode == DialMode.Scroll;
        ScrollSettings.Visibility = selected.DialMode == DialMode.Scroll ? Visibility.Visible : Visibility.Collapsed;
        ModeCombo.SelectedIndex = (int)selected.Mode;
        SpeedSlider.Value = selected.Speed;
        SpeedLabel.Text = Text.Get("Speed", selected.Speed);
        InvertCheck.IsChecked = selected.Invert;
        RemoveButton.IsEnabled = selected != settings.General;
        foreach (var (button, controls) in mappings)
        {
            var action = selected.Buttons.GetValueOrDefault(button) ?? new();
            controls.Kind.SelectedIndex = (int)action.Kind;
            controls.Shortcut.Keys = action.Keys;
            controls.Shortcut.Visibility = action.Kind == ActionKind.Shortcut ? Visibility.Visible : Visibility.Collapsed;
        }
        loading = false;
    }

    private void StoreEditor()
    {
        if (selected is null) return;
        selected.Mode = (ScrollMode)Math.Max(0, ModeCombo.SelectedIndex);
        selected.DialMode = VolumeDialButton.IsChecked == true ? DialMode.Volume : DialMode.Scroll;
        selected.Speed = (int)SpeedSlider.Value;
        selected.Invert = InvertCheck.IsChecked == true;
        selected.Buttons = mappings.ToDictionary(p => p.Key, p => new ButtonAction {
            Kind = (ActionKind)Math.Max(0, p.Value.Kind.SelectedIndex), Keys = p.Value.Shortcut.Keys });
    }

    private void SetDirty()
    {
        if (loading) return;
        dirty = true;
        if (store.LoadError is null) ShowFeedback(new("Unsaved"));
    }
    private void MarkDirty(object sender, RoutedEventArgs e) => SetDirty();
    private void DialModeChanged(object sender, RoutedEventArgs e)
    {
        if (ScrollSettings is not null) ScrollSettings.Visibility = ScrollDialButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SetDirty();
    }
    private void SpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (SpeedLabel is not null) SpeedLabel.Text = Text.Get("Speed", (int)e.NewValue); SetDirty(); }
    private void TogglePause(object sender, RoutedEventArgs e) => controller.Pause(!controller.IsPaused);

    private void ChangeTheme(object sender, RoutedEventArgs e)
    {
        var error = Appearance.SetDark(DarkThemeCheck.IsChecked == true);
        DarkThemeCheck.Content = Text.Get(DarkThemeCheck.IsChecked == true ? "DarkTheme" : "LightTheme");
        Appearance.ApplyWindowTheme(this);
        ShowFeedback(error ?? new("ThemeApplied"));
    }

    private void ShowFeedback(LocalizedText message)
    { feedback = message; FeedbackText.Text = feedback.ToString(); }

    private async void ChangeLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (loading || LanguageCombo.SelectedValue is not string language || language == Text.Culture.Name) return;
        LanguageCombo.IsEnabled = false;
        try
        {
            await Task.WhenAll(mappings.Values.Select(c => c.Shortcut.CancelCaptureAsync()));
            var error = LanguageUi.Change(language);
            RefreshLanguage();
            if (error is not null && store.LoadError is null) ShowFeedback(error);
        }
        finally { LanguageCombo.IsEnabled = true; }
    }

    private void RefreshLanguage()
    {
        loading = true;
        try
        {
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(Text.Culture.Name);
            DarkThemeCheck.Content = Text.Get(DarkThemeCheck.IsChecked == true ? "DarkTheme" : "LightTheme");
            if (selected is not null)
            {
                ProfileTitle.Text = selected == settings.General ? Text.Get("General") : selected.Name;
                ProfilePath.Text = selected.ProgramPath ?? Text.Get("GeneralDescription");
            }
            SpeedLabel.Text = Text.Get("Speed", (int)SpeedSlider.Value);
            foreach (var (button, controls) in mappings)
            {
                var row = (StackPanel)controls.Row;
                ((TextBlock)row.Children[0]).Text = Text.Get("Button", ButtonLayout.Number(button));
                ((TextBlock)row.Children[1]).Text = DeviceDiagram.PositionName(button);
                var index = controls.Kind.SelectedIndex;
                controls.Kind.ItemsSource = ActionKeys.Select(k => Text.Get(k)).ToArray();
                controls.Kind.SelectedIndex = index;
                System.Windows.Automation.AutomationProperties.SetName(controls.Kind, Text.Get("ButtonAction", ButtonLayout.Number(button)));
                controls.Shortcut.RefreshLanguage();
            }
            Diagram.RefreshLanguage();
            FeedbackText.Text = feedback.ToString();
            RefreshStatus();
        }
        finally { loading = false; }
    }

    private void WindowLoaded(object sender, RoutedEventArgs e) => UpdateWindowBounds();
    private void WindowLocationChanged(object? sender, EventArgs e) => UpdateWindowBounds();
    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (arranging || ButtonLayoutGrid is null || ActualWidth <= 0) return;
        var stack = BodyGrid.ActualWidth < 780;
        if (stackedLayout == stack) { UpdateWindowBounds(); return; }
        stackedLayout = stack;
        arranging = true;
        if (stack)
        {
            BodyGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            BodyGrid.ColumnDefinitions[1].Width = new GridLength(0);
            BodyGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ProfilePanel, 0); Grid.SetColumn(ProfilePanel, 0); Grid.SetColumnSpan(ProfilePanel, 3);
            Grid.SetRow(EditorScroll, 1); Grid.SetColumn(EditorScroll, 0); Grid.SetColumnSpan(EditorScroll, 3);
            EditorScroll.Margin = new Thickness(0, 16, 0, 0);
            ProfilesList.MinHeight = 96; ProfilesList.MaxHeight = 176;
            ButtonLayoutGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ButtonLayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
            ButtonLayoutGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(MappingEditor, 1); Grid.SetColumn(MappingEditor, 0);
            Grid.SetColumnSpan(MappingEditor, 3); Grid.SetRow(Diagram, 0);
            MappingEditor.Margin = new Thickness(0, 16, 0, 0);
        }
        else
        {
            BodyGrid.ColumnDefinitions[0].Width = new GridLength(220);
            BodyGrid.ColumnDefinitions[1].Width = new GridLength(24);
            BodyGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ProfilePanel, 0); Grid.SetColumn(ProfilePanel, 0); Grid.SetColumnSpan(ProfilePanel, 1);
            Grid.SetRow(EditorScroll, 0); Grid.SetColumn(EditorScroll, 2); Grid.SetColumnSpan(EditorScroll, 1);
            EditorScroll.Margin = new Thickness(0);
            ProfilesList.MinHeight = 0; ProfilesList.MaxHeight = double.PositiveInfinity;
            ButtonLayoutGrid.ColumnDefinitions[0].Width = new GridLength(224);
            ButtonLayoutGrid.ColumnDefinitions[1].Width = new GridLength(24);
            ButtonLayoutGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(MappingEditor, 0); Grid.SetColumn(MappingEditor, 2);
            Grid.SetColumnSpan(MappingEditor, 1); Grid.SetRow(Diagram, 0);
            MappingEditor.Margin = new Thickness(0);
        }
        arranging = false;
        UpdateWindowBounds();
    }

    private void UpdateWindowBounds()
    {
        if (fitting) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0) return;
        var monitor = MonitorFromWindow(hwnd, 2);
        var info = new MonitorInformation { Size = Marshal.SizeOf<MonitorInformation>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return;
        var dpi = hwnd == 0 ? 96u : GetDpiForWindow(hwnd);
        if (dpi == 0) dpi = 96;
        var scale = 96d / dpi;
        fitting = true;
        try
        {
            var maxWidth = Math.Max(1, (info.Work.Right - info.Work.Left) * scale - 32);
            var maxHeight = Math.Max(1, (info.Work.Bottom - info.Work.Top) * scale - 32);
            if (Math.Abs(MaxWidth - maxWidth) > 0.5) MaxWidth = maxWidth;
            if (Math.Abs(MaxHeight - maxHeight) > 0.5) MaxHeight = maxHeight;
            if (!IsLoaded) return;
            var left = Math.Clamp(Left, info.Work.Left * scale + 16,
                Math.Max(info.Work.Left * scale + 16, info.Work.Right * scale - ActualWidth - 16));
            var top = Math.Clamp(Top, info.Work.Top * scale + 16,
                Math.Max(info.Work.Top * scale + 16, info.Work.Bottom * scale - ActualHeight - 16));
            if (Math.Abs(Left - left) > 0.5) Left = left;
            if (Math.Abs(Top - top) > 0.5) Top = top;
        }
        finally { fitting = false; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInformation
    {
        public int Size;
        public NativeRectangle Monitor;
        public NativeRectangle Work;
        public int Flags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInformation info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);

    private void SelectButton(int button)
    {
        CancelCaptures();
        selectedButton = button;
        Diagram.Select(button);
        foreach (var (number, controls) in mappings)
            controls.Row.Visibility = number == button ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddProgram(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = Text.Get("ChooseProgram"), Filter = Text.Get("ProgramFilter") + "|*.exe", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        var existing = profiles.FirstOrDefault(p => string.Equals(p.ProgramPath, picker.FileName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { ProfilesList.SelectedItem = existing; return; }
        var profile = new Profile { Name = Path.GetFileNameWithoutExtension(picker.FileName), ProgramPath = picker.FileName };
        settings.Programs.Add(profile); profiles.Add(profile); ProfilesList.SelectedItem = profile; SetDirty();
    }

    private void RemoveProgram(object sender, RoutedEventArgs e)
    {
        if (selected is null || selected == settings.General) return;
        var removed = selected;
        ProfilesList.SelectedIndex = 0;
        settings.Programs.Remove(removed); profiles.Remove(removed); SetDirty();
    }

    private void SaveClick(object sender, RoutedEventArgs e) => Save();
    private bool Save()
    {
        try
        {
            StoreEditor(); settings.HapticFeedbackEnabled = HapticFeedbackCheck.IsChecked == true;
            store.Save(settings); controller.Update(settings); dirty = false;
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (StartupCheck.IsChecked == true)
            {
                var executable = Path.Combine(AppContext.BaseDirectory, "Orbit D100H.exe");
                if (!File.Exists(executable)) throw Text.Describe(new IOException(Text.Get("MissingExe")), "MissingExe");
                key.SetValue("D100HControl", $"\"{executable}\" --background", RegistryValueKind.String);
            }
            else key.DeleteValue("D100HControl", throwOnMissingValue: false);
            ShowFeedback(new("Saved"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Security.SecurityException)
        { ShowFeedback(new("SaveError", Text.Error(ex))); return false; }
    }

    private void OpenMouseSettings(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:mousetouchpad") { UseShellExecute = true }); }
        catch (Win32Exception ex) { ShowFeedback(new("MouseSettingsError", Text.Error(ex))); }
    }
}
