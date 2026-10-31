using System.Windows;
using OrbitD100H.Core;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace OrbitD100H.App;

public partial class App : Application
{
    private Mutex? mutex;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private Stream? trayIconStream;
    private Controller? controller;
    private MainWindow? window;
    private bool exiting;
    private bool resumeAfterUnlock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var languageError = LanguageUi.Initialize();
        // Share the legacy mutex so the old and renamed apps cannot control the same device together.
        mutex = new Mutex(true, @"Local\D100HControl", out var created);
        if (!created)
        {
            MessageBox.Show(Text.Get("AlreadyRunning"), "Orbit D100H");
            Shutdown(); return;
        }
        var store = new SettingsStore();
        var settings = store.Load();
        controller = new Controller(settings);
        controller.Start();
        window = new MainWindow(controller, store, settings, ExitApplication, languageError);
        MainWindow = window;
        var menu = CreateTrayMenu(ShowWindow, () => controller.Pause(!controller.IsPaused), ExitApplication);
        trayIconStream = GetResourceStream(new Uri("Assets/OrbitD100H.ico", UriKind.Relative)).Stream;
        trayIcon = new System.Drawing.Icon(trayIconStream);
        tray = new Forms.NotifyIcon { Text = "Orbit D100H", Icon = trayIcon,
            ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        void RefreshTray()
        {
            if (exiting) return;
            RefreshTrayMenu(menu, controller.IsPaused);
            tray.Text = ("Orbit D100H · " + controller.Status)[..Math.Min(63, ("Orbit D100H · " + controller.Status).Length)];
        }
        controller.Changed += () => Dispatcher.BeginInvoke(RefreshTray);
        LanguageUi.Changed += RefreshTray;
        RefreshTray();
        SystemEvents.SessionSwitch += SessionSwitch;
        if (!e.Args.Contains("--background") || store.LoadError is not null) ShowWindow();
    }

    internal static Forms.ContextMenuStrip CreateTrayMenu(Action open, Action pause, Action close)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Text.Get("OpenSettings"), null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripMenuItem(Text.Get("Pause"), null, (_, _) => pause()));
        menu.Items.Add(Text.Get("Exit"), null, (_, _) => close());
        return menu;
    }

    internal static void RefreshTrayMenu(Forms.ContextMenuStrip menu, bool paused)
    {
        menu.Items[0].Text = Text.Get("OpenSettings");
        var pause = (Forms.ToolStripMenuItem)menu.Items[1];
        pause.Checked = paused;
        pause.Text = Text.Get(paused ? "Resume" : "Pause");
        menu.Items[2].Text = Text.Get("Exit");
    }

    private void SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (controller is null) return;
        if (e.Reason == SessionSwitchReason.SessionLock)
        { Dispatcher.BeginInvoke(() => window?.CancelCaptures()); resumeAfterUnlock = !controller.IsPaused; controller.Pause(true); }
        if (e.Reason == SessionSwitchReason.SessionUnlock && resumeAfterUnlock)
        { resumeAfterUnlock = false; controller.Pause(false); }
    }

    private void ShowWindow()
    {
        window?.Show();
        if (window is not null) { window.WindowState = WindowState.Normal; window.Activate(); }
    }

    private async void ExitApplication()
    {
        window?.CancelCaptures();
        if (exiting || window?.CanExit() == false) return;
        exiting = true;
        SystemEvents.SessionSwitch -= SessionSwitch;
        try { if (controller is not null) await controller.DisposeAsync(); }
        finally { if (window is not null) window.AllowClose = true; tray?.Dispose(); trayIcon?.Dispose(); trayIconStream?.Dispose(); mutex?.Dispose(); Shutdown(); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose(); trayIcon?.Dispose(); trayIconStream?.Dispose(); mutex?.Dispose(); base.OnExit(e);
    }
}
