using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BulingOffline
{
    // DLL 宿主：让程序目录中的全部 DLL 都可用
    // - Inspect/ExportNames：纯 C# 解析 PE 头，得到架构/导出表/.NET 程序集标记
    // - ScriptLoad/ScriptCall/ScriptUnload：脚本命令 加载DLL/调用DLL/调用DLL函数/卸载DLL
    //   x64 DLL 在本进程内直接 LoadLibrary + GetProcAddress 调用；
    //   x86 DLL 在 64 位进程里自动转交 DllBridge32.exe 桥接调用（反之亦然）。
    public static class DllHost
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr h);
        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr h, string name);
        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);
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

        static long InvokeProc(IntPtr p, bool std, int n, IntPtr[] a)
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

        public static bool Is64BitProcess { get { return IntPtr.Size == 8; } }

        // ================= PE 解析 =================
        public class DllInfo
        {
            public string Name = "";
            public string Path = "";
            public string Arch = "?";        // x64 / x86 / ARM64 / ?
            public int Exports = 0;          // 导出函数数量
            public bool HasExports;
            public bool IsNet;               // .NET 程序集
            public bool SystemDep;           // 运行库依赖（Qt/opencv/paddle/CRT 等）
            public bool LoadTested;
            public bool LoadOk;
            public uint LastError;

            public string Kind
            {
                get
                {
                    if (IsNet) return "NET程序集";
                    if (SystemDep) return "运行库依赖";
                    if (HasExports) return "可调用插件";
                    return "资源库";
                }
            }
            public string StatusText
            {
                get
                {
                    if (IsNet) return ".NET程序集(需反射宿主)";
                    if (Arch == "x86" && Is64BitProcess) return "x86·经DllBridge32调用";
                    if (Arch == "x64" && !Is64BitProcess) return "x64·经DllBridge64调用";
                    if (Arch == "x86" || Arch == "x64") return "可调用";
                    if (LoadTested) return LoadOk ? "可加载" : "加载失败(" + LastError + ")";
                    return "未知架构";
                }
            }
        }

        class Section { public uint VA, VSize, RawOff, RawSize; }

        static List<Section> ReadSections(BinaryReader br, int peOff, int numSec, int optSize)
        {
            List<Section> secs = new List<Section>();
            int off = peOff + 24 + optSize;
            for (int i = 0; i < numSec; i++)
            {
                br.BaseStream.Seek(off + i * 40 + 8, SeekOrigin.Begin);
                Section s = new Section();
                s.VSize = br.ReadUInt32(); s.VA = br.ReadUInt32();
                s.RawSize = br.ReadUInt32(); s.RawOff = br.ReadUInt32();
                secs.Add(s);
            }
            return secs;
        }
        static long Rva2Off(uint rva, List<Section> secs)
        {
            foreach (Section s in secs)
            {
                uint sz = Math.Max(s.VSize, s.RawSize);
                if (rva >= s.VA && rva < s.VA + sz)
                    return rva - s.VA + s.RawOff;
            }
            return -1;
        }

        public static DllInfo Inspect(string path)
        {
            DllInfo d = new DllInfo();
            d.Path = path;
            d.Name = Path.GetFileName(path);
            d.SystemDep = IsSystemDep(d.Name);
            try
            {
                using (FileStream fs = File.OpenRead(path))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 0x40) return d;
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOff = br.ReadInt32();
                    if (peOff <= 0 || peOff + 264 > fs.Length) return d;
                    fs.Seek(peOff, SeekOrigin.Begin);
                    if (br.ReadUInt32() != 0x00004550) return d;  // PE\0\0
                    fs.Seek(peOff + 4, SeekOrigin.Begin);
                    ushort machine = br.ReadUInt16();
                    if (machine == 0x8664) d.Arch = "x64";
                    else if (machine == 0x014C) d.Arch = "x86";
                    else if (machine == 0xAA64) d.Arch = "ARM64";
                    fs.Seek(peOff + 6, SeekOrigin.Begin);
                    ushort numSec = br.ReadUInt16();
                    fs.Seek(peOff + 20, SeekOrigin.Begin);
                    ushort optSize = br.ReadUInt16();
                    fs.Seek(peOff + 24, SeekOrigin.Begin);
                    ushort magic = br.ReadUInt16();
                    int ddOff = magic == 0x20B ? peOff + 24 + 112 : peOff + 24 + 96;
                    if (ddOff + 128 > fs.Length) return d;
                    // 数据目录 0=导出表 14=CLR
                    fs.Seek(ddOff, SeekOrigin.Begin);
                    uint expRva = br.ReadUInt32();
                    fs.Seek(ddOff + 14 * 8, SeekOrigin.Begin);
                    uint clrRva = br.ReadUInt32();
                    d.IsNet = clrRva != 0;
                    List<Section> secs = ReadSections(br, peOff, numSec, optSize);
                    if (expRva != 0)
                    {
                        long eo = Rva2Off(expRva, secs);
                        if (eo > 0)
                        {
                            fs.Seek(eo + 16, SeekOrigin.Begin);
                            uint numFuncs = br.ReadUInt32();
                            fs.Seek(eo + 20, SeekOrigin.Begin);
                            uint numNames = br.ReadUInt32();
                            d.Exports = (int)Math.Max(numFuncs, numNames);
                            d.HasExports = d.Exports > 0;
                        }
                    }
                }
            }
            catch { }
            return d;
        }

        static readonly string[] DepPrefixes = new string[] {
            "api-ms-", "ext-ms-", "qt5", "qt6", "qtwebengine", "opencv_", "paddle", "onnxruntime",
            "msvcp", "vcruntime", "concrt", "ucrtbase", "libcrypto", "libssl", "libcurl",
            "libgcc", "libstdc++", "libwinpthread", "libegl", "libglesv2", "d3dcompiler",
            "opengl32sw", "python3", "mklml", "mkldnn", "libiomp5md", "d3d", "mscoree"
        };
        static bool IsSystemDep(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (string p in DepPrefixes) if (n.StartsWith(p)) return true;
            return false;
        }

        // 导出函数名列表（不加载 DLL，直接读导出表）
        public static List<string> ExportNames(string path, int cap)
        {
            List<string> r = new List<string>();
            try
            {
                using (FileStream fs = File.OpenRead(path))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (fs.Length < 0x40) return r;
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOff = br.ReadInt32();
                    if (peOff <= 0 || peOff + 264 > fs.Length) return r;
                    fs.Seek(peOff, SeekOrigin.Begin);
                    if (br.ReadUInt32() != 0x00004550) return r;
                    fs.Seek(peOff + 6, SeekOrigin.Begin);
                    ushort numSec = br.ReadUInt16();
                    fs.Seek(peOff + 20, SeekOrigin.Begin);
                    ushort optSize = br.ReadUInt16();
                    fs.Seek(peOff + 24, SeekOrigin.Begin);
                    ushort magic = br.ReadUInt16();
                    int ddOff = magic == 0x20B ? peOff + 24 + 112 : peOff + 24 + 96;
                    fs.Seek(ddOff, SeekOrigin.Begin);
                    uint expRva = br.ReadUInt32();
                    if (expRva == 0) return r;
                    List<Section> secs = ReadSections(br, peOff, numSec, optSize);
                    long eo = Rva2Off(expRva, secs);
                    if (eo < 0) return r;
                    fs.Seek(eo + 20, SeekOrigin.Begin);
                    uint numNames = br.ReadUInt32();
                    fs.Seek(eo + 32, SeekOrigin.Begin);
                    uint namesRva = br.ReadUInt32();
                    long no = Rva2Off(namesRva, secs);
                    if (no < 0) return r;
                    int n = (int)Math.Min(numNames, (uint)Math.Max(0, cap));
                    for (int i = 0; i < n; i++)
                    {
                        fs.Seek(no + i * 4, SeekOrigin.Begin);
                        uint nrva = br.ReadUInt32();
                        long o = Rva2Off(nrva, secs);
                        if (o < 0) { r.Add(""); continue; }
                        fs.Seek(o, SeekOrigin.Begin);
                        StringBuilder sb = new StringBuilder();
                        for (int k = 0; k < 256; k++)
                        {
                            byte c = br.ReadByte();
                            if (c == 0) break;
                            sb.Append((char)c);
                        }
                        r.Add(sb.ToString());
                    }
                }
            }
            catch { }
            return r;
        }

        // 导出函数名列表（脚本命令 获取DLL导出 用，支持程序目录内文件名或系统 DLL 名）
        public static List<string> ScriptExports(string name, int cap, Action<string> log)
        {
            string path = Resolve(name);
            if (path == null && !string.IsNullOrEmpty(name))
            {
                string sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), name);
                if (File.Exists(sys)) path = sys;
            }
            if (path == null)
            {
                if (log != null) log("获取DLL导出失败: 找不到 " + name);
                return new List<string>();
            }
            return ExportNames(path, cap);
        }

        // 测试加载（不常驻：加载后立即释放）
        public static bool TestLoad(string path, out uint err)
        {
            IntPtr h = LoadLibrary(path);
            err = h == IntPtr.Zero ? (uint)Marshal.GetLastWin32Error() : 0;
            if (h != IntPtr.Zero) FreeLibrary(h);
            return h != IntPtr.Zero;
        }

        // ================= 脚本调用支持 =================
        class Handle
        {
            public IntPtr Ptr;          // 本进程内模块句柄
            public string Path;         // DLL 路径
            public bool External;       // 需经桥接器调用
        }
        static readonly Dictionary<long, Handle> handles = new Dictionary<long, Handle>();
        static readonly Dictionary<string, IntPtr> libCache = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        static long nextHandle = 1;

        public static string BaseDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }

        static string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Path.IsPathRooted(name) || name.Contains("\\")) return File.Exists(name) ? name : null;
            string local = Path.Combine(BaseDir, name);
            if (File.Exists(local)) return local;
            return null;   // 系统 DLL（user32 等）按原名交给系统搜索
        }

        static bool ArchCompatible(DllInfo info)
        {
            if (info.Arch == "x64") return Is64BitProcess;
            if (info.Arch == "x86") return !Is64BitProcess;
            return true;
        }

        public static long ScriptLoad(string name, Action<string> log)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string path = Resolve(name);
            DllInfo info = path != null ? Inspect(path) : null;
            if (info != null && info.IsNet)
            {
                if (log != null) log("加载DLL失败: " + info.Name + " 是 .NET 程序集，不能用 LoadLibrary 加载");
                return 0;
            }
            Handle h = new Handle();
            h.Path = path ?? name;
            if (info != null && !ArchCompatible(info))
            {
                h.External = true;
                if (log != null) log(info.Name + " 为 " + info.Arch + " 库，将通过 DllBridge" + (info.Arch == "x86" ? "32" : "64") + ".exe 桥接调用");
            }
            else
            {
                IntPtr p;
                if (!libCache.TryGetValue(h.Path, out p) || p == IntPtr.Zero)
                {
                    p = LoadLibrary(h.Path);
                    if (p == IntPtr.Zero)
                    {
                        uint err = (uint)Marshal.GetLastWin32Error();
                        if (log != null) log("加载DLL失败: " + h.Path + " (Win32错误 " + err + ")");
                        return 0;
                    }
                    libCache[h.Path] = p;
                }
                h.Ptr = p;
            }
            long id = nextHandle++;
            lock (handles) handles[id] = h;
            return id;
        }

        public static bool ScriptUnload(long id)
        {
            Handle h;
            lock (handles) { if (!handles.TryGetValue(id, out h)) return false; handles.Remove(id); }
            if (!h.External && h.Ptr != IntPtr.Zero)
            {
                // 缓存里的模块不真正释放，避免重复句柄失效；仅移除句柄映射
            }
            return true;
        }

        public static int ScriptCall(long idOrZero, string dllName, string func, List<object> args, bool stdcall, Action<string> log)
        {
            try
            {
                Handle h = null;
                if (idOrZero > 0)
                {
                    lock (handles) { if (!handles.TryGetValue(idOrZero, out h)) h = null; }
                    if (h == null) { if (log != null) log("调用DLL失败: 句柄无效 " + idOrZero); return 0; }
                }
                else
                {
                    long id = ScriptLoad(dllName, log);
                    if (id == 0) return 0;
                    lock (handles) handles.TryGetValue(id, out h);
                }
                if (h == null || string.IsNullOrEmpty(func)) return 0;

                if (h.External)
                    return BridgeCall(h.Path, func, args, stdcall, log);

                IntPtr p;
                if (!libCache.TryGetValue(h.Path, out p) || p == IntPtr.Zero)
                {
                    p = LoadLibrary(h.Path);
                    if (p == IntPtr.Zero) { if (log != null) log("调用DLL失败: 无法加载 " + h.Path); return 0; }
                    libCache[h.Path] = p;
                }
                IntPtr proc = GetProcAddress(p, func);
                if (proc == IntPtr.Zero)
                {
                    if (log != null) log("调用DLL失败: 未找到导出函数 " + func + "（可用 获取DLL导出 查看函数列表）");
                    return 0;
                }
                if (args.Count > 8)
                {
                    if (log != null) log("调用DLL: 最多支持 8 个参数，多余参数已忽略");
                    args = args.GetRange(0, 8);
                }
                IntPtr[] a = new IntPtr[8];
                List<IntPtr> frees = new List<IntPtr>();
                for (int i = 0; i < args.Count; i++)
                {
                    string s = args[i] as string;
                    if (s != null) { IntPtr q = Marshal.StringToHGlobalAnsi(s); frees.Add(q); a[i] = q; }
                    else a[i] = (IntPtr)Convert.ToInt64(args[i]);
                }
                long ret = InvokeProc(proc, stdcall, args.Count, a);
                foreach (IntPtr q in frees) Marshal.FreeHGlobal(q);
                return (int)ret;
            }
            catch (Exception ex)
            {
                if (log != null) log("调用DLL出错: " + ex.Message);
                return 0;
            }
        }

        // x86/x64 不匹配时经桥接器（独立进程，架构与 DLL 一致）调用
        static int BridgeCall(string dllPath, string func, List<object> args, bool stdcall, Action<string> log)
        {
            DllInfo info = Inspect(dllPath);
            string bridge = Path.Combine(BaseDir, info.Arch == "x86" ? "DllBridge32.exe" : "DllBridge64.exe");
            if (!File.Exists(bridge))
            {
                if (log != null) log("桥接调用失败: 未找到 " + Path.GetFileName(bridge));
                return 0;
            }
            List<string> pa = new List<string>();
            pa.Add(dllPath);
            pa.Add(func);
            pa.Add(stdcall ? "stdcall" : "cdecl");
            int n = Math.Min(8, args.Count);
            for (int i = 0; i < n; i++)
            {
                string s = args[i] as string;
                pa.Add(s != null ? "s:" + s : "i:" + Convert.ToInt64(args[i]));
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(bridge, QuoteAll(pa));
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (Process pr = Process.Start(psi))
                {
                    string output = pr.StandardOutput.ReadToEnd();
                    if (!pr.WaitForExit(10000)) { try { pr.Kill(); } catch { } return 0; }
                    int ret = 0;
                    foreach (string ln in output.Split('\n'))
                    {
                        string t = ln.Trim();
                        if (t.StartsWith("RET ")) int.TryParse(t.Substring(4), out ret);
                        else if (t.StartsWith("ERR") && log != null) log("桥接调用: " + t);
                    }
                    return ret;
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("桥接调用出错: " + ex.Message);
                return 0;
            }
        }
        static string QuoteAll(List<string> items)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append('"').Append(items[i].Replace("\"", "\\\"")).Append('"');
            }
            return sb.ToString();
        }
    }
}
