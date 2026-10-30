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
        // Share the legacy mutex so the old and renamed apps cannot control the same device together.
        mutex = new Mutex(true, @"Local\D100HControl", out var created);
        if (!created)
        {
            MessageBox.Show("Orbit D100H is al actief. Open het venster via het pictogram in het systeemvak.", "Orbit D100H");
            Shutdown(); return;
        }
        var store = new SettingsStore();
        var settings = store.Load();
        controller = new Controller(settings);
        controller.Start();
        window = new MainWindow(controller, store, settings, ExitApplication);
        MainWindow = window;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Instellingen openen", null, (_, _) => ShowWindow());
        var pause = new Forms.ToolStripMenuItem("Pauzeren");
        pause.Click += (_, _) => controller.Pause(!controller.IsPaused);
        menu.Items.Add(pause);
        menu.Items.Add("Afsluiten", null, (_, _) => ExitApplication());
        trayIconStream = GetResourceStream(new Uri("Assets/OrbitD100H.ico", UriKind.Relative)).Stream;
        trayIcon = new System.Drawing.Icon(trayIconStream);
        tray = new Forms.NotifyIcon { Text = "Orbit D100H", Icon = trayIcon,
            ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        controller.Changed += () => Dispatcher.BeginInvoke(() =>
        {
            if (exiting) return;
            pause.Checked = controller.IsPaused;
            pause.Text = controller.IsPaused ? "Hervatten" : "Pauzeren";
            tray.Text = ("Orbit D100H · " + controller.Status)[..Math.Min(63, ("Orbit D100H · " + controller.Status).Length)];
        });
        SystemEvents.SessionSwitch += SessionSwitch;
        if (!e.Args.Contains("--background") || store.LoadError is not null) ShowWindow();
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
