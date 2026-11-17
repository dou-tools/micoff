using System.Diagnostics;

namespace MicMute;

sealed class TrayApp : ApplicationContext
{
    readonly Config config = Config.Load();
    readonly SynchronizationContext ui = new WindowsFormsSynchronizationContext();
    readonly AudioEndpoint endpoint = new();
    readonly HotkeyManager hotkeys = new();
    readonly Overlay overlay = new();
    readonly ContextMenuStrip menu = new();
    readonly NotifyIcon tray;
    readonly Beeps beeps;
    readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 5000 };
    List<string> failedHotkeys = [];
    bool? lastMuted;

    public TrayApp()
    {
        beeps = new Beeps(config.BeepVolume);
        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        menu.Opening += (_, _) => BuildMenu();
        hotkeys.Pressed += Toggle;
        endpoint.MuteChanged += (muted, own) => ui.Post(_ => { if (!own) UpdateState(muted, flash: true); }, null);
        refreshTimer.Tick += (_, _) => Refresh();

        // si el .exe cambio de lugar, se actualiza la ruta de inicio
        if (Config.StartWithWindows) Config.StartWithWindows = true;

        ApplyHotkeys();
        Refresh();
        refreshTimer.Start();
    }

    void Toggle()
    {
        var current = ReadMute();
        if (current == null)
        {
            UpdateState(null, flash: false);
            tray.ShowBalloonTip(2000, "micmute", "No se encontró el micrófono.", ToolTipIcon.Warning);
            return;
        }

        var muted = !current.Value;
        if (!endpoint.SetMute(muted)) return;
        beeps.Play(muted);
        UpdateState(muted, flash: true);
    }

    // si el dispositivo se desconecto, la primera lectura falla y se reintenta con el nuevo
    bool? ReadMute()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var muted = endpoint.Resolve(config.DeviceName) ? endpoint.GetMute() : null;
            if (muted != null) return muted;
        }
        return null;
    }

    void Refresh(bool force = false)
    {
        var muted = ReadMute();
        if (force || muted != lastMuted || tray.Icon == null) UpdateState(muted, flash: false);
    }

    void UpdateState(bool? muted, bool flash)
    {
        lastMuted = muted;
        var previous = tray.Icon;
        tray.Icon = TrayIcons.Create(muted);
        previous?.Dispose();

        var state = muted switch { true => "muteado", false => "activo", null => "no encontrado" };
        var text = $"{endpoint.DeviceFriendlyName ?? "Micrófono"}: {state}";
        tray.Text = text.Length > 127 ? text[..127] : text;

        if (!config.Overlay || muted == null) overlay.Hide();
        else if (muted == true) overlay.ShowState(true, persistent: true);
        else if (flash) overlay.ShowState(false, persistent: false);
        else overlay.Hide();
    }

    void ApplyHotkeys()
    {
        var parsed = config.Hotkeys.Select(text => Hotkey.TryParse(text, out var h) ? h : (Hotkey?)null).OfType<Hotkey>();
        failedHotkeys = hotkeys.Apply(parsed);
        if (failedHotkeys.Count > 0)
            tray.ShowBalloonTip(3000, "micmute", $"No se pudo registrar: {string.Join(", ", failedHotkeys)} (lo usa otra app).", ToolTipIcon.Warning);
    }

    void BuildMenu()
    {
        menu.Items.Clear();
        menu.Items.Add(new ToolStripMenuItem(tray.Text) { Enabled = false });
        menu.Items.Add(new ToolStripMenuItem(lastMuted == true ? "Activar micrófono" : "Mutear micrófono", null, (_, _) => Toggle()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(new ToolStripSeparator());

        var shortcuts = new ToolStripMenuItem("Atajos");
        foreach (var text in config.Hotkeys.ToList())
        {
            var taken = failedHotkeys.Contains(text, StringComparer.OrdinalIgnoreCase) ? "   ⚠ ocupado por otra app" : "";
            shortcuts.DropDownItems.Add(new ToolStripMenuItem($"{text}{taken}   (quitar)", null, (_, _) => RemoveHotkey(text)));
        }
        if (config.Hotkeys.Count > 0) shortcuts.DropDownItems.Add(new ToolStripSeparator());
        // el dialogo se abre recien cuando el menu termino de cerrarse
        shortcuts.DropDownItems.Add("Agregar atajo…", null, (_, _) => ui.Post(_ => AddHotkey(), null));
        menu.Items.Add(shortcuts);

        menu.Items.Add("Probar beeps", null, async (_, _) =>
        {
            beeps.Play(true);
            await Task.Delay(500);
            beeps.Play(false);
        });
        menu.Items.Add(new ToolStripMenuItem("Indicador en pantalla", null, (_, _) => ToggleOverlay()) { Checked = config.Overlay });
        menu.Items.Add(new ToolStripMenuItem("Iniciar con Windows", null, (_, _) => Config.StartWithWindows = !Config.StartWithWindows) { Checked = Config.StartWithWindows });
        menu.Items.Add("Abrir carpeta de configuración", null, (_, _) => Process.Start("explorer.exe", Config.Folder));
        menu.Items.Add("Recargar configuración", null, (_, _) => ReloadConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Exit());
    }

    void AddHotkey()
    {
        hotkeys.Suspended = true;
        using var dialog = new HotkeyDialog();
        var result = dialog.ShowDialog() == DialogResult.OK ? dialog.Result : null;
        hotkeys.Suspended = false;
        if (result == null) return;

        var text = result.Value.ToString();
        if (config.Hotkeys.Contains(text, StringComparer.OrdinalIgnoreCase)) return;
        config.Hotkeys.Add(text);
        config.Save();
        ApplyHotkeys();
    }

    void RemoveHotkey(string text)
    {
        config.Hotkeys.Remove(text);
        config.Save();
        ApplyHotkeys();
    }

    void ToggleOverlay()
    {
        config.Overlay = !config.Overlay;
        config.Save();
        UpdateState(lastMuted, flash: false);
    }

    void ReloadConfig()
    {
        var fresh = Config.Load();
        config.Hotkeys = fresh.Hotkeys;
        config.DeviceName = fresh.DeviceName;
        config.BeepVolume = fresh.BeepVolume;
        config.Overlay = fresh.Overlay;
        beeps.Reload(config.BeepVolume);
        ApplyHotkeys();
        Refresh(force: true);
    }

    void Exit()
    {
        refreshTimer.Stop();
        tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            hotkeys.Dispose();
            endpoint.Dispose();
            beeps.Dispose();
            overlay.Dispose();
            tray.Icon?.Dispose();
            tray.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
