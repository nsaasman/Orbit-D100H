using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Markup;
using System.Xml.Linq;
using Microsoft.Win32;
using OrbitD100H.App;
using OrbitD100H.Core;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        // Load the real styles without starting HID or claiming the app mutex.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppResources.xaml"));
        var resources = new XElement(wpf + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            source.Root!.Element(wpf + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        // Restore real preferences even if a UI assertion fails.
        using var preferences = Registry.CurrentUser.CreateSubKey(@"Software\D100HControl", true);
        var oldLanguage = preferences.GetValue("Language");
        var oldTheme = preferences.GetValue("DarkMode");
        var directory = Path.Combine(Path.GetTempPath(), "OrbitD100H-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        MainWindow? window = null;
        Controller? controller = null;
        try
        {
            if (args.Length > 0) Directory.CreateDirectory(args[0]);
            LanguageUi.Initialize();
            var store = new SettingsStore(directory);
            var settings = new Settings();
            settings.General.Name = "Algemeen";
            settings.General.Buttons[1] = new() { Kind = ActionKind.Shortcut, Keys = [0xA2, 0x53] };
            settings.Programs.Add(new() { Name = "Untranslated profile", ProgramPath = @"C:\Apps\example.exe" });
            store.Save(settings);
            var savedBytes = File.ReadAllBytes(store.FilePath);
            controller = new(settings);
            controller.Connection(true, new("HapticUnconfirmed"));
            controller.Pause(true);
            window = new(controller, store, settings, () => { });
            window.Show();
            Pump();
            var profiles = Find<ListBox>(window, "ProfilesList");
            profiles.SelectedIndex = 1;
            var slider = Find<Slider>(window, "SpeedSlider");
            slider.Value = 7;
            Find<RadioButton>(window, "VolumeDialButton").IsChecked = true;
            Find<CheckBox>(window, "HapticFeedbackCheck").IsChecked = false;
            var mappings = Find<StackPanel>(window, "MappingsPanel");
            var row = (StackPanel)mappings.Children[0];
            var kind = (ComboBox)row.Children[2];
            kind.SelectedIndex = (int)ActionKind.Shortcut;
            var shortcut = (ShortcutEditor)row.Children[3];
            shortcut.Keys = [0xA3, 0x46];
            var diagram = Find<DeviceDiagram>(window, "Diagram");
            ((Button)diagram.FindName("Position3")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var languages = Find<ComboBox>(window, "LanguageCombo");
            languages.SelectedValue = Text.Culture.Name == "es" ? "en" : "es";
            WaitFor(() => languages.IsEnabled);
            using var trayMenu = App.CreateTrayMenu(() => { }, () => { }, () => { });

            foreach (var language in new[] { "nl", "en", "de", "fr", "es" })
            {
                Console.WriteLine("UI: testing " + language);
                slider.Value = 6; slider.Value = 7;
                languages.SelectedValue = language;
                WaitFor(() => languages.IsEnabled);
                Assert(Text.Culture.Name == language, "UI language selected: " + language);
                Assert(profiles.SelectedIndex == 1 && slider.Value == 7 && kind.SelectedIndex == (int)ActionKind.Shortcut,
                    "Unsaved selection and edits preserved: " + language);
                Assert(shortcut.Keys.SequenceEqual([0xA3, 0x46]) && row.Visibility == Visibility.Visible,
                    "Shortcut and selected button preserved: " + language);
                Assert(Find<RadioButton>(window, "VolumeDialButton").IsChecked == true && Find<CheckBox>(window, "HapticFeedbackCheck").IsChecked == false,
                    "Unsaved mode and haptic preference preserved: " + language);
                Assert(Find<TextBlock>(window, "ProfileTitle").Text == "Untranslated profile", "Custom profile name unchanged");
                Assert(controller.IsPaused && Find<TextBlock>(window, "ConnectionText").Text.Contains(Text.Get("HapticUnconfirmed")), "Paused status retranslates");
                Assert(Find<TextBlock>(window, "FeedbackText").Text == Text.Get("Unsaved"), "Unsaved feedback retranslates");
                Assert(AutomationProperties.GetName(shortcut.Children.OfType<ComboBox>().Single()) == Text.Get("ButtonMainKey", 3), "Shortcut accessibility name retranslates");
                Assert(((Button)diagram.FindName("Position3")).ToolTip?.ToString() == Text.Get("ButtonPosition", 3, Text.Get("TopLeft")), "Diagram tooltip retranslates");
                Assert(File.ReadAllBytes(store.FilePath).SequenceEqual(savedBytes), "Language switch never saves profiles");
                Assert(preferences.GetValue("Language") as string == language, "Language preference persisted");
                Text.SetLanguage("en");
                LanguageUi.Initialize();
                Assert(Text.Culture.Name == language, "Saved language restored at startup");
                App.RefreshTrayMenu(trayMenu, true);
                Assert(trayMenu.Items[0].Text == Text.Get("OpenSettings") && trayMenu.Items[2].Text == Text.Get("Exit"), "Native tray menu retranslates");
                var trayPause = (System.Windows.Forms.ToolStripMenuItem)trayMenu.Items[1];
                Assert(trayPause.Checked && trayPause.Text == Text.Get("Resume"), "Native tray menu shows paused state");
                if (args.Length > 0)
                {
                    trayMenu.Show(new System.Drawing.Point(10, 10));
                    System.Windows.Forms.Application.DoEvents();
                    using var bitmap = new System.Drawing.Bitmap(trayMenu.Width, trayMenu.Height);
                    trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(args[0], language + "-tray-menu.png"));
                    trayMenu.Close(); Pump();
                }
                App.RefreshTrayMenu(trayMenu, false);
                Assert(!trayPause.Checked && trayPause.Text == Text.Get("Pause"), "Native tray menu shows running state");

                foreach (var dark in new[] { false, true })
                {
                    var theme = Find<ToggleButton>(window, "DarkThemeCheck");
                    theme.IsChecked = dark;
                    theme.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    foreach (var width in new[] { 1200d, 560d })
                    {
                        window.SizeToContent = SizeToContent.Manual;
                        window.Width = width; window.Height = 900;
                        Pump();
                        Assert(window.ActualWidth <= width + 1, "Window fits requested width");
                        Assert(languages.ActualWidth >= 140 && Find<Button>(window, "PauseButton").IsVisible, "Header remains usable");
                        if (args.Length > 0)
                        {
                            Find<RadioButton>(window, "ScrollDialButton").IsChecked = true;
                            Pump();
                            var scroll = Find<ScrollViewer>(window, "WindowScroll");
                            scroll.ScrollToTop(); Pump();
                            Screenshot(window, Path.Combine(args[0], $"{language}-{(dark ? "dark" : "light")}-{width}-top.png"));
                            if (width < 780)
                            {
                                scroll.ScrollToVerticalOffset(550); Pump();
                                Screenshot(window, Path.Combine(args[0], $"{language}-{(dark ? "dark" : "light")}-{width}-middle.png"));
                            }
                            scroll.ScrollToEnd(); Pump();
                            Screenshot(window, Path.Combine(args[0], $"{language}-{(dark ? "dark" : "light")}-{width}-bottom.png"));
                            Find<RadioButton>(window, "VolumeDialButton").IsChecked = true;
                        }
                    }
                }
            }

            // Exercise the actual recording cancellation path without injecting any keys.
            foreach (var paused in new[] { false, true })
            {
                controller.Pause(paused);
                var record = shortcut.Children.OfType<Button>().Single();
                record.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert(record.Content?.ToString() == Text.Get("Cancel"), "Recording started");
                languages.SelectedValue = Text.Culture.Name == "en" ? "nl" : "en";
                WaitFor(() => languages.IsEnabled);
                Assert(record.Content?.ToString() == Text.Get("RecordShortcut") && shortcut.Keys.SequenceEqual([0xA3, 0x46]), "Language change cancels recording and preserves binding");
                Assert(controller.IsPaused == paused && Find<Button>(window, "SaveButton").IsEnabled, "Recording restores pause state and save availability");
            }
            controller.Connection(false, new("ConnectionInterrupted", new LocalizedText("NoKeepalive")));
            languages.SelectedValue = "fr";
            WaitFor(() => languages.IsEnabled);
            Assert(Find<TextBlock>(window, "ConnectionText").Text.Contains(Text.Get("NoKeepalive")), "Disconnected status retranslates without reconnecting");
            profiles.SelectedIndex = 0;
            Assert(Find<TextBlock>(window, "ProfileTitle").Text == Text.Get("General") && settings.General.Name == "Algemeen", "General title translates without renaming data");
            Close(window); window = null;

            File.WriteAllText(store.FilePath, "null");
            var brokenStore = new SettingsStore(directory);
            var fallback = brokenStore.Load();
            window = new(controller, brokenStore, fallback, () => { });
            window.Show(); Pump();
            languages = Find<ComboBox>(window, "LanguageCombo");
            languages.SelectedValue = "de";
            WaitFor(() => languages.IsEnabled);
            Assert(!Find<Button>(window, "SaveButton").IsEnabled && Find<TextBlock>(window, "FeedbackText").Text.Contains(Text.Get("EmptySettings")), "Corrupt settings remain protected and error retranslates");
            Assert(File.ReadAllText(store.FilePath) == "null", "Corrupt file remains untouched");
            Console.WriteLine($"Geslaagd: {checks} UI-controles voor taalwissel, opslag, opname, thema en vensterbreedte.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            if (window is not null) Close(window);
            controller?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (oldLanguage is null) preferences.DeleteValue("Language", false); else preferences.SetValue("Language", oldLanguage);
            if (oldTheme is null) preferences.DeleteValue("DarkMode", false); else preferences.SetValue("DarkMode", oldTheme);
            File.Delete(Path.Combine(directory, "settings.json"));
            Directory.Delete(directory);
            app.Shutdown();
        }
    }

    private static T Find<T>(MainWindow window, string name) where T : FrameworkElement => (T)window.FindName(name);
    private static void Close(MainWindow window) { window.AllowClose = true; window.Close(); Pump(); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void WaitFor(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready() && DateTime.UtcNow < deadline) Pump();
        Pump();
        Assert(ready(), "Asynchronous language change finished");
    }
    private static void Screenshot(Window window, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
