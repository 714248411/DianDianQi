using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BulingOffline
{
    // 智能键盘鼠标录制器：录制完直接转为多功能脚本代码
    // - 鼠标：移动/左中右键按下抬起/单击/双击/拖动/滚轮
    // - 键盘：按键按下抬起/自动连击/修饰键组合（CTRL/SHIFT/ALT/WIN）
    // - 自动插入 延迟(ms)，可选项：记录鼠标移动、记录时间延迟、合并双击
    public class RecorderForm : Form
    {
        class Ev { public int Kind; public int X, Y, Btn, Vk, Delta; public DateTime T; }
        const int K_MOVE = 0, K_DOWN = 1, K_UP = 2, K_WHEEL = 3, K_KDOWN = 4, K_KUP = 5;

        readonly List<Ev> evs = new List<Ev>();
        RichTextBox box;
        DarkButton startBtn, stopBtn;
        CheckBox chkMove, chkDelay, chkMerge;
        Label statusLabel;
        bool recording;
        IntPtr mouseHook = IntPtr.Zero, keyHook = IntPtr.Zero;
        Native.LowLevelHookProc mouseProc, keyProc;
        GCHandle mh, kh;
        DateTime lastPreview = DateTime.MinValue;

        public string Generated = "";
        public Action<string> OnInsertNode;   // 生成自定义节点
        public Action<string> OnOpenCode;     // 转为多功能代码

        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        public RecorderForm()
        {
            Text = "键盘鼠标录制器 - 录制完可直接转为多功能代码";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 460);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;
            TopMost = true;

            startBtn = new DarkButton("开始录制", true, 90, 28); startBtn.Location = new Point(14, 12);
            startBtn.Click += delegate { StartRec(); };
            stopBtn = new DarkButton("停止录制", false, 90, 28); stopBtn.Location = new Point(112, 12);
            stopBtn.Enabled = false;
            stopBtn.Click += delegate { StopRec(); };
            DarkButton clearBtn = new DarkButton("清空", false, 62, 28); clearBtn.Location = new Point(210, 12);
            clearBtn.Click += delegate { evs.Clear(); RefreshBox(); };
            DarkButton codeBtn = new DarkButton("转为多功能代码", true, 130, 28); codeBtn.Location = new Point(280, 12);
            codeBtn.Click += delegate
            {
                StopRec();
                Generated = Generate();
                if (Generated.Trim() == "") { MessageBox.Show("没有录制到内容"); return; }
                if (OnOpenCode != null) { OnOpenCode(Generated); Close(); }
                else MessageBox.Show(Generated);
            };
            DarkButton insBtn = new DarkButton("生成自定义节点", false, 122, 28); insBtn.Location = new Point(418, 12);
            insBtn.Click += delegate
            {
                StopRec();
                Generated = Generate();
                if (Generated.Trim() == "") { MessageBox.Show("没有录制到内容"); return; }
                if (OnInsertNode != null) { OnInsertNode(Generated); Close(); }
            };
            DarkButton copyBtn = new DarkButton("复制代码", false, 86, 28); copyBtn.Location = new Point(548, 12);
            copyBtn.Click += delegate { StopRec(); try { Clipboard.SetText(Generate()); } catch { } };
            Controls.Add(startBtn); Controls.Add(stopBtn); Controls.Add(clearBtn);
            Controls.Add(codeBtn); Controls.Add(insBtn); Controls.Add(copyBtn);

            chkMove = MakeCheck("记录鼠标移动", true, 14, 50);
            chkDelay = MakeCheck("记录时间延迟", true, 120, 50);
            chkMerge = MakeCheck("合并双击", true, 240, 50);
            statusLabel = new Label();
            statusLabel.Text = "未在录制";
            statusLabel.ForeColor = Theme.TextDim; statusLabel.AutoSize = true; statusLabel.Location = new Point(336, 53);
            Controls.Add(statusLabel);

            box = new RichTextBox();
            box.Location = new Point(14, 80);
            box.Size = new Size(612, 350);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            box.BackColor = Theme.Bg; box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = Theme.FontCode;
            box.ReadOnly = true;
            box.WordWrap = false;
            Controls.Add(box);

            FormClosing += delegate { StopRec(); };
        }

        CheckBox MakeCheck(string text, bool chk, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text; c.Checked = chk;
            c.ForeColor = Theme.Text; c.BackColor = Theme.Panel;
            c.AutoSize = true; c.Location = new Point(x, y);
            Controls.Add(c);
            return c;
        }

        // ================= 钩子 =================
        void StartRec()
        {
            if (recording) return;
            recording = true;
            evs.Clear();
            mouseProc = MouseHookProc; keyProc = KeyHookProc;
            mh = GCHandle.Alloc(mouseProc);
            kh = GCHandle.Alloc(keyProc);
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, GetModuleHandle(null), 0);
            keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero)
                MessageBox.Show("键盘/鼠标钩子安装失败，录制可能不完整");
            startBtn.Enabled = false; stopBtn.Enabled = true;
            statusLabel.Text = "● 录制中...（本窗口内的操作不会被录制）";
            statusLabel.ForeColor = Theme.Pink;
        }

        void StopRec()
        {
            if (!recording) return;
            recording = false;
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
            mouseHook = keyHook = IntPtr.Zero;
            if (mh.IsAllocated) mh.Free();
            if (kh.IsAllocated) kh.Free();
            startBtn.Enabled = true; stopBtn.Enabled = false;
            statusLabel.Text = "已停止，共 " + evs.Count + " 条事件";
            statusLabel.ForeColor = Theme.TextDim;
            RefreshBox();
        }

        bool InSelf(int x, int y)
        {
            Rectangle r = Bounds;
            r.Inflate(4, 4);
            return r.Contains(x, y);
        }

        void AddEv(Ev e)
        {
            lock (evs) evs.Add(e);
            MaybePreview();
        }
        void MaybePreview()
        {
            if ((DateTime.Now - lastPreview).TotalMilliseconds < 300) return;
            lastPreview = DateTime.Now;
            RefreshBox();
        }

        IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && recording)
            {
                int msg = wParam.ToInt32();
                Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                // 跳过注入事件与录制器自身窗口内的操作
                if ((info.flags & 0x10) == 0 && !InSelf(info.pt.X, info.pt.Y))
                {
                    Ev e = new Ev(); e.T = DateTime.Now; e.X = info.pt.X; e.Y = info.pt.Y;
                    switch (msg)
                    {
                        case 0x0200: e.Kind = K_MOVE; AddEv(e); break;
                        case 0x0201: e.Kind = K_DOWN; e.Btn = 1; AddEv(e); break;
                        case 0x0202: e.Kind = K_UP; e.Btn = 1; AddEv(e); break;
                        case 0x0204: e.Kind = K_DOWN; e.Btn = 2; AddEv(e); break;
                        case 0x0205: e.Kind = K_UP; e.Btn = 2; AddEv(e); break;
                        case 0x0207: e.Kind = K_DOWN; e.Btn = 3; AddEv(e); break;
                        case 0x0208: e.Kind = K_UP; e.Btn = 3; AddEv(e); break;
                        case 0x020A:
                            e.Kind = K_WHEEL;
                            e.Delta = (short)((info.mouseData >> 16) & 0xFFFF);
                            AddEv(e);
                            break;
                    }
                }
            }
            return Native.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        IntPtr KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && recording)
            {
                int msg = wParam.ToInt32();
                if (msg == 0x0100 || msg == 0x0101 || msg == 0x0104 || msg == 0x0105)
                {
                    Native.KBDLLHOOKSTRUCT info = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    bool down = msg == 0x0100 || msg == 0x0104;
                    // 跳过注入事件；录制器窗口为前台时跳过（避免录到自身按键）
                    if ((info.flags & 0x10) == 0 && GetForegroundWindow() != Handle)
                    {
                        Ev e = new Ev();
                        e.T = DateTime.Now; e.Vk = (int)info.vkCode;
                        e.Kind = down ? K_KDOWN : K_KUP;
                        AddEv(e);
                    }
                }
            }
            return Native.CallNextHookEx(keyHook, nCode, wParam, lParam);
        }

        // ================= 转换为多功能代码 =================
        static bool IsModifier(int vk)
        {
            return vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x5B || vk == 0x5C || vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3;
        }

        static string VkName(int vk)
        {
            switch (vk)
            {
                case 0x08: return "BACK";
                case 0x09: return "TAB";
                case 0x0D: return "回车";
                case 0x10: case 0xA0: case 0xA1: return "SHIFT";
                case 0x11: case 0xA2: case 0xA3: return "CTRL";
                case 0x12: return "ALT";
                case 0x14: return "CAPS";
                case 0x1B: return "ESC";
                case 0x20: return "SPACE";
                case 0x21: return "PAGEUP";
                case 0x22: return "PAGEDOWN";
                case 0x23: return "END";
                case 0x24: return "HOME";
                case 0x25: return "左";
                case 0x26: return "上";
                case 0x27: return "右";
                case 0x28: return "下";
                case 0x2D: return "INSERT";
                case 0x2E: return "DELETE";
                case 0x5B: case 0x5C: return "WIN";
                case 0x30: return "0"; case 0x31: return "1"; case 0x32: return "2"; case 0x33: return "3"; case 0x34: return "4";
                case 0x35: return "5"; case 0x36: return "6"; case 0x37: return "7"; case 0x38: return "8"; case 0x39: return "9";
            }
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "NUM" + (vk - 0x60);
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x70 + 1);
            switch (vk)
            {
                case 0xBA: return ";"; case 0xBB: return "="; case 0xBC: return ",";
                case 0xBD: return "-"; case 0xBE: return "."; case 0xBF: return "/";
                case 0xDB: return "["; case 0xDC: return "\\"; case 0xDD: return "]";
                case 0xDE: return "'"; case 0xC0: return "`";
            }
            return null;
        }

        static string KeyLine(string verb, int vk)
        {
            string name = VkName(vk);
            if (name == null) return "键值" + verb + "(" + vk + ")";
            return "键盘" + verb + "(" + name + ")";
        }
        static string KeyClick(int vk, int count)
        {
            string baseLine = KeyLine("单击", vk);
            return count > 1 ? baseLine.Replace(")", ", " + count + ")") : baseLine;
        }
        static string BtnName(int b) { return b == 1 ? "左键" : b == 2 ? "右键" : "中键"; }

        // 事件流 → 脚本代码（单击/双击/拖动/滚轮/长按/连击/修饰键组合）
        public string Generate()
        {
            List<string> lines = new List<string>();
            List<Ev> list;
            lock (evs) list = new List<Ev>(evs);
            if (list.Count == 0) return "";

            DateTime lastEmit = DateTime.MinValue;
            bool anyEmit = false;

            // 鼠标按下状态（拖动检测）
            int downBtn = 0; Point downPt = Point.Empty; DateTime downT = DateTime.MinValue; bool dragMoved = false;
            List<Point> dragPath = new List<Point>();
            // 双击检测
            int lastClickBtn = 0; Point lastClickPt = Point.Empty; DateTime lastClickT = DateTime.MinValue; int lastClickLine = -1;
            // 键盘连击
            int pendVk = 0; int pendCount = 0; DateTime pendT = DateTime.MinValue;
            // 上一次已输出的鼠标位置（移动去重）
            Point lastPt = Point.Empty; bool hasPt = false;

            Action<string, DateTime> emit = delegate(string line, DateTime t)
            {
                if (chkDelay.Checked && anyEmit)
                {
                    int gap = (int)(t - lastEmit).TotalMilliseconds;
                    if (gap >= 100 && gap < 600000) lines.Add("延迟(" + gap + ")");
                }
                lines.Add(line);
                lastEmit = t; anyEmit = true;
            };
            Action flushPending = delegate()
            {
                if (pendVk == 0) return;
                emit(KeyClick(pendVk, pendCount), pendT);
                pendVk = 0; pendCount = 0;
            };

            foreach (Ev e in list)
            {
                Point pt = new Point(e.X, e.Y);
                if (e.Kind == K_MOVE)
                {
                    if (downBtn != 0)
                    {
                        if (Math.Abs(e.X - downPt.X) + Math.Abs(e.Y - downPt.Y) >= 8) dragMoved = true;
                        dragPath.Add(pt);
                    }
                    if (chkMove.Checked && (!hasPt || Math.Abs(e.X - lastPt.X) + Math.Abs(e.Y - lastPt.Y) >= 10))
                    {
                        emit("鼠标移动(" + e.X + ", " + e.Y + ")", e.T);
                        lastPt = pt; hasPt = true;
                    }
                    continue;
                }
                if (e.Kind == K_WHEEL)
                {
                    flushPending();
                    int notches = Math.Max(1, Math.Min(15, Math.Abs(e.Delta) / 120));
                    for (int i = 0; i < notches; i++)
                        emit((e.Delta > 0 ? "鼠标向上滚动(" : "鼠标向下滚动(") + e.X + ", " + e.Y + ")", e.T);
                    lastPt = pt; hasPt = true;
                    lastClickBtn = 0;
                    continue;
                }
                if (e.Kind == K_DOWN && e.Btn > 0)
                {
                    flushPending();
                    if (downBtn != 0)
                    {
                        // 上一次按下未配对抬起（如录制中途开始）：按一次完整点击收尾
                        string bn0 = BtnName(downBtn);
                        emit("鼠标移动(" + downPt.X + ", " + downPt.Y + ")", downT);
                        emit(bn0 + "按下()", downT);
                        emit(bn0 + "抬起()", e.T);
                        lastPt = downPt; hasPt = true;
                    }
                    downBtn = e.Btn; downPt = pt; downT = e.T; dragMoved = false; dragPath.Clear();
                    continue;
                }
                if (e.Kind == K_UP && e.Btn > 0)
                {
                    if (downBtn != e.Btn) continue;
                    flushPending();
                    int dt = (int)(e.T - downT).TotalMilliseconds;
                    bool moved = dragMoved || Math.Abs(e.X - downPt.X) + Math.Abs(e.Y - downPt.Y) > 10;
                    string bn = BtnName(downBtn);
                    if (!moved && dt < 400)
                    {
                        // 单击 / 双击
                        bool dbl = chkMerge.Checked && lastClickBtn == downBtn &&
                            (e.T - lastClickT).TotalMilliseconds < 400 &&
                            Math.Abs(e.X - lastClickPt.X) <= 6 && Math.Abs(e.Y - lastClickPt.Y) <= 6;
                        if (dbl && lastClickLine >= 0 && lastClickLine < lines.Count &&
                            lines[lastClickLine].StartsWith("鼠标" + BtnName(lastClickBtn) + "单击("))
                            lines.RemoveAt(lastClickLine);
                        emit("鼠标" + bn + (dbl ? "双击" : "单击") + "(" + e.X + ", " + e.Y + ")", e.T);
                        if (!dbl) { lastClickBtn = downBtn; lastClickPt = pt; lastClickT = e.T; lastClickLine = lines.Count - 1; }
                        else lastClickBtn = 0;
                    }
                    else
                    {
                        // 拖动 / 长按：移动到起点 → 按下 → (轨迹) → 抬起
                        emit("鼠标移动(" + downPt.X + ", " + downPt.Y + ")", downT);
                        emit(bn + "按下()", downT);
                        lastPt = downPt; hasPt = true;
                        if (chkMove.Checked && dragPath.Count > 0)
                        {
                            Point lp = downPt;
                            foreach (Point p in dragPath)
                            {
                                if (Math.Abs(p.X - lp.X) + Math.Abs(p.Y - lp.Y) >= 25)
                                {
                                    emit("鼠标移动(" + p.X + ", " + p.Y + ")", e.T);
                                    lp = p;
                                }
                            }
                        }
                        emit("鼠标移动(" + e.X + ", " + e.Y + ")", e.T);
                        emit(bn + "抬起()", e.T);
                        lastPt = pt; hasPt = true;
                        lastClickBtn = 0;
                    }
                    downBtn = 0; dragPath.Clear(); dragMoved = false;
                    continue;
                }
                if (e.Kind == K_KDOWN)
                {
                    if (IsModifier(e.Vk))
                    {
                        flushPending();
                        emit(KeyLine("按下", e.Vk), e.T);
                    }
                    else if (pendVk == e.Vk) pendCount++;   // 按住自动连击
                    else { flushPending(); pendVk = e.Vk; pendCount = 1; pendT = e.T; }
                    continue;
                }
                if (e.Kind == K_KUP)
                {
                    if (IsModifier(e.Vk))
                    {
                        flushPending();
                        emit(KeyLine("抬起", e.Vk), e.T);
                    }
                    else if (pendVk == e.Vk)
                    {
                        int dt = (int)(e.T - pendT).TotalMilliseconds;
                        if (dt < 600) emit(KeyClick(pendVk, pendCount), e.T);
                        else { emit(KeyLine("按下", pendVk), pendT); emit(KeyLine("抬起", pendVk), e.T); }
                        pendVk = 0; pendCount = 0;
                    }
                    else flushPending();
                    continue;
                }
            }
            flushPending();
            if (downBtn != 0)
            {
                // 录制在按住状态结束：补抬起，避免脚本按下后一直不抬起
                emit(BtnName(downBtn) + "抬起()", DateTime.Now);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 由键盘鼠标录制器生成，可在多功能中直接修改执行");
            foreach (string l in lines) sb.AppendLine(l);
            return sb.ToString();
        }

        void RefreshBox()
        {
            string code = Generate();
            box.Text = code == "" ? "（尚未录制到操作，点击\"开始录制\"后到其它窗口操作）" : code;
            box.SelectionStart = box.TextLength;
            box.ScrollToCaret();
        }
    }
}
