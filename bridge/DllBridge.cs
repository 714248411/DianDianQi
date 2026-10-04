using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

// DLL 桥接器：独立小进程，架构与目标 DLL 一致（DllBridge32.exe / DllBridge64.exe），
// 用于让主程序调用与本进程架构不同的 DLL（如 64 位主程序调用 x86 DLL）。
// 用法: DllBridge32.exe "C:\path\xx.dll" "函数名" stdcall|cdecl [i:123 | s:文本 ...]
// 输出: RET <整数返回值>，或 ERR <原因>
static class DllBridge
{
    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr LoadLibrary(string s);
    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr h, string s);
    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool SetDllDirectory(string s);
    [DllImport("kernel32")] static extern bool IsBadReadPtr(IntPtr p, UIntPtr len);

    // 按实际参数个数定义委托：32 位下 stdcall 由被调方清栈，参数个数必须精确匹配
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S0();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S1(IntPtr a);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S2(IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S3(IntPtr a, IntPtr b, IntPtr c);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S4(IntPtr a, IntPtr b, IntPtr c, IntPtr d);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S5(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S6(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S7(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr S8(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C0();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C1(IntPtr a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C2(IntPtr a, IntPtr b);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C3(IntPtr a, IntPtr b, IntPtr c);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C4(IntPtr a, IntPtr b, IntPtr c, IntPtr d);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C5(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C6(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C7(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr C8(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e, IntPtr f, IntPtr g, IntPtr h);

    static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2) { Out("ERR 用法: DllBridge.exe <dll> <函数名> [stdcall|cdecl] [参数...]"); return 1; }
            string dll = args[0];
            string func = args[1];
            string conv = args.Length > 2 ? args[2] : "stdcall";
            int narg = Math.Min(8, Math.Max(0, args.Length - 3));

            string dir = Path.GetDirectoryName(Path.GetFullPath(dll));
            if (!string.IsNullOrEmpty(dir)) SetDllDirectory(dir);
            IntPtr h = LoadLibrary(dll);
            if (h == IntPtr.Zero) { Out("ERR LoadLibrary 失败 Win32=" + Marshal.GetLastWin32Error()); return 2; }
            IntPtr p = GetProcAddress(h, func);
            if (p == IntPtr.Zero) { Out("ERR 未找到导出函数 " + func + " Win32=" + Marshal.GetLastWin32Error()); return 3; }

            IntPtr[] a = new IntPtr[8];
            for (int i = 0; i < narg; i++)
            {
                string s = args[3 + i] ?? "";
                if (s.StartsWith("s:")) a[i] = Marshal.StringToHGlobalAnsi(s.Substring(2));
                else if (s.StartsWith("w:")) a[i] = Marshal.StringToHGlobalUni(s.Substring(2));
                else
                {
                    long v;
                    if (!long.TryParse(s.StartsWith("i:") ? s.Substring(2) : s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) v = 0;
                    a[i] = (IntPtr)v;
                }
            }
            bool std = conv != "cdecl";
            long ret = Invoke(p, std, narg, a);
            Out("RET " + ret);
            // 返回值若指向可读内存，尝试按 ANSI 字符串输出（供需要文本返回的场景参考）
            if (ret > 65536 && ret < 0x7FFFFFFFFFFF && !IsBadReadPtr((IntPtr)ret, (UIntPtr)1))
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < 4096; i++)
                    {
                        byte b = Marshal.ReadByte((IntPtr)ret, i);
                        if (b == 0) break;
                        sb.Append((char)b);
                    }
                    string s = sb.ToString();
                    if (s.Length > 0 && IsMostlyPrintable(s)) Out("STR " + s);
                }
                catch { }
            }
            return 0;
        }
        catch (Exception ex)
        {
            Out("ERR " + ex.Message);
            return 1;
        }
    }

    static long Invoke(IntPtr p, bool std, int n, IntPtr[] a)
    {
        if (std)
        {
            switch (n)
            {
                case 0: { S0 f = (S0)Marshal.GetDelegateForFunctionPointer(p, typeof(S0)); return (long)f(); }
                case 1: { S1 f = (S1)Marshal.GetDelegateForFunctionPointer(p, typeof(S1)); return (long)f(a[0]); }
                case 2: { S2 f = (S2)Marshal.GetDelegateForFunctionPointer(p, typeof(S2)); return (long)f(a[0], a[1]); }
                case 3: { S3 f = (S3)Marshal.GetDelegateForFunctionPointer(p, typeof(S3)); return (long)f(a[0], a[1], a[2]); }
                case 4: { S4 f = (S4)Marshal.GetDelegateForFunctionPointer(p, typeof(S4)); return (long)f(a[0], a[1], a[2], a[3]); }
                case 5: { S5 f = (S5)Marshal.GetDelegateForFunctionPointer(p, typeof(S5)); return (long)f(a[0], a[1], a[2], a[3], a[4]); }
                case 6: { S6 f = (S6)Marshal.GetDelegateForFunctionPointer(p, typeof(S6)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5]); }
                case 7: { S7 f = (S7)Marshal.GetDelegateForFunctionPointer(p, typeof(S7)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5], a[6]); }
                default: { S8 f = (S8)Marshal.GetDelegateForFunctionPointer(p, typeof(S8)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7]); }
            }
        }
        switch (n)
        {
            case 0: { C0 f = (C0)Marshal.GetDelegateForFunctionPointer(p, typeof(C0)); return (long)f(); }
            case 1: { C1 f = (C1)Marshal.GetDelegateForFunctionPointer(p, typeof(C1)); return (long)f(a[0]); }
            case 2: { C2 f = (C2)Marshal.GetDelegateForFunctionPointer(p, typeof(C2)); return (long)f(a[0], a[1]); }
            case 3: { C3 f = (C3)Marshal.GetDelegateForFunctionPointer(p, typeof(C3)); return (long)f(a[0], a[1], a[2]); }
            case 4: { C4 f = (C4)Marshal.GetDelegateForFunctionPointer(p, typeof(C4)); return (long)f(a[0], a[1], a[2], a[3]); }
            case 5: { C5 f = (C5)Marshal.GetDelegateForFunctionPointer(p, typeof(C5)); return (long)f(a[0], a[1], a[2], a[3], a[4]); }
            case 6: { C6 f = (C6)Marshal.GetDelegateForFunctionPointer(p, typeof(C6)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5]); }
            case 7: { C7 f = (C7)Marshal.GetDelegateForFunctionPointer(p, typeof(C7)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5], a[6]); }
            default: { C8 f = (C8)Marshal.GetDelegateForFunctionPointer(p, typeof(C8)); return (long)f(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7]); }
        }
    }

    static bool IsMostlyPrintable(string s)
    {
        int ok = 0;
        foreach (char c in s)
            if (c >= ' ' && c != 0x7F) ok++;
        return s.Length > 0 && ok * 10 >= s.Length * 8;
    }

    static void Out(string s)
    {
        try
        {
            byte[] b = Encoding.UTF8.GetBytes(s + "\n");
            using (Stream o = Console.OpenStandardOutput()) o.Write(b, 0, b.Length);
        }
        catch { }
    }
}
