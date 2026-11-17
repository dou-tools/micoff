using System.Runtime.InteropServices;

namespace MicOff;

sealed class HotkeyManager : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int WM_QUIT = 0x0012;
    const int WM_XBUTTONDOWN = 0x020B;
    const int WM_XBUTTONUP = 0x020C;
    const int WH_MOUSE_LL = 14;
    const uint MOD_NOREPEAT = 0x4000;

    readonly List<int> registeredIds = [];
    readonly LowLevelMouseProc mouseProc;
    readonly IntPtr target;
    Hotkey[] mouseHotkeys = [];
    Thread? hookThread;
    uint hookThreadId;
    volatile bool suspended;
    bool swallowedXButton1;
    bool swallowedXButton2;

    public event Action? Pressed;

    public HotkeyManager()
    {
        mouseProc = MouseHookCallback;
        CreateHandle(new CreateParams());
        target = Handle;
    }

    // mientras se captura un atajo nuevo no se dispara el toggle
    public bool Suspended
    {
        get => suspended;
        set => suspended = value;
    }

    public List<string> Apply(IEnumerable<Hotkey> hotkeys)
    {
        Clear();
        var failed = new List<string>();
        var mouse = new List<Hotkey>();
        var id = 1;
        foreach (var hotkey in hotkeys)
        {
            if (hotkey.IsMouse)
            {
                mouse.Add(hotkey);
                continue;
            }
            if (RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | MOD_NOREPEAT, (uint)hotkey.Key))
                registeredIds.Add(id);
            else
                failed.Add(hotkey.ToString());
            id++;
        }
        mouseHotkeys = [.. mouse];
        if (mouseHotkeys.Length > 0) StartMouseHook();
        return failed;
    }

    void Clear()
    {
        foreach (var id in registeredIds) UnregisterHotKey(Handle, id);
        registeredIds.Clear();
        StopMouseHook();
        mouseHotkeys = [];
    }

    // el hook global de mouse vive en su propio hilo para que la ui nunca frene los clicks del sistema
    void StartMouseHook()
    {
        using var ready = new ManualResetEventSlim();
        hookThread = new Thread(() =>
        {
            hookThreadId = GetCurrentThreadId();
            var hook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, GetModuleHandle(null), 0);
            ready.Set();
            if (hook == IntPtr.Zero) return;
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            UnhookWindowsHookEx(hook);
        })
        { IsBackground = true, Name = "MouseHook" };
        hookThread.Start();
        ready.Wait();
    }

    void StopMouseHook()
    {
        if (hookThread == null) return;
        PostThreadMessage(hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        hookThread.Join(1000);
        hookThread = null;
        swallowedXButton1 = swallowedXButton2 = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && !suspended) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && (wParam == WM_XBUTTONDOWN || wParam == WM_XBUTTONUP) && ShouldSwallow(wParam == WM_XBUTTONDOWN, lParam))
                return 1;
        }
        catch { }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    bool ShouldSwallow(bool isDown, IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<MouseHookData>(lParam);
        var isFirst = (data.MouseData >> 16) == 1;

        // el up solo se traga si se trago el down, asi ninguna app queda con el boton apretado
        if (!isDown)
        {
            var swallowed = isFirst ? swallowedXButton1 : swallowedXButton2;
            if (isFirst) swallowedXButton1 = false; else swallowedXButton2 = false;
            return swallowed;
        }

        if (suspended) return false;
        var button = isFirst ? Keys.XButton1 : Keys.XButton2;
        var modifiers = CurrentModifiers();
        if (!mouseHotkeys.Any(h => h.Key == button && h.Modifiers == modifiers)) return false;

        if (isFirst) swallowedXButton1 = true; else swallowedXButton2 = true;
        PostMessage(target, WM_HOTKEY, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    public static KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsDown(Keys.ControlKey)) modifiers |= KeyModifiers.Ctrl;
        if (IsDown(Keys.Menu)) modifiers |= KeyModifiers.Alt;
        if (IsDown(Keys.ShiftKey)) modifiers |= KeyModifiers.Shift;
        if (IsDown(Keys.LWin) || IsDown(Keys.RWin)) modifiers |= KeyModifiers.Win;
        return modifiers;
    }

    static bool IsDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    public void Dispose()
    {
        Clear();
        DestroyHandle();
    }

    delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct MouseHookData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int GetMessage(out NativeMessage msg, IntPtr hWnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref NativeMessage msg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage(ref NativeMessage msg);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vk);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? name);
}
