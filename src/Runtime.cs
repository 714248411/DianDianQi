using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BulingOffline
{
    // 运行时：全部内置命令实现（前台模式）
    public class Runtime
    {
        public volatile bool StopFlag;
        public Action<string> LogFn;
        public Action<int> ProgressFn;             // 执行轮次回调（初级视图“当前次数”）
        public IntPtr BoundWindow = IntPtr.Zero;   // 绑定窗口句柄

        public int AccelOffset;                    // 加速延迟偏移
        public int MouseClickDelay = 20;           // 按下-抬起间隔
        public int KeyClickDelay = 20;
        public int MouseOffsetX, MouseOffsetY;     // 鼠标偏移
        public bool TrajectoryOn;                  // 移动轨迹
        public int TrajectorySpeed = 1;

        // 线程
        Dictionary<long, Thread> threads = new Dictionary<long, Thread>();
        long nextTid = 1;

        // 锁
        static Dictionary<string, SemaphoreSlim> taskLocks = new Dictionary<string, SemaphoreSlim>();
        static Dictionary<string, object> threadLocks = new Dictionary<string, object>();
        Dictionary<SemaphoreSlim, int> heldTaskLocks = new Dictionary<SemaphoreSlim, int>();

        Random rnd = new Random();

        public Runtime() { }

        public void Log(string s)
        {
            Action<string> f = LogFn;
            if (f != null) f(s);
        }

        public void CheckStop()
        {
            if (StopFlag) throw new StopSignal();
        }

        public void Sleep(long ms)
        {
            long target = Environment.TickCount + ms;
            while (Environment.TickCount < target)
            {
                CheckStop();
                long left = target - Environment.TickCount;
                Thread.Sleep(left > 50 ? 50 : left < 1 ? 1 : (int)left);
            }
            CheckStop();
        }

        // ================= 调度 =================
        public Value Call(string name, CallArgs a)
        {
            // 后台图色：视觉命令自动改从绑定窗口截图
            Vision.CaptureSource = (BackendCap && BoundWindow != IntPtr.Zero) ? BoundWindow : IntPtr.Zero;
            switch (name)
            {
                // ---- 常用操作 ----
                case "延迟": Delay(a); return Value.Int(1);
                case "随机延迟": Delay(Rnd(a.I(0), a.I(1))); return Value.Int(1);
                case "消息框": MsgBox(a); return Value.Int(1);
                case "退出": throw new StopSignal();

                // ---- 鼠标 ----
                case "鼠标移动": DoMouseMove(a, false); return Value.Int(1);
                case "鼠标相对移动": DoMouseMove(a, true); return Value.Int(1);
                case "鼠标左键单击": DoClick(a, Native.MOUSEEVENTF_LEFTDOWN, 1); return Value.Int(1);
                case "鼠标左键双击": DoClick(a, Native.MOUSEEVENTF_LEFTDOWN, 2); return Value.Int(1);
                case "鼠标左键按下": DoPress(a, Native.MOUSEEVENTF_LEFTDOWN, true); return Value.Int(1);
                case "鼠标左键抬起": DoPress(a, Native.MOUSEEVENTF_LEFTDOWN, false); return Value.Int(1);
                case "鼠标右键单击": DoClick(a, Native.MOUSEEVENTF_RIGHTDOWN, 1); return Value.Int(1);
                case "鼠标右键双击": DoClick(a, Native.MOUSEEVENTF_RIGHTDOWN, 2); return Value.Int(1);
                case "鼠标右键按下": DoPress(a, Native.MOUSEEVENTF_RIGHTDOWN, true); return Value.Int(1);
                case "鼠标右键抬起": DoPress(a, Native.MOUSEEVENTF_RIGHTDOWN, false); return Value.Int(1);
                case "鼠标中键单击": DoClick(a, Native.MOUSEEVENTF_MIDDLEDOWN, 1); return Value.Int(1);
                case "鼠标中键双击": DoClick(a, Native.MOUSEEVENTF_MIDDLEDOWN, 2); return Value.Int(1);
                case "鼠标中键按下": DoPress(a, Native.MOUSEEVENTF_MIDDLEDOWN, true); return Value.Int(1);
                case "鼠标中键抬起": DoPress(a, Native.MOUSEEVENTF_MIDDLEDOWN, false); return Value.Int(1);
                case "鼠标向上滚动": DoWheel(a, 1); return Value.Int(1);
                case "鼠标向下滚动": DoWheel(a, -1); return Value.Int(1);

                // ---- 键盘 ----
                case "键盘单击": DoKey(a, false, 0); return Value.Int(1);
                case "键盘按下": DoKey(a, false, 1); return Value.Int(1);
                case "键盘抬起": DoKey(a, false, 2); return Value.Int(1);
                case "键值单击": DoKey(a, true, 0); return Value.Int(1);
                case "键值按下": DoKey(a, true, 1); return Value.Int(1);
                case "键值抬起": DoKey(a, true, 2); return Value.Int(1);
                case "等待按键": return DoWaitKey(a, false);
                case "等待键值": return DoWaitKey(a, true);
                case "等待鼠标": return DoWaitMouse(a);

                // ---- 字符串剪切板 ----
                case "字符串长度": return Value.Int(a.S(0).Length);
                case "拆分字符串":
                    {
                        string[] parts = a.S(0).Split(new string[] { a.S(1) }, StringSplitOptions.None);
                        RefArg arr = a.R(2);
                        if (arr != null)
                        {
                            Slot sl = arr.Slot;
                            sl.Arr = new List<Value>();
                            foreach (string p in parts) sl.Arr.Add(Value.Str(p));
                        }
                        return Value.Int(parts.Length);
                    }
                case "提取字符串":
                    {
                        string s = a.S(0);
                        int start = a.I(1);
                        if (start < 0 || start >= s.Length) return Value.Str("");
                        if (a.Count >= 3)
                        {
                            int len = a.I(2);
                            if (len < 0) return Value.Str(s.Substring(start));
                            if (start + len > s.Length) len = s.Length - start;
                            return Value.Str(s.Substring(start, len));
                        }
                        return Value.Str(s.Substring(start, 1));
                    }
                case "是否包含字符串":
                    {
                        int idx = a.S(0).IndexOf(a.S(1), StringComparison.Ordinal);
                        return Value.Int(idx);
                    }
                case "替换字符串": return Value.Str(a.S(0).Replace(a.S(1), a.S(2)));
                case "获取剪切板文本": return Value.Str(GetClipboard());
                case "设置剪切板文本": SetClipboard(a.S(0)); return Value.Int(1);
                case "发送字符串": SendStringToWindow(a.S(0), 0); return Value.Int(1);
                case "发送字符串2": SendStringToWindow(a.S(0), 1); return Value.Int(1);
                case "发送剪切板": SendPaste(); return Value.Int(1);

                // ---- 文件 ----
                case "读取文本文件":
                    {
                        try { a.SetRef(1, Value.Str(File.ReadAllText(a.S(0), new UTF8Encoding(false)))); return Value.Int(1); }
                        catch { a.SetRef(1, Value.Str("")); return Value.Int(0); }
                    }
                case "写入文本文件":
                    {
                        try { File.WriteAllText(a.S(0), a.S(1), new UTF8Encoding(false)); return Value.Int(1); }
                        catch { return Value.Int(0); }
                    }
                case "追加文本文件":
                    {
                        try { File.AppendAllText(a.S(0), a.S(1), new UTF8Encoding(false)); return Value.Int(1); }
                        catch { return Value.Int(0); }
                    }
                case "获取文件大小":
                    {
                        try { FileInfo fi = new FileInfo(a.S(0)); return Value.Int(fi.Exists ? fi.Length : -1); }
                        catch { return Value.Int(-1); }
                    }

                // ---- 时间日期 ----
                case "获取本地时间": return Value.Str(DateTime.Now.ToString("HH:mm:ss"));
                case "获取本地日期时间": return Value.Str(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                case "获取本地时间戳": return Value.Int((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
                case "获取本地年": return Value.Int(DateTime.Now.Year);
                case "获取本地月": return Value.Int(DateTime.Now.Month);
                case "获取本地日": return Value.Int(DateTime.Now.Day);
                case "获取本地时": return Value.Int(DateTime.Now.Hour);
                case "获取本地分": return Value.Int(DateTime.Now.Minute);
                case "获取本地秒": return Value.Int(DateTime.Now.Second);
                case "等待时间": return DoWaitTime(a);

                // ---- 数学 ----
                case "正弦值": return Value.Dbl(Math.Sin(Deg(a.D(0))));
                case "余弦值": return Value.Dbl(Math.Cos(Deg(a.D(0))));
                case "正切值": return Value.Dbl(Math.Tan(Deg(a.D(0))));
                case "反正弦值": return Value.Dbl(Rad(Math.Asin(a.D(0))));
                case "反余弦值": return Value.Dbl(Rad(Math.Acos(a.D(0))));
                case "反正切值": return Value.Dbl(Rad(Math.Atan(a.D(0))));
                case "幂函数": return Value.Dbl(Math.Pow(a.D(0), a.D(1)));
                case "求平方根": return Value.Dbl(Math.Sqrt(a.D(0)));
                case "绝对值": { double d = a.D(0); return (a.V(0).T == 1) ? Value.Dbl(Math.Abs(d)) : Value.Int(Math.Abs(a.I(0))); }
                case "最大值":
                    {
                        double m = double.MinValue;
                        for (int i = 0; i < a.Count; i++) m = Math.Max(m, a.D(i));
                        return Value.Dbl(m);
                    }
                case "最小值":
                    {
                        double m = double.MaxValue;
                        for (int i = 0; i < a.Count; i++) m = Math.Min(m, a.D(i));
                        return Value.Dbl(m);
                    }

                // ---- 类型转换 ----
                case "到字符串": return Value.Str(a.V(0).AsStr());
                case "到整数": return Value.Int(a.V(0).AsInt());
                case "到小数": return Value.Dbl(a.V(0).AsDbl());

                // ---- 进程 ----
                case "启动进程": return DoStartProcess(a);
                case "获取进程编号":
                    {
                        try
                        {
                            Process[] ps = Process.GetProcessesByName(StripExe(a.S(0)));
                            if (ps.Length == 0) ps = Process.GetProcessesByName(a.S(0));
                            if (ps.Length > 0) { a.SetRef(1, Value.Int(ps[0].Id)); return Value.Int(1); }
                            a.SetRef(1, Value.Int(0)); return Value.Int(0);
                        }
                        catch { a.SetRef(1, Value.Int(0)); return Value.Int(0); }
                    }
                case "通过名称杀死进程":
                    {
                        try
                        {
                            foreach (Process p in Process.GetProcessesByName(StripExe(a.S(0)))) { try { p.Kill(); } catch { } }
                            return Value.Int(1);
                        }
                        catch { return Value.Int(0); }
                    }
                case "通过编号杀死进程":
                    {
                        try { Process p = Process.GetProcessById(a.I(0)); p.Kill(); return Value.Int(1); }
                        catch { return Value.Int(0); }
                    }

                // ---- 窗口 ----
                case "查找窗口": { IntPtr h = Native.FindWindowByTitle(a.S(0)); return Value.Int(h.ToInt64()); }
                case "获取绑定窗口": return Value.Int(BoundWindow.ToInt64());
                case "获取鼠标位置窗口":
                    {
                        Native.POINT p = Native.GetMousePos();
                        IntPtr h = Native.WindowFromPoint(p);
                        h = Native.RootOwner(h);
                        return Value.Int(h.ToInt64());
                    }
                case "获取鼠标位置窗口具体":
                    {
                        Native.POINT p = Native.GetMousePos();
                        IntPtr h = Native.WindowFromPoint(p);
                        return Value.Int(h.ToInt64());
                    }
                case "移动窗口": { IntPtr h = Hwnd(a); Native.MoveWindow(h, a.I(1), a.I(2), CurrentW(h), CurrentH(h), true); return Value.Int(1); }
                case "设置窗口大小": { IntPtr h = Hwnd(a); Native.SetWindowPos(h, IntPtr.Zero, 0, 0, a.I(1), a.I(2), 0x0002 | 0x0004); return Value.Int(1); }
                case "设置窗口显示": Native.ShowWindow(Hwnd(a), a.I(1) != 0 ? 5 : 0); return Value.Int(1);
                case "关闭窗口": Native.PostMessage(Hwnd(a), Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); return Value.Int(1);

                // ---- 图色 ----
                case "屏幕截图": return Value.Int(Vision.CaptureRect(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4)));
                case "获取图片": return Value.Int(Vision.CaptureRect(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4)));
                case "获取颜色": return Value.Str(Vision.GetColor(a.I(0), a.I(1)));
                case "获取颜色数量": return Value.Int(Vision.CountColor(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4)));
                case "查找颜色":
                    {
                        int x, y;
                        int ok = Vision.FindColor(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4), a.D(5), out x, out y);
                        if (ok == 1) { a.SetRef(7, Value.Int(x)); a.SetRef(8, Value.Int(y)); }
                        else { a.SetRef(7, Value.Int(-1)); a.SetRef(8, Value.Int(-1)); }
                        return Value.Int(ok);
                    }
                case "多点查找颜色":
                    {
                        int x, y;
                        int ok = Vision.MultiFindColor(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4), a.S(5), a.D(6), out x, out y);
                        if (ok == 1) { a.SetRef(8, Value.Int(x)); a.SetRef(9, Value.Int(y)); }
                        else { a.SetRef(8, Value.Int(-1)); a.SetRef(9, Value.Int(-1)); }
                        return Value.Int(ok);
                    }
                case "查找图片":
                    {
                        int x, y;
                        int ok = Vision.FindImage(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4), a.S(5), a.D(6), out x, out y);
                        if (ok == 1) { a.SetRef(8, Value.Int(x)); a.SetRef(9, Value.Int(y)); }
                        else { a.SetRef(8, Value.Int(-1)); a.SetRef(9, Value.Int(-1)); }
                        return Value.Int(ok);
                    }
                case "查找图片返回多张":
                    {
                        List<Point> pts;
                        int n = Vision.FindImages(a.I(0), a.I(1), a.I(2), a.I(3), a.S(4), a.S(5), a.D(6), out pts);
                        RefArg rx = a.R(8), ry = a.R(9), rn = a.R(10);
                        if (rx != null) FillArray(rx.Slot, pts, delegate(Point p) { return Value.Int(p.X); });
                        if (ry != null) FillArray(ry.Slot, pts, delegate(Point p) { return Value.Int(p.Y); });
                        if (rn != null) FillArray(rn.Slot, pts, delegate(Point p) { return Value.Str(Path.GetFileNameWithoutExtension(a.S(4))); });
                        return Value.Int(n);
                    }

                // ---- 文本识别 ----
                case "文本识别": return DoOcr(a, null, false);
                case "文本查找": return DoOcr(a, a.S(6), a.I(7) != 0);
                case "目标检测": return Value.Int(0);
                case "目标查找": return Value.Int(0);

                // ---- 其它 ----
                case "系统命令":
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + a.S(0));
                            psi.CreateNoWindow = true; psi.UseShellExecute = false;
                            Process.Start(psi);
                            return Value.Int(1);
                        }
                        catch { return Value.Int(0); }
                    }
                case "蜂鸣器": try { Native.Beep((uint)a.I(0), (uint)a.I(1)); } catch { } return Value.Int(1);
                case "随机数": return Value.Int(Rnd(a.I(0), a.I(1)));
                case "加速延迟": AccelOffset = a.I(0); return Value.Int(1);
                case "设置键盘点击延迟": KeyClickDelay = a.I(0); return Value.Int(1);
                case "设置鼠标点击延迟": MouseClickDelay = a.I(0); return Value.Int(1);
                case "设置鼠标偏移": MouseOffsetX = a.I(0); MouseOffsetY = a.I(1); return Value.Int(1);
                case "设置鼠标移动轨迹": TrajectoryOn = a.I(0) != 0; TrajectorySpeed = Math.Max(1, a.I(1)); return Value.Int(1);
                case "多任务加锁":
                    {
                        SemaphoreSlim sem;
                        lock (taskLocks)
                        {
                            if (!taskLocks.TryGetValue(a.S(0), out sem)) { sem = new SemaphoreSlim(1, 1); taskLocks[a.S(0)] = sem; }
                        }
                        heldTaskLocks[sem] = heldTaskLocks.ContainsKey(sem) ? heldTaskLocks[sem] + 1 : 1;
                        return Value.Int(heldTaskLocks[sem]);
                    }
                case "多任务解锁":
                    {
                        SemaphoreSlim sem = null;
                        // 通过编号找回锁
                        foreach (KeyValuePair<SemaphoreSlim, int> kv in heldTaskLocks)
                            if (kv.Value == a.I(0)) { sem = kv.Key; break; }
                        if (sem != null)
                        {
                            sem.Release();
                            heldTaskLocks.Remove(sem);
                        }
                        return Value.Int(1);
                    }
                case "多线程锁":
                    {
                        object lk;
                        lock (threadLocks)
                        {
                            if (!threadLocks.TryGetValue(a.S(1), out lk)) { lk = new object(); threadLocks[a.S(1)] = lk; }
                        }
                        return Value.Int(1);
                    }
                case "启动线程": return StartThread(a.S(0));
                case "等待线程": WaitThread(a.I(0)); return Value.Int(1);
                case "输出调试": Log(a.S(0)); return Value.Int(1);
                case "获取鼠标位置":
                    {
                        Native.POINT p = Native.GetMousePos();
                        a.SetRef(0, Value.Int(p.X)); a.SetRef(1, Value.Int(p.Y));
                        return Value.Int(1);
                    }

                // ---- DLL 调用（让目录中的全部 DLL 可用）----
                case "加载DLL": return Value.Int(DllHost.ScriptLoad(a.S(0), Log));
                case "卸载DLL": return Value.Int(DllHost.ScriptUnload(a.I(0)) ? 1 : 0);
                case "调用DLL":
                case "调用DLL函数":
                    {
                        bool stdcall = name == "调用DLL";
                        if (a.Count < 2)
                        {
                            Log("调用DLL用法: 返回值 = 调用DLL(\"文件.dll\", \"函数名\", 参数1, 参数2...)");
                            return Value.Int(0);
                        }
                        long idOrZero = 0; string dllName = null;
                        if (a.V(0).T == 2) dllName = a.S(0); else idOrZero = a.V(0).AsInt();
                        List<object> cargs = new List<object>();
                        for (int i = 2; i < a.Count; i++)
                        {
                            Value v = a.V(i);
                            cargs.Add(v.T == 2 ? (object)v.AsStr() : (object)(long)v.AsInt());
                        }
                        return Value.Int(DllHost.ScriptCall(idOrZero, dllName, a.S(1), cargs, stdcall, Log));
                    }
                case "获取DLL导出":
                    {
                        List<string> names = DllHost.ScriptExports(a.S(0), 4000, Log);
                        RefArg arr = a.R(1);
                        if (arr != null)
                        {
                            arr.Slot.Arr = new List<Value>();
                            foreach (string s in names) arr.Slot.Arr.Add(Value.Str(s));
                        }
                        return Value.Int(names.Count);
                    }
            }
            throw new Exception("未知命令: " + name);
        }

        void FillArray<T>(Slot sl, List<T> items, Func<T, Value> f)
        {
            sl.Arr = new List<Value>();
            foreach (T it in items) sl.Arr.Add(f(it));
        }

        IntPtr Hwnd(CallArgs a) { IntPtr h = (IntPtr)a.I(0); return h == IntPtr.Zero ? BoundWindow : h; }
        int CurrentW(IntPtr h) { Native.RECT r; if (Native.GetWindowRect(h, out r)) return r.Right - r.Left; return 200; }
        int CurrentH(IntPtr h) { Native.RECT r; if (Native.GetWindowRect(h, out r)) return r.Bottom - r.Top; return 200; }

        int Rnd(int lo, int hi)
        {
            if (hi < lo) { int t = lo; lo = hi; hi = t; }
            return rnd.Next(lo, hi + 1);
        }
        double Deg(double d) { return d * Math.PI / 180.0; }
        double Rad(double r) { return r * 180.0 / Math.PI; }
        string StripExe(string n) { return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n; }

        // ================= 延迟 =================
        void Delay(CallArgs a) { Delay(a.I(0)); }
        public void Delay(long ms)
        {
            long real = ms - AccelOffset;
            if (real < 0) real = 0;
            Sleep(real);
        }

        void MsgBox(CallArgs a)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 1; i < a.Count; i++) sb.Append(a.S(i));
            string title = a.S(0);
            string text = sb.Length == 0 ? title : sb.ToString();
            MessageBox.Show(text, sb.Length == 0 ? "提示" : title);
        }

        // ================= 鼠标 =================
        // 后台模式：向绑定窗口发送窗口消息（坐标为窗口客户区坐标）
        public bool BackendMouse, BackendKey, BackendCap;

        void BackendMouseMsg(uint btn, int clientX, int clientY, bool down)
        {
            if (BoundWindow == IntPtr.Zero) return;
            uint msg;
            if (btn == Native.MOUSEEVENTF_LEFTDOWN) msg = down ? 0x0201u : 0x0202u;
            else if (btn == Native.MOUSEEVENTF_RIGHTDOWN) msg = down ? 0x0204u : 0x0205u;
            else msg = down ? 0x0207u : 0x0208u;
            IntPtr lp = (IntPtr)(((clientY & 0xFFFF) << 16) | (clientX & 0xFFFF));
            Native.PostMessage(BoundWindow, msg, (IntPtr)(down ? 1 : 0), lp);
        }

        void BackendMouseMove(int clientX, int clientY)
        {
            if (BoundWindow == IntPtr.Zero) return;
            IntPtr lp = (IntPtr)(((clientY & 0xFFFF) << 16) | (clientX & 0xFFFF));
            Native.PostMessage(BoundWindow, 0x0200, IntPtr.Zero, lp);
        }

        void BackendKeyMsg(ushort vk, bool down)
        {
            if (BoundWindow == IntPtr.Zero) return;
            uint msg = down ? 0x0100u : 0x0101u;
            Native.PostMessage(BoundWindow, msg, (IntPtr)vk, (IntPtr)(down ? 1 : 0xC0000001));
        }

        // 屏幕坐标转绑定窗口客户区坐标
        void ToClient(int screenX, int screenY, out int cx, out int cy)
        {
            Native.POINT p = new Native.POINT();
            p.X = screenX; p.Y = screenY;
            Native.ScreenToClient(BoundWindow, ref p);
            cx = p.X; cy = p.Y;
        }

        void MoveWithTrajectory(int x, int y)
        {
            if (BackendMouse && BoundWindow != IntPtr.Zero)
            {
                int cx2, cy2;
                ToClient(x, y, out cx2, out cy2);
                BackendMouseMove(cx2, cy2);
                return;
            }
            Native.POINT cur = Native.GetMousePos();
            int sx = cur.X + MouseOffsetX, sy = cur.Y + MouseOffsetY;
            if (!TrajectoryOn) { Native.MoveMouse(x, y); return; }
            int dist = Math.Max(Math.Abs(x - sx), Math.Abs(y - sy));
            int steps = Math.Max(2, Math.Min(60, dist / 20));
            int perDelay = TrajectorySpeed;
            for (int i = 1; i <= steps; i++)
            {
                CheckStop();
                int nx = sx + (x - sx) * i / steps;
                int ny = sy + (y - sy) * i / steps;
                Native.MoveMouse(nx, ny);
                if (perDelay > 0) Thread.Sleep(perDelay);
            }
            Native.MoveMouse(x, y);
        }

        void DoMouseMove(CallArgs a, bool relative)
        {
            int x = a.I(0) + MouseOffsetX, y = a.I(1) + MouseOffsetY;
            if (relative)
            {
                Native.POINT cur = Native.GetMousePos();
                x = cur.X + a.I(0); y = cur.Y + a.I(1);
            }
            if (a.Count >= 7)
            {
                int cnt = a.I(6), per = a.Count >= 8 ? a.I(7) : 0;
                for (int i = 0; i < Math.Max(1, cnt); i++)
                {
                    CheckStop();
                    MoveWithTrajectory(x, y);
                    if (per > 0) Delay(per);
                }
                return;
            }
            MoveWithTrajectory(x, y);
        }

        void ClickAt(CallArgs a, uint btn, int clicks, int x, int y, bool domove)
        {
            if (BackendMouse && BoundWindow != IntPtr.Zero)
            {
                int cx2, cy2;
                ToClient(x, y, out cx2, out cy2);
                for (int c = 0; c < clicks; c++)
                {
                    CheckStop();
                    BackendMouseMsg(btn, cx2, cy2, true);
                    if (MouseClickDelay > 0) Sleep(MouseClickDelay);
                    BackendMouseMsg(btn, cx2, cy2, false);
                    if (clicks > 1 && c < clicks - 1 && MouseClickDelay > 0) Sleep(MouseClickDelay);
                }
                return;
            }
            if (domove) MoveWithTrajectory(x, y);
            for (int c = 0; c < clicks; c++)
            {
                CheckStop();
                Native.MouseDown(btn);
                if (MouseClickDelay > 0) Sleep(MouseClickDelay);
                Native.MouseUp(btn);
                if (clicks > 1 && c < clicks - 1 && MouseClickDelay > 0) Sleep(MouseClickDelay);
            }
        }

        void DoClick(CallArgs a, uint btn, int clicks)
        {
            int baseCnt = 0;
            if (a.Count >= 2)
            {
                int x = a.I(0), y = a.I(1);
                int rl = 0, rt = 0, rr = 0, rb = 0, cnt = 1, per = 0;
                if (a.Count >= 6) { rl = a.I(2); rt = a.I(3); rr = a.I(4); rb = a.I(5); }
                if (a.Count >= 8) { cnt = a.I(6); per = a.I(7); }
                for (int i = 0; i < Math.Max(1, cnt); i++)
                {
                    CheckStop();
                    int fx = x, fy = y;
                    if (!(x == -1 && y == -1))
                    {
                        if (rl > 0 || rr > 0) fx = x + Rnd(-rl, rr);
                        if (rt > 0 || rb > 0) fy = y + Rnd(-rt, rb);
                        ClickAt(a, btn, clicks, fx + MouseOffsetX, fy + MouseOffsetY, true);
                    }
                    else ClickAt(a, btn, clicks, 0, 0, false);
                    if (per > 0) Delay(per);
                }
                baseCnt = 1;
            }
            if (baseCnt == 0) ClickAt(a, btn, clicks, 0, 0, false);
        }

        void DoPress(CallArgs a, uint btn, bool down)
        {
            if (a.Count >= 2 && !(a.I(0) == -1 && a.I(1) == -1))
            {
                MoveWithTrajectory(a.I(0) + MouseOffsetX, a.I(1) + MouseOffsetY);
            }
            if (BackendMouse && BoundWindow != IntPtr.Zero)
            {
                Native.POINT cur = Native.GetMousePos();
                int cx2, cy2;
                ToClient(cur.X, cur.Y, out cx2, out cy2);
                BackendMouseMsg(btn, cx2, cy2, down);
                return;
            }
            if (down) Native.MouseDown(btn); else Native.MouseUp(btn);
        }

        void DoWheel(CallArgs a, int dir)
        {
            if (a.Count >= 2 && !(a.I(0) == -1 && a.I(1) == -1))
                MoveWithTrajectory(a.I(0) + MouseOffsetX, a.I(1) + MouseOffsetY);
            if (BackendMouse && BoundWindow != IntPtr.Zero)
            {
                Native.POINT cur = Native.GetMousePos();
                int cx2, cy2;
                ToClient(cur.X, cur.Y, out cx2, out cy2);
                IntPtr lp = (IntPtr)(((cy2 & 0xFFFF) << 16) | (cx2 & 0xFFFF));
                Native.PostMessage(BoundWindow, 0x020A, (IntPtr)((dir * 120) << 16), lp);
                return;
            }
            Native.MouseWheel(dir * 120);
        }

        // ================= 键盘 =================
        void DoKey(CallArgs a, bool byVk, int mode) // mode 0=单击 1=按下 2=抬起
        {
            ushort vk = byVk ? (ushort)a.I(0) : Native.KeyToVk(a.S(0));
            if (vk == 0) { Log("未知按键: " + (byVk ? a.I(0).ToString() : a.S(0))); return; }
            int cnt = 1, per = 0;
            if (mode == 0)
            {
                if (a.Count >= 2) cnt = a.I(1);
                if (a.Count >= 3) per = a.I(2);
            }
            bool backend = BackendKey && BoundWindow != IntPtr.Zero;
            for (int i = 0; i < Math.Max(1, cnt); i++)
            {
                CheckStop();
                if (backend)
                {
                    if (mode == 0)
                    {
                        BackendKeyMsg(vk, true);
                        if (KeyClickDelay > 0) Sleep(KeyClickDelay);
                        BackendKeyMsg(vk, false);
                        if (per > 0 && i < cnt - 1) Delay(per);
                    }
                    else if (mode == 1) BackendKeyMsg(vk, true);
                    else BackendKeyMsg(vk, false);
                    continue;
                }
                if (mode == 0)
                {
                    Native.KeyDown(vk);
                    if (KeyClickDelay > 0) Sleep(KeyClickDelay);
                    Native.KeyUp(vk);
                    if (per > 0 && i < cnt - 1) Delay(per);
                }
                else if (mode == 1) Native.KeyDown(vk);
                else Native.KeyUp(vk);
            }
        }

        Value DoWaitKey(CallArgs a, bool byVk)
        {
            ushort vk = byVk ? (ushort)a.I(0) : Native.KeyToVk(a.S(0));
            long timeout = a.Count >= 2 ? a.I(1) : -1;
            long start = Environment.TickCount;
            while (true)
            {
                CheckStop();
                if (Native.IsKeyDown(vk)) return Value.Int(1);
                if (timeout >= 0 && Environment.TickCount - start >= timeout) return Value.Int(0);
                Thread.Sleep(10);
            }
        }

        Value DoWaitMouse(CallArgs a)
        {
            uint btn = a.I(0) == 1 ? Native.MOUSEEVENTF_LEFTDOWN : a.I(0) == 2 ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_MIDDLEDOWN;
            while (true)
            {
                CheckStop();
                if (Native.IsMouseDown(btn)) return Value.Int(1);
                Thread.Sleep(10);
            }
        }

        // ================= 剪贴板/发送 =================
        string GetClipboard()
        {
            string s = "";
            Thread t = new Thread(delegate() { try { if (Clipboard.ContainsText()) s = Clipboard.GetText(); } catch { } });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(2000);
            return s;
        }
        void SetClipboard(string s)
        {
            Thread t = new Thread(delegate() { try { Clipboard.SetText(s ?? ""); } catch { } });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(2000);
        }
        void SendStringToWindow(string s, int mode)
        {
            if (mode == 0 && BoundWindow != IntPtr.Zero)
            {
                foreach (char c in s)
                {
                    CheckStop();
                    Native.PostMessage(BoundWindow, 0x0102 /*WM_CHAR*/, (IntPtr)c, IntPtr.Zero);
                    Thread.Sleep(10);
                }
                return;
            }
            // 通过 unicode 输入
            foreach (char c in s)
            {
                CheckStop();
                Native.SendUnicodeChar(c);
                Thread.Sleep(10);
            }
        }
        void SendPaste()
        {
            Native.KeyDown(0x11);
            Native.KeyClick(0x56); // V
            Native.KeyUp(0x11);
        }

        Value DoWaitTime(CallArgs a)
        {
            int h = a.I(0), m = a.I(1), s = a.I(2), ms = a.I(3);
            bool strict = a.Count >= 5 && a.I(4) != 0;
            long start = Environment.TickCount;
            bool everPassed = false;
            while (true)
            {
                CheckStop();
                DateTime now = DateTime.Now;
                long target = now.Hour * 3600000L + now.Minute * 60000L + now.Second * 1000L + now.Millisecond;
                long want = h * 3600000L + m * 60000L + s * 1000L + ms;
                bool passed = target >= want;
                if (passed) everPassed = true;
                if (strict)
                {
                    if (everPassed && passed && (target - want) < 100) return Value.Int(1);
                    if (passed && target - want >= 100) { /* 已经过头，等下一天轮回 */ }
                }
                else if (passed) return Value.Int(1);
                if (Environment.TickCount - start > 86400000L) return Value.Int(0);
                Thread.Sleep(50);
            }
        }

        Value DoStartProcess(CallArgs a)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(a.S(0), a.Count >= 2 ? a.S(1) : "");
                bool show = a.Count < 3 || a.I(2) != 0;
                psi.CreateNoWindow = !show;
                psi.UseShellExecute = show;
                if (!show && a.S(0).ToLower().Contains("cmd")) psi.UseShellExecute = false;
                bool wait = a.Count >= 4 && a.I(3) != 0;
                Process p = Process.Start(psi);
                if (wait && p != null) { while (!p.HasExited) { CheckStop(); Thread.Sleep(100); } }
                return Value.Int(1);
            }
            catch (Exception ex) { Log("启动进程失败: " + ex.Message); return Value.Int(0); }
        }

        // ================= OCR =================
        Value DoOcr(CallArgs a, string findText, bool contains)
        {
            int l = a.I(0), t = a.I(1), r = a.I(2), b = a.I(3);
            double txtConf = a.D(4), boxConf = a.D(5);
            RefArg rl, rt, rr, rb, rt2;
            string outImg;
            if (findText == null)
            {   // 文本识别(l,t,r,b,txtConf,boxConf, l,t,r,b,text, "输出图片")
                rl = a.R(6); rt = a.R(7); rr = a.R(8); rb = a.R(9); rt2 = a.R(10);
                outImg = a.Count >= 12 ? a.S(11) : "";
            }
            else
            {   // 文本查找(l,t,r,b,txtConf,boxConf, "文本", 含匹配, 多条, l,t,r,b,text, "输出图片")
                rl = a.R(9); rt = a.R(10); rr = a.R(11); rb = a.R(12); rt2 = a.R(13);
                outImg = a.Count >= 15 ? a.S(14) : "";
            }
            List<Vision.OcrItem> items;
            int n = Vision.OcrRegion(l, t, r, b, Math.Max(0.05, txtConf), out items, outImg);
            if (n > 0 && findText != null)
            {
                List<Vision.OcrItem> filtered = new List<Vision.OcrItem>();
                foreach (Vision.OcrItem it in items)
                {
                    if (contains ? it.Text.IndexOf(findText, StringComparison.Ordinal) >= 0 : it.Text == findText) filtered.Add(it);
                }
                items = filtered;
                n = items.Count;
            }
            if (rl != null) FillArray(rl.Slot, items, delegate(Vision.OcrItem it) { return Value.Int(it.Left); });
            if (rt != null) FillArray(rt.Slot, items, delegate(Vision.OcrItem it) { return Value.Int(it.Top); });
            if (rr != null) FillArray(rr.Slot, items, delegate(Vision.OcrItem it) { return Value.Int(it.Right); });
            if (rb != null) FillArray(rb.Slot, items, delegate(Vision.OcrItem it) { return Value.Int(it.Bottom); });
            if (rt2 != null) FillArray(rt2.Slot, items, delegate(Vision.OcrItem it) { return Value.Str(it.Text); });
            return Value.Int(n);
        }

        // ================= 线程 =================
        Value StartThread(string funcName)
        {
            Interp interp = CurrentInterp;
            if (interp == null || !interp.Sc.Funcs.ContainsKey(funcName))
            {
                Log("启动线程失败: 未找到子任务 " + funcName);
                return Value.Int(0);
            }
            long tid = nextTid++;
            Thread th = new Thread(delegate()
            {
                try
                {
                    Interp sub = new Interp(this) { Sc = interp.Sc };
                    sub.Globals = interp.Globals;
                    CurrentInterp = sub;
                    Stmt f = interp.Sc.Funcs[funcName];
                    sub.RunBlock(f.Bodies[0]);
                }
                catch (StopSignal) { }
                catch (ReturnSignal) { }
                catch (Exception ex) { Log("线程" + tid + "异常: " + ex.Message); }
                finally { CurrentInterp = interp; }
            });
            th.SetApartmentState(ApartmentState.STA);
            lock (threads) threads[tid] = th;
            th.IsBackground = true;
            th.Start();
            return Value.Int(tid);
        }
        void WaitThread(long tid)
        {
            Thread th;
            lock (threads) { if (!threads.TryGetValue(tid, out th)) return; }
            while (th.IsAlive)
            {
                CheckStop();
                th.Join(100);
            }
            lock (threads) threads.Remove(tid);
        }

        [ThreadStatic] public static Interp CurrentInterp;

        // ================= 脚本执行入口 =================
        public void RunCode(string code)
        {
            Interp ip = CurrentInterp;
            if (ip == null) { ip = new Interp(this) { Sc = new Script() }; CurrentInterp = ip; }
            RunCodeInto(ip, code);
        }
        public void RunCodeInto(Interp interp, string code)
        {
            Script sub = Interp.Compile(code);
            foreach (KeyValuePair<string, Stmt> kv in sub.Funcs) interp.Sc.Funcs[kv.Key] = kv.Value;
            interp.Run(sub.Body);
        }
        public Value EvalExpr(Interp ip, string expr)
        {
            Lexer lx = new Lexer(expr);
            Parser ps = new Parser(lx.Lex());
            ExprNode e = ps.ParseExpr();
            return ip.Eval(e);
        }
    }
}
