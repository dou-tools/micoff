namespace MicMute;

static class Program
{
    [STAThread]
    static void Main()
    {
#if AUTOSTART
        // la variante autostart ademas queda registrada para iniciar con windows
        Config.StartWithWindows = true;
#endif
        using var mutex = new Mutex(true, @"Local\micmute", out var isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
