using System.Runtime.InteropServices;

namespace AuraLite;

internal sealed class NativeTray : IDisposable
{
    private const uint Callback = 0x8001; // WM_APP + 1
    private const uint HideAfterMinimize = 0x8002;
    private const uint IconId = 1;
    private const uint SubclassId = 1;
    private readonly nint hwnd;
    private readonly SubclassProc callback;
    private readonly nint icon;
    private readonly Action restore;
    private readonly Action quit;
    private readonly Action resumed;
    private readonly Func<string> openLabel;
    private readonly Func<string> exitLabel;
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private bool attached;

    public bool IsAvailable { get; private set; }

    public NativeTray(nint hwnd, string iconPath, Action restore, Action quit, Action resumed, Func<string> openLabel, Func<string> exitLabel)
    {
        this.hwnd = hwnd;
        this.restore = restore;
        this.quit = quit;
        this.resumed = resumed;
        this.openLabel = openLabel;
        this.exitLabel = exitLabel;
        icon = LoadImage(0, iconPath, 1, 0, 0, 0x50); // IMAGE_ICON, LR_LOADFROMFILE | LR_DEFAULTSIZE
        callback = HandleMessage;
        attached = SetWindowSubclass(hwnd, callback, SubclassId, 0);
        if (attached && icon != 0) Add();
    }

    public void Hide()
    {
        if (IsAvailable) ShowWindow(hwnd, 0); // SW_HIDE
    }

    public void Restore()
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, 9); // SW_RESTORE
        else if (!IsWindowVisible(hwnd)) ShowWindow(hwnd, 5); // SW_SHOW
        SetForegroundWindow(hwnd);
    }

    private NotifyData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyData>(),
        Window = hwnd,
        Id = IconId,
        Flags = 0x87, // NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP
        Callback = Callback,
        Icon = icon,
        Tip = "AuraLite",
        Info = "",
        InfoTitle = ""
    };

    private void Add()
    {
        NotifyData data = Data();
        IsAvailable = Shell_NotifyIcon(0, ref data); // NIM_ADD
        if (!IsAvailable) return;
        data.Version = 4;
        Shell_NotifyIcon(4, ref data); // NIM_SETVERSION
    }

    private nint HandleMessage(nint window, uint msg, nuint wparam, nint lparam, nuint id, nuint data)
    {
        if (taskbarCreated != 0 && msg == taskbarCreated)
        {
            bool wasAvailable = IsAvailable;
            if (icon != 0) Add();
            if (wasAvailable && !IsAvailable) restore();
            return 0;
        }

        if (msg == 0x0218 && wparam == 0x12) // WM_POWERBROADCAST, resume
            resumed();

        if (msg == 0x0005 && wparam == 1 && IsAvailable) // WM_SIZE, SIZE_MINIMIZED
            PostMessage(hwnd, HideAfterMinimize, 0, 0);
        if (msg == HideAfterMinimize)
        {
            if (IsIconic(hwnd)) Hide();
            return 0;
        }
        if (msg != Callback) return DefSubclassProc(window, msg, wparam, lparam);

        switch ((ushort)lparam)
        {
            case 0x0202:  // WM_LBUTTONUP, fallback for older tray versions
            case 0x0400:  // NIN_SELECT
            case 0x0401:  // NIN_KEYSELECT
                restore();
                break;
            case 0x007b:  // WM_CONTEXTMENU
            case 0x0205:  // WM_RBUTTONUP, fallback for older tray versions
                ShowMenu();
                break;
        }
        return 0;
    }

    private void ShowMenu()
    {
        nint menu = CreatePopupMenu();
        if (menu == 0) return;
        uint action;
        try
        {
            if (!AppendMenu(menu, 0, 1, openLabel()) || !AppendMenu(menu, 0, 2, exitLabel()))
                return;
            GetCursorPos(out Point point);
            SetForegroundWindow(hwnd);
            action = TrackPopupMenu(menu, 0x102, point.X, point.Y, 0, hwnd, 0);
            PostMessage(hwnd, 0, 0, 0);
        }
        finally { DestroyMenu(menu); }

        if (action == 1) restore();
        if (action == 2) quit();
    }

    public void Dispose()
    {
        if (IsAvailable)
        {
            NotifyData data = Data();
            Shell_NotifyIcon(2, ref data); // NIM_DELETE
        }
        if (attached) RemoveWindowSubclass(hwnd, callback, SubclassId);
        if (icon != 0) DestroyIcon(icon);
        attached = false;
        IsAvailable = false;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, Callback;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    private delegate nint SubclassProc(nint window, uint msg, nuint wparam, nint lparam, nuint id, nuint data);

    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window, uint msg, nuint wparam, nint lparam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint action, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint instance, string path, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint item, string label);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint window, nint rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint msg, nuint wparam, nint lparam);
}
