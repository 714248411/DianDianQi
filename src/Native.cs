using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BulingOffline
{
    public static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPoint(IntPtr parent, POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        // 拾取指定坐标处最顶层的、不属于本进程的可见窗口（准星拾取用）
        public static IntPtr TopmostWindowAtPoint(POINT pt, int selfPid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if ((int)pid == selfPid) return true;
                RECT r;
                if (!GetWindowRect(h, out r)) return true;
                if (r.Right - r.Left < 40 || r.Bottom - r.Top < 20) return true;
                if (pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom)
                {
                    found = h;
                    if (GetWindowText(h).Length > 0) return false;  // 有标题的按Z序第一个命中即最顶层
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        [DllImport("kernel32.dll")] public static extern bool Beep(uint dwFreq, uint dwDuration);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        public const uint MOUSEEVENTF_LEFTDOWN = 0x02, MOUSEEVENTF_LEFTUP = 0x04;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x08, MOUSEEVENTF_RIGHTUP = 0x10;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x20, MOUSEEVENTF_MIDDLEUP = 0x40;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const int WM_CLOSE = 0x0010;
        public const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public UIntPtr dwExtraInfo; }

        public static string GetWindowText(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, 512);
            return sb.ToString();
        }
        public static string GetClassName(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            return sb.ToString();
        }
        public static int GetWindowPid(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return (int)pid;
        }
        public static IntPtr RootOwner(IntPtr h) { return GetAncestor(h, GA_ROOT); }

        // ---------- 键名映射 ----------
        public static readonly Dictionary<string, ushort> KeyNames = BuildKeyNames();
        static Dictionary<string, ushort> BuildKeyNames()
        {
            Dictionary<string, ushort> m = new Dictionary<string, ushort>();
            for (int i = 0; i < 26; i++) m[((char)('A' + i)).ToString()] = (ushort)(0x41 + i);
            for (int i = 0; i < 10; i++) m[((char)('0' + i)).ToString()] = (ushort)(0x30 + i);
            for (int i = 1; i <= 12; i++) m["F" + i] = (ushort)(0x70 + i - 1);
            m["ESC"] = 0x1B; m["ESCAPE"] = 0x1B;
            m["TAB"] = 0x09; m["CAPS"] = 0x14; m["CAPSLOCK"] = 0x14;
            m["SHIFT"] = 0x10; m["CTRL"] = 0x11; m["CONTROL"] = 0x11; m["ALT"] = 0x12; m["WIN"] = 0x5B;
            m["BACK"] = 0x08; m["BACKSPACE"] = 0x08;
            m["SPACE"] = 0x20; m["空格"] = 0x20;
            m["回车"] = 0x0D; m["ENTER"] = 0x0D; m["RETURN"] = 0x0D;
            m["上"] = 0x26; m["下"] = 0x28; m["左"] = 0x25; m["右"] = 0x27;
            m["上键"] = 0x26; m["下键"] = 0x28; m["左键"] = 0x25; m["右键"] = 0x27;
            m["UP"] = 0x26; m["DOWN"] = 0x28; m["LEFT"] = 0x25; m["RIGHT"] = 0x27;
            m["INSERT"] = 0x2D; m["DELETE"] = 0x2E; m["DEL"] = 0x2E;
            m["HOME"] = 0x24; m["END"] = 0x23; m["PAGEUP"] = 0x21; m["PAGEDOWN"] = 0x22;
            m["-"] = 0xBD; m["="] = 0xBB; m["["] = 0xDB; m["]"] = 0xDD; m["\\"] = 0xDC;
            m[";"] = 0xBA; m["'"] = 0xDE; m[","] = 0xBC; m["."] = 0xBE; m["/"] = 0xBF; m["`"] = 0xC0;
            m["NUM0"] = 0x60; m["NUM1"] = 0x61; m["NUM2"] = 0x62; m["NUM3"] = 0x63; m["NUM4"] = 0x64;
            m["NUM5"] = 0x65; m["NUM6"] = 0x66; m["NUM7"] = 0x67; m["NUM8"] = 0x68; m["NUM9"] = 0x69;
            return m;
        }
        public static ushort KeyToVk(string name)
        {
            string k = (name ?? "").Trim().ToUpperInvariant();
            ushort vk;
            if (KeyNames.TryGetValue(k, out vk)) return vk;
            if (k.Length == 1)
            {
                char c = k[0];
                if (c >= 'a' && c <= 'z') return (ushort)(0x41 + c - 'a');
                if (c >= 'A' && c <= 'Z') return (ushort)c;
                if (c >= '0' && c <= '9') return (ushort)c;
            }
            return 0;
        }

        // ---------- 鼠标操作 ----------
        public static void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
        }
        public static void MouseDown(uint button)
        {
            mouse_event(button, 0, 0, 0, UIntPtr.Zero);
        }
        public static void MouseUp(uint button)
        {
            mouse_event(button + 2, 0, 0, 0, UIntPtr.Zero); // DOWN+2 = UP flag
        }
        public static void MouseWheel(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
        }
        public static POINT GetMousePos()
        {
            POINT p; GetCursorPos(out p); return p;
        }
        public static bool IsMouseDown(uint button)
        {
            int vk = button == MOUSEEVENTF_LEFTDOWN ? 0x01 : button == MOUSEEVENTF_RIGHTDOWN ? 0x02 : 0x04;
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }
        public static bool IsKeyDown(ushort vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        // ---------- 键盘操作 ----------
        public static void KeyDown(ushort vk)
        {
            keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
        }
        public static void KeyUp(ushort vk)
        {
            keybd_event((byte)vk, 0, 2, UIntPtr.Zero);
        }
        public static void KeyClick(ushort vk)
        {
            KeyDown(vk); System.Threading.Thread.Sleep(15); KeyUp(vk);
        }

        // unicode 字符输入（发送字符串2）
        public static void SendUnicodeChar(char c)
        {
            const uint KEYEVENTF_UNICODE = 0x0001, KEYEVENTF_KEYUP = 0x0002;
            INPUT down = new INPUT(); down.type = 1;
            down.u.ki.wVk = 0; down.u.ki.wScan = c; down.u.ki.dwFlags = KEYEVENTF_UNICODE;
            INPUT up = new INPUT(); up.type = 1;
            up.u.ki.wVk = 0; up.u.ki.wScan = c; up.u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            INPUT[] arr = new INPUT[] { down, up };
            SendInput(2, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public UIntPtr dwExtraInfo;
            public uint unused1, unused2; // 对齐 MOUSEINPUT 大小
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTunion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTunion u;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // ---------- 窗口枚举 ----------
        public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);

        public static List<IntPtr> TopWindows()
        {
            List<IntPtr> list = new List<IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (IsWindowVisible(h))
                {
                    string t = GetWindowText(h);
                    if (!string.IsNullOrEmpty(t)) list.Add(h);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }
        public static IntPtr FindWindowByTitle(string title)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (IsWindowVisible(h))
                {
                    string t = GetWindowText(h);
                    if (!string.IsNullOrEmpty(t) && t.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = h; return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
    }
}
