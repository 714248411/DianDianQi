using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace BulingOffline
{
    public class TaskDoc
    {
        public string Name;
        public FlowTask Flow;
    }

    public class MainForm : Form
    {
        // 视图
        Panel beginnerView, editView;
        Panel right, tabPages, tabRow;
        Dictionary<string, FNode> nodeCodeBinding = new Dictionary<string, FNode>();   // 多功能文档名 → 自定义节点
        ComboBox edImgMode, edMouseMode, edKeyMode;
        CodeView codeView;
        Panel activeView;

        // 标题栏
        Panel titleBar;
        DarkButton btnSwitchView, btnMulti, btnSettings, btnTop, btnDonate, btnAbout, btnUpdates, btnMin, btnMax, btnClose;
        bool topMost;

        // 编辑视图
        TabStrip taskTabs;
        List<TaskDoc> tasks = new List<TaskDoc>();
        FlowCanvas canvas;
        Panel canvasHost;
        DarkButton execBtn, debugBtn, stopBtn, zoomIn, zoomOut, zoomFit;
        Label zoomLabel, countLabel;
        Panel flowRunPanel;

        // 初级视图
        TextBox taskNameBox, repeatBox, hwndBox, wtitleBox, wclassBox, wpidBox;
        DarkButton begExec, begDebug, begStop;
        Label begCount;
        ComboBox cbBindMode, cbImgMode, cbMouseMode, cbKeyMode, cbCapMode;
        string begMode = "极速模式";
        MiniFlowPreview miniPrev;      // 任务流程小预览（联动编辑/多功能）
        Label codeStat;                // 当前任务多功能代码统计

        // 初级视图 DLL 页
        ListBox dllList;
        Label dllStat;
        List<DllHost.DllInfo> dllInfos = new List<DllHost.DllInfo>();

        // 输出
        Panel outputPanel;
        RichTextBox outputBox;
        DarkButton outputToggle;

        // 运行
        Runtime rt;
        Thread runThread;
        bool running;
        string runningFrom = "";

        // 热键
        const int WM_HOTKEY = 0x0312;
        int hotStart = 0x46 + 0x70 - 4; // F9=0x78
        int hotStop = 0x79;             // F10
        const uint MOD_NONE = 0x0000;
        string hotStartName = "F9", hotStopName = "F10";

        public MainForm()
        {
            Text = "言逸点点器";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(60, 40);
            ClientSize = new Size(1060, 660);
            BackColor = Theme.Bg;
            Font = Theme.FontUI;
            KeyPreview = true;
            DoubleBuffered = true;

            rt = new Runtime();
            rt.LogFn = Log;
            rt.ProgressFn = delegate(int n)
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        begCount.Text = "当前次数: " + n;
                        countLabel.Text = "当前次数: " + n;
                    });
                }
                catch { }
            };

            BuildTitleBar();
            BuildOutput();
            BuildBeginnerView();
            BuildEditView();
            BuildCodeView();

            // 多功能联动：代码变化同步到任务 / 自定义节点
            codeView.OnCodeChanged = OnCodeChanged;
            foreach (TaskDoc d in tasks) codeView.OpenOrSwitch(d.Name, d.Flow.Code);

            Controls.Add(codeView);
            Controls.Add(editView);
            Controls.Add(beginnerView);
            codeView.Visible = editView.Visible = false;

            outputPanel.SendToBack();
            SwitchView(beginnerView, "初级视图");

            LoadSettings();
            RegisterHotkeys();
            System.Windows.Forms.Timer shotTimer = new System.Windows.Forms.Timer();
            shotTimer.Interval = 700;
            shotTimer.Tick += delegate(object s3, EventArgs e3)
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string addreq = System.IO.Path.Combine(baseDir, "addnode.req");
                    if (System.IO.File.Exists(addreq))
                    {
                        System.IO.File.Delete(addreq);
                        SwitchView(editView, "切换视图");
                        SelectTask(tasks[0].Name);
                        canvas.DebugAddNodes();
                    }
                    string req = System.IO.Path.Combine(baseDir, "shot.req");
                    if (System.IO.File.Exists(req))
                    {
                        System.IO.File.Delete(req);
                        using (System.Drawing.Bitmap bmp2 = new System.Drawing.Bitmap(ClientSize.Width, ClientSize.Height))
                        {
                            DrawToBitmap(bmp2, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                            bmp2.Save(System.IO.Path.Combine(baseDir, "bl_appself.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                catch { }
            };
            shotTimer.Start();

            FormClosing += delegate
            {
                rt.StopFlag = true;
                UnregisterHotkeys();
                SaveSettings();
            };
        }

        // ================= 标题栏 =================
        void BuildTitleBar()
        {
            titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 34;
            titleBar.BackColor = Theme.Bg;
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "言逸点点器", Theme.FontTitle, new Point(30, 8), Theme.Text);
            };
            titleBar.MouseDown += DragWindow;
            titleBar.MouseDoubleClick += delegate { ToggleMax(); };
            Controls.Add(titleBar);
            titleBar.BringToFront();

            btnSwitchView = new DarkButton("切换编辑", false, 76, 26); btnSwitchView.Location = new Point(430, 4);
            btnSwitchView.Click += delegate { TogglePrimaryView(); };
            btnMulti = new DarkButton("多功能", false, 62, 26); btnMulti.Location = new Point(510, 4);
            btnMulti.Click += delegate { ShowTaskCode(); };   // 联动：直接打开当前任务的多功能代码
            btnSettings = new DarkButton("设置", false, 52, 26); btnSettings.Location = new Point(576, 4);
            btnSettings.Click += delegate { ShowSettings(); };
            btnTop = new DarkButton("置顶", false, 46, 26); btnTop.Location = new Point(632, 4);
            btnTop.Click += delegate { topMost = !topMost; TopMost = topMost; Invalidate(); };

            btnDonate = new DarkButton("捐赠", false, 52, 26); btnDonate.Location = new Point(682, 4);
            btnDonate.Click += delegate { using (DonationForm f = new DonationForm()) f.ShowDialog(this); };
            btnAbout = new DarkButton("关于", false, 52, 26); btnAbout.Location = new Point(738, 4);
            btnAbout.Click += delegate { using (AboutForm f = new AboutForm()) f.ShowDialog(this); };
            btnUpdates = new DarkButton("更新说明", false, 72, 26); btnUpdates.Location = new Point(794, 4);
            btnUpdates.Click += delegate { using (UpdateNotesForm f = new UpdateNotesForm()) f.ShowDialog(this); };

            btnMin = new DarkButton("—", false, 34, 26); btnMin.Location = new Point(ClientSize.Width - 130, 4);
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            btnMax = new DarkButton("□", false, 34, 26); btnMax.Location = new Point(ClientSize.Width - 92, 4);
            btnMax.Click += delegate { ToggleMax(); };
            btnClose = new DarkButton("×", false, 34, 26); btnClose.Location = new Point(ClientSize.Width - 54, 4);
            btnClose.Click += delegate { Close(); };
            titleBar.Controls.Add(btnSwitchView);
            titleBar.Controls.Add(btnMulti);
            titleBar.Controls.Add(btnSettings);
            titleBar.Controls.Add(btnTop);
            titleBar.Controls.Add(btnDonate);
            titleBar.Controls.Add(btnAbout);
            titleBar.Controls.Add(btnUpdates);
            titleBar.Controls.Add(btnMin);
            titleBar.Controls.Add(btnMax);
            titleBar.Controls.Add(btnClose);
        }
        void DragWindow(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && WindowState == FormWindowState.Normal)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0);
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        void ToggleMax()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else WindowState = FormWindowState.Maximized;
            LayoutTitleButtons();
        }
        void LayoutTitleButtons()
        {
            if (btnMin == null || btnMax == null || btnClose == null) return;
            btnMin.Location = new Point(ClientSize.Width - 130, 4);
            btnMax.Location = new Point(ClientSize.Width - 92, 4);
            btnClose.Location = new Point(ClientSize.Width - 54, 4);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTitleButtons();
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Pink))
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // 允许无边框窗口调整大小
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) HotkeyRun();
                if (id == 2) rt.StopFlag = true;
                return;
            }
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                int x = (m.LParam.ToInt32() & 0xFFFF), y = (m.LParam.ToInt32() >> 16) & 0xFFFF;
                if (x >= 32768) x -= 65536;
                Point pos = PointToClient(new Point(x, y));
                int edge = 6;
                bool l = pos.X <= edge, r = pos.X >= ClientSize.Width - edge, t = pos.Y <= edge, b = pos.Y >= ClientSize.Height - edge;
                if (l && t) { m.Result = (IntPtr)13; return; }
                if (r && t) { m.Result = (IntPtr)14; return; }
                if (l && b) { m.Result = (IntPtr)16; return; }
                if (r && b) { m.Result = (IntPtr)17; return; }
                if (l) { m.Result = (IntPtr)10; return; }
                if (r) { m.Result = (IntPtr)11; return; }
                if (t) { m.Result = (IntPtr)12; return; }
                if (b) { m.Result = (IntPtr)15; return; }
            }
            base.WndProc(ref m);
        }

        // ================= 视图切换 =================
        void SwitchView(Panel v, string switchLabel)
        {
            beginnerView.Visible = v == beginnerView;
            editView.Visible = v == editView;
            codeView.Visible = v == codeView;
            activeView = v;
            btnSwitchView.Text = switchLabel;
            LayoutOutput();
        }
        void TogglePrimaryView()
        {
            if (activeView == beginnerView) SwitchView(editView, "切换视图");
            else if (activeView == editView) SwitchView(beginnerView, "切换编辑");
            else SwitchView(editView, "切换视图");
        }
        void ShowView(Panel v, string switchLabel) { SwitchView(v, switchLabel); }

        // ================= 输出窗口 =================
        void BuildOutput()
        {
            outputPanel = new Panel();
            outputPanel.Dock = DockStyle.Bottom;
            outputPanel.Height = 130;
            outputPanel.BackColor = Theme.Panel;
            Controls.Add(outputPanel);

            outputToggle = new DarkButton("输出窗口", false, 80, 24);
            outputToggle.Location = new Point(4, 4);
            outputToggle.Click += delegate
            {
                outputPanel.Height = outputPanel.Height > 40 ? 28 : 130;
                LayoutOutput();
            };
            DarkButton clearBtn = new DarkButton("清空", false, 56, 24);
            clearBtn.Location = new Point(90, 4);
            clearBtn.Click += delegate { outputBox.Text = ""; };
            Label links = new Label();
            links.Text = "交流讨论        更多用法";
            links.ForeColor = Theme.Accent;
            links.Location = new Point(820, 8);
            links.AutoSize = true;
            outputPanel.Controls.Add(outputToggle);
            outputPanel.Controls.Add(clearBtn);
            outputPanel.Controls.Add(links);

            outputBox = new RichTextBox();
            outputBox.Location = new Point(4, 32);
            outputBox.Size = new Size(outputPanel.Width - 8, outputPanel.Height - 36);
            outputBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            outputBox.BackColor = Color.FromArgb(12, 12, 18);
            outputBox.ForeColor = Theme.TextDim;
            outputBox.BorderStyle = BorderStyle.FixedSingle;
            outputBox.ReadOnly = true;
            outputBox.Font = Theme.FontSmall;
            outputPanel.Controls.Add(outputBox);
        }
        void LayoutOutput()
        {
            outputBox.Size = new Size(outputPanel.Width - 8, outputPanel.Height - 36);
        }
        void Log(string s)
        {
            if (outputBox.InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { Log(s); }); } catch { }
                return;
            }
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + "\n";
            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.SelectionColor = Theme.TextDim;
            outputBox.AppendText(line);
            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.ScrollToCaret();
        }

        // ================= 初级视图 =================
        void BuildBeginnerView()
        {
            beginnerView = new Panel();
            beginnerView.Dock = DockStyle.Fill;
            beginnerView.BackColor = Theme.Bg;

            // 左侧任务面板
            Panel left = new Panel();
            left.Location = new Point(4, 4);
            left.Size = new Size(420, 300);
            left.BackColor = Theme.Panel;
            left.Paint += delegate(object s, PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "任务", Theme.FontSmall, new Point(8, 2), Theme.TextDim);
            };
            beginnerView.Controls.Add(left);

            Label n1 = new Label(); n1.Text = "任务名称:"; n1.ForeColor = Theme.Text; n1.AutoSize = true; n1.Location = new Point(12, 30);
            taskNameBox = new TextBox(); taskNameBox.Location = new Point(80, 26); taskNameBox.Size = new Size(130, 24);
            Theme.StyleInput(taskNameBox);
            // 任务改名 → 页签/多功能文档同步
            taskNameBox.Leave += delegate { TryRenameTask(); };
            taskNameBox.KeyDown += delegate(object s2, KeyEventArgs e2)
            {
                if (e2.KeyCode == Keys.Enter) { TryRenameTask(); begExec.Focus(); }
            };
            DarkButton nb = new DarkButton("新建", false, 56, 24); nb.Location = new Point(220, 25); nb.Click += delegate { NewTask(); };
            DarkButton sb = new DarkButton("保存", false, 56, 24); sb.Location = new Point(282, 25); sb.Click += delegate { SaveTask(false); };
            begExec = new DarkButton("执行", true, 70, 26); begExec.Location = new Point(348 - 0, 22);
            begDebug = new DarkButton("调试", false, 70, 26); begDebug.Location = new Point(348, 54);
            begStop = new DarkButton("停止", false, 70, 26); begStop.Location = new Point(348, 86); begStop.Visible = false;
            begExec.Click += delegate { RunBeginner(int.Parse(Safe(repeatBox.Text, "1"))); };
            begDebug.Click += delegate { RunBeginner(1); };
            begStop.Click += delegate { rt.StopFlag = true; };

            Label n2 = new Label(); n2.Text = "当前次数: 0"; n2.ForeColor = Theme.TextDim; n2.AutoSize = true; n2.Location = new Point(12, 66);
            begCount = n2;
            Label n3 = new Label(); n3.Text = "重复次数(0为一直重复):"; n3.ForeColor = Theme.Text; n3.AutoSize = true; n3.Location = new Point(12, 96);
            repeatBox = new TextBox(); repeatBox.Location = new Point(160, 92); repeatBox.Size = new Size(60, 24); repeatBox.Text = "1";
            Theme.StyleInput(repeatBox);

            // 任务流程小预览（联动：编辑视图/多功能变化实时刷新，点击进入编辑视图）
            miniPrev = new MiniFlowPreview();
            miniPrev.Location = new Point(10, 126);
            miniPrev.Size = new Size(400, 128);
            miniPrev.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            miniPrev.TaskProvider = delegate { return CurrentTask(); };
            miniPrev.OnOpenEdit = delegate { SwitchView(editView, "切换视图"); };
            DarkButton openCode = new DarkButton("打开多功能代码", true, 130, 26); openCode.Location = new Point(10, 262);
            openCode.Click += delegate { ShowTaskCode(); };
            codeStat = new Label();
            codeStat.Text = "节点 0 个 · 代码 0 行";
            codeStat.ForeColor = Theme.TextDim; codeStat.AutoSize = true; codeStat.Location = new Point(150, 268);
            left.Controls.Add(n1); left.Controls.Add(taskNameBox); left.Controls.Add(nb); left.Controls.Add(sb);
            left.Controls.Add(begExec); left.Controls.Add(begDebug); left.Controls.Add(begStop);
            left.Controls.Add(n2); left.Controls.Add(n3); left.Controls.Add(repeatBox);
            left.Controls.Add(miniPrev);
            left.Controls.Add(openCode); left.Controls.Add(codeStat);

            // 右侧：窗口绑定
            right = new Panel();
            right.Location = new Point(430, 4);
            right.Size = new Size(ClientSize.Width - 438, 470);
            right.BackColor = Theme.Panel;
            right.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            beginnerView.Controls.Add(right);

            // 页签
            string[] tabs = new string[] { "后台", "图色", "录制", "拖动", "AI识图", "DLL" };
            List<DarkButton> tabBtns = new List<DarkButton>();
            tabPages = new Panel();
            tabPages.BackColor = Theme.Panel;
            right.Controls.Add(tabPages);
            tabRow = new Panel();
            tabRow.BackColor = Theme.Panel;
            for (int i = 0; i < tabs.Length; i++)
            {
                string name = tabs[i];
                DarkButton tb = new DarkButton(name, i == 0, 64, 24);
                tb.Location = new Point(8 + i * 68, 2);
                tb.Click += delegate
                {
                    foreach (DarkButton b in tabBtns) b.AccentStyle = false;
                    tb.AccentStyle = true;
                    foreach (Control cc in tabPages.Controls) cc.Visible = false;
                    tabPages.Controls[name].Visible = true;
                };
                tabBtns.Add(tb);
                tabRow.Controls.Add(tb);
            }
            right.Controls.Remove(tabRow);
            beginnerView.Controls.Add(tabRow);
            tabRow.BringToFront();
            Action relayout = delegate()
            {
                int rw = Math.Max(50, right.ClientSize.Width);
                int rh = Math.Max(50, right.ClientSize.Height);
                right.Bounds = new Rectangle(right.Left, 36, rw, Math.Max(60, beginnerView.Height - 40));
                tabRow.Bounds = new Rectangle(right.Left, 4, rw, 28);
                tabPages.Bounds = new Rectangle(0, 0, rw, rh);
                foreach (Control cc in tabPages.Controls)
                {
                    cc.Bounds = new Rectangle(0, 0, rw, rh);
                }
            };
            relayout();
            right.Resize += delegate(object s2, EventArgs e2) { relayout(); };
            beginnerView.Resize += delegate(object s2, EventArgs e2) { relayout(); };

            // 可拖拽分隔条：调整左侧任务面板宽度
            Divider begDiv = new Divider();
            begDiv.Location = new Point(426, 4);
            begDiv.Size = new Size(4, beginnerView.Height - 8);
            begDiv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            begDiv.OnDrag += delegate(int dx)
            {
                int nw = Math.Max(300, Math.Min(620, left.Width + dx));
                int actual = nw - left.Width;
                if (actual == 0) return;
                left.Width = nw;
                right.Left += actual;
                relayout();
            };
            beginnerView.Controls.Add(begDiv);
            begDiv.BringToFront();

            // --- 后台页 ---
            Panel bindPage = BindPage();
            bindPage.Name = "后台";
            bindPage.Size = tabPages.Size;
            bindPage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            tabPages.Controls.Add(bindPage);

            // --- 图色页 ---
            Panel imgPage = new Panel();
            imgPage.Name = "图色";
            imgPage.Size = tabPages.Size;
            imgPage.BackColor = Theme.Panel;
            imgPage.Visible = false;
            Label il = new Label(); il.Text = "模板图片（保存在 模板图片\\ 目录，找图命令可直接使用图片名）"; il.ForeColor = Theme.TextDim; il.AutoSize = true; il.Location = new Point(10, 8);
            ListBox imgList = new ListBox();
            imgList.Location = new Point(10, 30); imgList.Size = new Size(300, 200);
            imgList.BackColor = Theme.InputBg; imgList.ForeColor = Theme.Text; imgList.BorderStyle = BorderStyle.FixedSingle;
            DarkButton capBtn = new DarkButton("截取屏幕区域", true, 120, 26); capBtn.Location = new Point(10, 240);
            capBtn.Click += delegate
            {
                RegionPicker rp = new RegionPicker();
                if (rp.ShowDialog(this) == DialogResult.OK)
                {
                    Rectangle rc = rp.Region;
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板图片");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, "截图" + DateTime.Now.ToString("HHmmss") + ".bmp");
                    Vision.CaptureRect(rc.X, rc.Y, rc.Right, rc.Bottom, file);
                    Log("已保存模板图片: " + Path.GetFileName(file));
                    FillTplList(imgList, dir);
                }
            };
            DarkButton delImg = new DarkButton("删除", false, 60, 26); delImg.Location = new Point(140, 240);
            delImg.Click += delegate
            {
                if (imgList.SelectedItem == null) return;
                try { File.Delete(TplPath(imgList.SelectedItem.ToString())); FillTplList(imgList, null); } catch { }
            };
            imgPage.Controls.Add(il); imgPage.Controls.Add(imgList); imgPage.Controls.Add(capBtn); imgPage.Controls.Add(delImg);
            imgPage.VisibleChanged += delegate { if (imgPage.Visible) { string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板图片"); FillTplList(imgList, dir); } };
            tabPages.Controls.Add(imgPage);
            Vision.TemplateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板图片");

            // --- 录制页 ---
            Panel recPage = new Panel();
            recPage.Name = "录制";
            recPage.Size = tabPages.Size;
            recPage.BackColor = Theme.Panel;
            recPage.Visible = false;
            DarkButton recBtn = new DarkButton("打开键盘鼠标录制器", true, 150, 30); recBtn.Location = new Point(10, 16);
            Label recTip = new Label();
            recTip.Text = "录制鼠标移动/单击/双击/拖动/滚轮/键盘按键（含CTRL等组合、连击、长按），\r\n录制完可直接\"转为多功能代码\"在多功能中编辑执行，或生成\"自定义\"节点";
            recTip.ForeColor = Theme.TextDim; recTip.AutoSize = false; recTip.Size = new Size(560, 40); recTip.Location = new Point(10, 56);
            recBtn.Click += delegate
            {
                RecorderForm rf = new RecorderForm();
                rf.OnInsertNode = delegate(string gen)
                {
                    FlowTask t = CurrentTask();
                    FNode node = t.Add("自定义", 200, 200);
                    node.P["code"] = gen;
                    canvas.Invalidate();
                    UpdateCodeStat();
                    Log("已生成自定义节点");
                };
                rf.OnOpenCode = OpenRecordingAsCode;
                rf.ShowDialog(this);
            };
            recPage.Controls.Add(recBtn); recPage.Controls.Add(recTip);
            tabPages.Controls.Add(recPage);

            // --- 拖动页 ---
            Panel dragPage = new Panel();
            dragPage.Name = "拖动";
            dragPage.Dock = DockStyle.Fill;
            dragPage.BackColor = Theme.Panel;
            dragPage.Visible = false;
            Label dragTip = new Label();
            dragTip.Text = "拾取屏幕上的起点和终点，生成\"鼠标按下→移动→抬起\"拖动操作节点";
            dragTip.ForeColor = Theme.TextDim; dragTip.AutoSize = true; dragTip.Location = new Point(10, 12);
            Label dragFrom = new Label(); dragFrom.Text = "起点: 未拾取"; dragFrom.ForeColor = Theme.Text; dragFrom.AutoSize = true; dragFrom.Location = new Point(10, 48);
            Label dragTo = new Label(); dragTo.Text = "终点: 未拾取"; dragTo.ForeColor = Theme.Text; dragTo.AutoSize = true; dragTo.Location = new Point(10, 72);
            Point dragP1 = Point.Empty, dragP2 = Point.Empty;
            bool hasP1 = false, hasP2 = false;
            DarkButton pick1 = new DarkButton("拾取起点", true, 100, 28); pick1.Location = new Point(10, 100);
            DarkButton pick2 = new DarkButton("拾取终点", true, 100, 28); pick2.Location = new Point(118, 100);
            DarkButton genDrag = new DarkButton("生成拖动节点", false, 120, 28); genDrag.Location = new Point(10, 138);
            pick1.Click += delegate
            {
                using (PickerOverlay po = new PickerOverlay())
                {
                    if (po.ShowDialog(this) == DialogResult.OK && po.PickedPoint != Point.Empty)
                    {
                        dragP1 = po.PickedPoint; hasP1 = true;
                        dragFrom.Text = "起点: " + dragP1.X + ", " + dragP1.Y;
                    }
                }
            };
            pick2.Click += delegate
            {
                using (PickerOverlay po = new PickerOverlay())
                {
                    if (po.ShowDialog(this) == DialogResult.OK && po.PickedPoint != Point.Empty)
                    {
                        dragP2 = po.PickedPoint; hasP2 = true;
                        dragTo.Text = "终点: " + dragP2.X + ", " + dragP2.Y;
                    }
                }
            };
            genDrag.Click += delegate
            {
                if (!hasP1 || !hasP2) { MessageBox.Show("请先拾取起点和终点"); return; }
                FlowTask t = CurrentTask();
                FNode dn = t.Add("自定义", 300, 160);
                dn.Note = "拖动 " + dragP1.X + "," + dragP1.Y + " → " + dragP2.X + "," + dragP2.Y;
                dn.P["code"] = "鼠标移动(" + dragP1.X + ", " + dragP1.Y + ")\r\n延迟(150)\r\n鼠标左键按下()\r\n延迟(100)\r\n鼠标移动(" + dragP2.X + ", " + dragP2.Y + ", 1)\r\n延迟(100)\r\n鼠标左键抬起()";
                if (canvas != null) canvas.Invalidate();
                UpdateCodeStat();
                Log("已生成拖动节点: " + dn.Note);
            };
            dragPage.Controls.Add(dragTip); dragPage.Controls.Add(dragFrom); dragPage.Controls.Add(dragTo);
            dragPage.Controls.Add(pick1); dragPage.Controls.Add(pick2); dragPage.Controls.Add(genDrag);
            tabPages.Controls.Add(dragPage);

            // --- AI识图页 ---
            Panel ocrPage = new Panel();
            ocrPage.Name = "AI识图";
            ocrPage.Size = tabPages.Size;
            ocrPage.BackColor = Theme.Panel;
            ocrPage.Visible = false;
            DarkButton ocrBtn = new DarkButton("框选识别文本", true, 130, 30); ocrBtn.Location = new Point(10, 16);
            RichTextBox ocrOut = new RichTextBox();
            ocrOut.Location = new Point(10, 56); ocrOut.Size = new Size(430, 200);
            ocrOut.BackColor = Theme.InputBg; ocrOut.ForeColor = Theme.Text; ocrOut.BorderStyle = BorderStyle.FixedSingle;
            ocrOut.ReadOnly = true;
            ocrBtn.Click += delegate
            {
                RegionPicker rp = new RegionPicker();
                if (rp.ShowDialog(this) == DialogResult.OK)
                {
                    Rectangle rc = rp.Region;
                    List<Vision.OcrItem> items;
                    Log("正在启动离线OCR（首次需加载模型，请稍候）...");
                    int n = Vision.OcrRegion(rc.X, rc.Y, rc.Right, rc.Bottom, 0.3, out items, "");
                    StringBuilder sb2 = new StringBuilder();
                    foreach (Vision.OcrItem it in items)
                        sb2.AppendLine(string.Format("({0},{1})-({2},{3})  {4}", it.Left, it.Top, it.Right, it.Bottom, it.Text));
                    ocrOut.Text = sb2.ToString();
                    Log("识别完成，共 " + n + " 条文本");
                }
            };
            ocrPage.Controls.Add(ocrBtn); ocrPage.Controls.Add(ocrOut);
            tabPages.Controls.Add(ocrPage);

            // --- DLL 页：程序目录中的全部 DLL 状态与调用 ---
            Panel dllPage = new Panel();
            dllPage.Name = "DLL";
            dllPage.Size = tabPages.Size;
            dllPage.BackColor = Theme.Panel;
            dllPage.Visible = false;
            Label dl = new Label();
            dl.Text = "程序目录中的 DLL 全部可用：脚本用 加载DLL/调用DLL/调用DLL函数/卸载DLL/获取DLL导出 即可调用任意导出函数。\r\nx64 DLL 本进程直接调用；x86 DLL 自动经 DllBridge32.exe 桥接调用；Qt5/opencv/paddle 等为内置工具的运行库依赖。";
            dl.ForeColor = Theme.TextDim; dl.AutoSize = false; dl.Size = new Size(660, 42); dl.Location = new Point(10, 6);
            dllList = new ListBox();
            dllList.Location = new Point(10, 52); dllList.Size = new Size(620, 300);
            dllList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            dllList.BackColor = Theme.InputBg; dllList.ForeColor = Theme.Text; dllList.BorderStyle = BorderStyle.FixedSingle;
            dllList.Font = Theme.FontSmall;
            dllList.DoubleClick += delegate { ShowDllExports(); };
            dllStat = new Label();
            dllStat.Text = "尚未检测";
            dllStat.ForeColor = Theme.TextDim; dllStat.AutoSize = true; dllStat.Location = new Point(10, 360);
            DarkButton dllScan = new DarkButton("重新检测", false, 90, 28); dllScan.Location = new Point(10, 386);
            dllScan.Click += delegate { FillDllList(); };
            DarkButton dllLoad = new DarkButton("测试加载", false, 90, 28); dllLoad.Location = new Point(108, 386);
            dllLoad.Click += delegate
            {
                DllHost.DllInfo info = SelectedDll();
                if (info == null) return;
                uint err;
                bool ok = DllHost.TestLoad(info.Path, out err);
                info.LoadTested = true; info.LoadOk = ok; info.LastError = err;
                Log((ok ? "加载测试通过: " : "加载测试失败: ") + info.Name + (ok ? "" : " (Win32错误 " + err + ")"));
                FillDllList();
            };
            DarkButton dllExp = new DarkButton("查看导出函数", false, 110, 28); dllExp.Location = new Point(206, 386);
            dllExp.Click += delegate { ShowDllExports(); };
            DarkButton dllGen = new DarkButton("生成调用代码", true, 110, 28); dllGen.Location = new Point(324, 386);
            dllGen.Click += delegate { GenDllCode(); };
            dllPage.Controls.Add(dl); dllPage.Controls.Add(dllList); dllPage.Controls.Add(dllStat);
            dllPage.Controls.Add(dllScan); dllPage.Controls.Add(dllLoad); dllPage.Controls.Add(dllExp); dllPage.Controls.Add(dllGen);
            dllPage.VisibleChanged += delegate { if (dllPage.Visible && dllList.Items.Count == 0) FillDllList(); };
            tabPages.Controls.Add(dllPage);
        }

        // ================= DLL 页辅助 =================
        DllHost.DllInfo SelectedDll()
        {
            if (dllList == null || dllList.SelectedIndex < 0 || dllList.SelectedIndex >= dllInfos.Count) return null;
            return dllInfos[dllList.SelectedIndex];
        }

        void FillDllList()
        {
            dllList.Items.Clear();
            dllInfos.Clear();
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            List<DllHost.DllInfo> list = new List<DllHost.DllInfo>();
            try
            {
                List<string> files = new List<string>(Directory.GetFiles(dir, "*.dll"));
                files.Sort();
                foreach (string f in files) list.Add(DllHost.Inspect(f));
            }
            catch { }
            dllInfos.AddRange(list);
            foreach (DllHost.DllInfo d in dllInfos)
                dllList.Items.Add(string.Format("{0}    {1}    导出{2}    {3}    {4}",
                    d.Name, d.Arch, d.HasExports ? d.Exports.ToString() : "无", d.Kind, d.StatusText));
            int plugin = 0, x86 = 0, net = 0, dep = 0;
            foreach (DllHost.DllInfo d in dllInfos)
            {
                if (!d.SystemDep && !d.IsNet && d.HasExports) plugin++;
                if (d.Arch == "x86") x86++;
                if (d.IsNet) net++;
                if (d.SystemDep) dep++;
            }
            dllStat.Text = string.Format("共 {0} 个 DLL：可调用插件 {1} 个（x86 {2} 个经桥接调用）、.NET 程序集 {3} 个、运行库依赖 {4} 个",
                dllInfos.Count, plugin, x86, net, dep);
            Log("DLL 检测完成: 共 " + dllInfos.Count + " 个，可调用插件 " + plugin + " 个");
        }

        void ShowDllExports()
        {
            DllHost.DllInfo info = SelectedDll();
            if (info == null) { Log("请先选择一个 DLL"); return; }
            List<string> names = DllHost.ExportNames(info.Path, 300);
            if (names.Count == 0) { Log(info.Name + " 没有可显示的导出函数"); return; }
            Log("==== " + info.Name + " 导出函数 " + names.Count + " 个（最多显示300） ====");
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                sb.Append(names[i]);
                if (i % 5 == 4 || i == names.Count - 1)
                {
                    Log("  " + sb.ToString());
                    sb.Length = 0;
                }
                else sb.Append("  ");
            }
        }

        void GenDllCode()
        {
            DllHost.DllInfo info = SelectedDll();
            if (info == null) { Log("请先选择一个 DLL"); return; }
            List<string> exps = DllHost.ExportNames(info.Path, 50);
            string fn = exps.Count > 0 ? exps[0] : "函数名";
            string code = "# 自动生成: " + info.Name + " 调用示例（" + info.Arch + "，" + info.Kind + "）\r\n" +
                "句柄 = 加载DLL(\"" + info.Name + "\")\r\n" +
                "如果(句柄 != 0)\r\n开始\r\n" +
                "    返回值 = 调用DLL(句柄, \"" + fn + "\", 0)\r\n" +
                "    输出调试(\"" + fn + " 返回: \" + 返回值)\r\n" +
                "    卸载DLL(句柄)\r\n" +
                "结束\r\n" +
                "否则\r\n开始\r\n" +
                "    输出调试(\"加载DLL失败\")\r\n" +
                "结束";
            codeView.OpenOrSwitch("DLL示例", code);
            SwitchView(codeView, "切换视图");
            Log("已生成 " + info.Name + " 调用示例代码，可在多功能中修改执行");
        }

        // 录制完成 → 直接转为多功能代码
        void OpenRecordingAsCode(string code)
        {
            string name = "录制" + DateTime.Now.ToString("HHmmss");
            codeView.OpenOrSwitch(name, code);
            SwitchView(codeView, "切换视图");
            Log("录制完成，已转为多功能代码页: " + name + "（可修改后直接执行）");
        }

        string TplPath(string name) { return Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板图片"), name); }
        void FillTplList(ListBox lb, string dir)
        {
            lb.Items.Clear();
            if (dir == null) dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板图片");
            if (!Directory.Exists(dir)) return;
            foreach (string f in Directory.GetFiles(dir))
                lb.Items.Add(Path.GetFileName(f));
        }

        Panel BindPage()
        {
            Panel p = new Panel();
            p.BackColor = Theme.Panel;

            Label t1 = new Label(); t1.Text = "窗口绑定"; t1.ForeColor = Theme.Text; t1.Font = Theme.FontUIB; t1.AutoSize = true; t1.Location = new Point(10, 8);
            Label h1 = new Label(); h1.Text = "窗口句柄"; h1.ForeColor = Theme.TextDim; h1.AutoSize = true; h1.Location = new Point(30, 40);
            hwndBox = new TextBox(); hwndBox.Location = new Point(110, 36); hwndBox.Size = new Size(170, 24);
            Theme.StyleInput(hwndBox);
            DarkButton bindBtn = new DarkButton("绑定", true, 64, 26); bindBtn.Location = new Point(290, 34);
            DarkButton pickBtn = new DarkButton("✚", false, 40, 26); pickBtn.Location = new Point(360, 34);
            DarkButton unbindBtn = new DarkButton("取消绑定", false, 76, 26); unbindBtn.Location = new Point(406, 34);
            unbindBtn.Click += delegate
            {
                rt.BoundWindow = IntPtr.Zero;
                Vision.CaptureSource = IntPtr.Zero;
                hwndBox.Text = ""; wtitleBox.Text = ""; wclassBox.Text = ""; wpidBox.Text = "";
                Log("已取消窗口绑定");
            };
            bindBtn.Click += delegate { DoBind(); };
            pickBtn.Click += delegate
            {
                using (PickerOverlay po = new PickerOverlay())
                {
                    if (po.ShowDialog(this) == DialogResult.OK && po.Picked != IntPtr.Zero)
                    {
                        hwndBox.Text = po.Picked.ToInt64().ToString();
                        DoBind();
                    }
                }
            };

            Label h2 = new Label(); h2.Text = "窗口标题:"; h2.ForeColor = Theme.TextDim; h2.AutoSize = true; h2.Location = new Point(30, 76);
            wtitleBox = new TextBox(); wtitleBox.Location = new Point(110, 72); wtitleBox.Size = new Size(290, 24); wtitleBox.ReadOnly = true;
            Theme.StyleInput(wtitleBox);
            Label h3 = new Label(); h3.Text = "窗口类:"; h3.ForeColor = Theme.TextDim; h3.AutoSize = true; h3.Location = new Point(30, 108);
            wclassBox = new TextBox(); wclassBox.Location = new Point(110, 104); wclassBox.Size = new Size(290, 24); wclassBox.ReadOnly = true;
            Theme.StyleInput(wclassBox);
            Label h4 = new Label(); h4.Text = "进程ID:"; h4.ForeColor = Theme.TextDim; h4.AutoSize = true; h4.Location = new Point(30, 140);
            wpidBox = new TextBox(); wpidBox.Location = new Point(110, 136); wpidBox.Size = new Size(290, 24); wpidBox.ReadOnly = true;
            Theme.StyleInput(wpidBox);

            Label hint = new Label();
            hint.Text = "  离线版说明：前台模式全功能可用；后台模式（句柄消息模拟）以兼容方式运行  ";
            hint.ForeColor = Color.FromArgb(120, 170, 255);
            hint.AutoSize = true;
            hint.Location = new Point(10, 170);

            // 后台配置
            Label t2 = new Label(); t2.Text = "后台配置"; t2.ForeColor = Theme.Text; t2.Font = Theme.FontUIB; t2.AutoSize = true; t2.Location = new Point(10, 200);
            Label modeTitle = new Label(); modeTitle.Text = "极速模式"; modeTitle.ForeColor = Theme.Accent; modeTitle.Font = Theme.FontUIB;
            modeTitle.Location = new Point(220, 200); modeTitle.AutoSize = true;

            cbBindMode = ModeCombo(); cbImgMode = ModeCombo(); cbMouseMode = ModeCombo(); cbKeyMode = ModeCombo(); cbCapMode = ModeCombo();
            AddModeRow(p, cbBindMode, "绑定模式(极速):", "通用", 232);
            AddModeRow(p, cbImgMode, "图色模式(极速):", "前台", 262);
            AddModeRow(p, cbCapMode, "截图模式(极速):", "前台", 292);
            AddModeRow(p, cbMouseMode, "鼠标模式(极速):", "前台", 322);
            AddModeRow(p, cbKeyMode, "键盘模式(极速):", "前台", 352);
            cbImgMode.SelectedIndexChanged += delegate { ApplyBackendModes(); };
            cbMouseMode.SelectedIndexChanged += delegate { ApplyBackendModes(); };
            cbKeyMode.SelectedIndexChanged += delegate { ApplyBackendModes(); };

            DarkButton exec2 = new DarkButton("执行", true, 90, 28); exec2.Location = new Point(210, 396);
            exec2.Click += delegate { RunBeginner(int.Parse(Safe(repeatBox.Text, "1"))); };

            p.Controls.Add(t1); p.Controls.Add(h1); p.Controls.Add(hwndBox); p.Controls.Add(bindBtn); p.Controls.Add(pickBtn); p.Controls.Add(unbindBtn);
            p.Controls.Add(h2); p.Controls.Add(wtitleBox); p.Controls.Add(h3); p.Controls.Add(wclassBox);
            p.Controls.Add(h4); p.Controls.Add(wpidBox); p.Controls.Add(hint);
            p.Controls.Add(t2); p.Controls.Add(modeTitle); p.Controls.Add(exec2);
            return p;
        }
        ComboBox ModeCombo()
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Theme.InputBg; c.ForeColor = Theme.Text;
            return c;
        }
        void AddModeRow(Panel p, ComboBox c, string label, string sel, int y)
        {
            Label l = new Label(); l.Text = label; l.ForeColor = Theme.TextDim; l.AutoSize = true; l.Location = new Point(60, y + 4);
            c.Items.Clear();
            if (label.StartsWith("绑定")) foreach (string s in new string[] { "通用", "标准", "超级" }) c.Items.Add(s);
            else foreach (string s in new string[] { "前台", "后台" }) c.Items.Add(s);
            c.SelectedItem = sel;
            c.Size = new Size(110, 24);
            c.Location = new Point(170, y);
            p.Controls.Add(l); p.Controls.Add(c);
        }

        void DoBind()
        {
            long hv;
            if (!long.TryParse(hwndBox.Text, out hv)) { Log("句柄无效"); return; }
            IntPtr h = (IntPtr)hv;
            if (h == IntPtr.Zero) { Log("句柄为0"); return; }
            rt.BoundWindow = h;
            ApplyBackendModes();
            wtitleBox.Text = Native.GetWindowText(h);
            wclassBox.Text = Native.GetClassName(h);
            wpidBox.Text = Native.GetWindowPid(h).ToString();
            string mode = (cbImgMode != null && cbImgMode.Text == "后台") ? "，后台图色已启用" : "";
            Log("已绑定窗口: " + wtitleBox.Text + " (句柄 " + hv + ")" + mode);
        }

        // 应用前台/后台模式设置（鼠标/键盘/图色）
        void ApplyBackendModes()
        {
            rt.BackendCap = (cbImgMode != null && cbImgMode.Text == "后台") || (edImgMode != null && edImgMode.Text == "后台");
            rt.BackendMouse = (cbMouseMode != null && cbMouseMode.Text == "后台") || (edMouseMode != null && edMouseMode.Text == "后台");
            rt.BackendKey = (cbKeyMode != null && cbKeyMode.Text == "后台") || (edKeyMode != null && edKeyMode.Text == "后台");
            Vision.CaptureSource = (rt.BackendCap && rt.BoundWindow != IntPtr.Zero) ? rt.BoundWindow : IntPtr.Zero;
        }

        // ================= 编辑视图 =================
        void BuildEditView()
        {
            editView = new Panel();
            editView.Dock = DockStyle.Fill;
            editView.BackColor = Theme.Bg;

            // 左侧操作栏
            Panel ops = new Panel();
            ops.Location = new Point(4, 4);
            ops.Size = new Size(96, 420);
            ops.BackColor = Theme.Panel;
            editView.Controls.Add(ops);
            ops.Paint += delegate(object s, PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, "任务操作", Theme.FontSmall, new Point(8, 3), Theme.TextDim);
                TextRenderer.DrawText(e.Graphics, "模板操作", Theme.FontSmall, new Point(8, 158), Theme.TextDim);
                TextRenderer.DrawText(e.Graphics, "其它", Theme.FontSmall, new Point(8, 268), Theme.TextDim);
            };
            string[] opsBtns = new string[] { "新建", "保存", "导入", "导出" };
            for (int i = 0; i < opsBtns.Length; i++)
            {
                string name = opsBtns[i];
                DarkButton b = new DarkButton(name, false, 74, 26);
                b.Location = new Point(11, 22 + i * 32);
                b.Click += delegate
                {
                    if (name == "新建") NewTask();
                    else if (name == "保存") SaveTask(false);
                    else if (name == "导入") ImportTask();
                    else ExportTask();
                };
                ops.Controls.Add(b);
            }
            DarkButton tplBtn = new DarkButton("模板管理", false, 74, 26);
            tplBtn.Location = new Point(11, 178);
            tplBtn.Click += delegate { ShowTemplates(); };
            ops.Controls.Add(tplBtn);
            DarkButton clearBtn = new DarkButton("清空内容", false, 74, 26);
            clearBtn.Location = new Point(11, 288);
            clearBtn.Click += delegate
            {
                if (MessageBox.Show("确定清空当前任务内容？", "提示", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    CurrentTask().Nodes.Clear();
                    canvas.Task = CurrentTask();
                    canvas.Invalidate();
                }
            };
            ops.Controls.Add(clearBtn);

            // 页签
            taskTabs = new TabStrip();
            taskTabs.Location = new Point(104, 4);
            taskTabs.Size = new Size(ClientSize.Width - 108, 30);
            taskTabs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            taskTabs.OnClose += delegate(string o)
            {
                string name = (string)o;
                TaskDoc doc = tasks.Find(delegate(TaskDoc d) { return d.Name == name; });
                if (doc == null) return;
                tasks.Remove(doc);
                taskTabs.RemoveItem(name);
                if (codeView != null) codeView.CloseDoc(name);
                if (tasks.Count == 0) NewTask();
                else SelectTask(tasks[tasks.Count - 1].Name);
            };
            taskTabs.OnSelect += delegate(string o) { SelectTask(o); };
            editView.Controls.Add(taskTabs);

            // 工具栏
            flowRunPanel = new Panel();
            flowRunPanel.Location = new Point(104, 36);
            flowRunPanel.Size = new Size(ClientSize.Width - 108, 40);
            flowRunPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            flowRunPanel.BackColor = Theme.Panel;
            editView.Controls.Add(flowRunPanel);

            execBtn = new DarkButton("执行", true, 66, 26); execBtn.Location = new Point(10, 7);
            debugBtn = new DarkButton("调试", false, 60, 26); debugBtn.Location = new Point(82, 7);
            stopBtn = new DarkButton("停止", false, 60, 26); stopBtn.Location = new Point(148, 7); stopBtn.Visible = false;
            execBtn.Click += delegate { RunFlow(int.Parse(Safe(repeatBox.Text, "1"))); };
            debugBtn.Click += delegate { RunFlow(1); };
            stopBtn.Click += delegate { rt.StopFlag = true; };

            Label ml = new Label(); ml.Text = "模式类型:"; ml.ForeColor = Theme.TextDim; ml.AutoSize = true; ml.Location = new Point(152, 12);
            ComboBox modeSel = ModeCombo();
            foreach (string m in new string[] { "极速模式", "高速模式", "兼容模式" }) modeSel.Items.Add(m);
            modeSel.SelectedItem = "极速模式"; modeSel.Location = new Point(208, 8); modeSel.Size = new Size(80, 24);
            modeSel.SelectedIndexChanged += delegate { begMode = modeSel.Text; };
            Label lb = new Label(); lb.Text = "绑定模式:"; lb.ForeColor = Theme.TextDim; lb.AutoSize = true; lb.Location = new Point(292, 12);
            ComboBox bindC = ModeCombo(); foreach (string m in new string[] { "通用", "标准" }) bindC.Items.Add(m);
            bindC.SelectedItem = "通用"; bindC.Location = new Point(352, 8); bindC.Size = new Size(58, 24);
            ComboBox imgC = ModeCombo(); foreach (string m in new string[] { "前台", "后台" }) imgC.Items.Add(m);
            imgC.SelectedItem = "前台"; imgC.Location = new Point(474, 8); imgC.Size = new Size(58, 24);
            imgC.SelectedIndexChanged += delegate { ApplyBackendModes(); };
            edImgMode = imgC;
            Label l1 = new Label(); l1.Text = "图色模式:"; l1.ForeColor = Theme.TextDim; l1.AutoSize = true; l1.Location = new Point(416, 12);
            l1.BringToFront();
            ComboBox mouseC = ModeCombo(); foreach (string m in new string[] { "前台", "后台" }) mouseC.Items.Add(m);
            mouseC.SelectedItem = "前台"; mouseC.Location = new Point(596, 8); mouseC.Size = new Size(58, 24);
            mouseC.SelectedIndexChanged += delegate { ApplyBackendModes(); };
            edMouseMode = mouseC;
            Label l2 = new Label(); l2.Text = "鼠标模式:"; l2.ForeColor = Theme.TextDim; l2.AutoSize = true; l2.Location = new Point(538, 12);
            ComboBox keyC = ModeCombo(); foreach (string m in new string[] { "前台", "后台" }) keyC.Items.Add(m);
            keyC.SelectedItem = "前台"; keyC.Location = new Point(718, 8); keyC.Size = new Size(58, 24);
            keyC.SelectedIndexChanged += delegate { ApplyBackendModes(); };
            edKeyMode = keyC;
            Label l3 = new Label(); l3.Text = "键盘模式:"; l3.ForeColor = Theme.TextDim; l3.AutoSize = true; l3.Location = new Point(660, 12);
            DarkButton bindWinBtn = new DarkButton("窗口绑定", false, 78, 26); bindWinBtn.Location = new Point(860, 7);
            flowRunPanel.Controls.Add(bindWinBtn);
            flowRunPanel.Controls.Add(execBtn); flowRunPanel.Controls.Add(debugBtn); flowRunPanel.Controls.Add(stopBtn);
            flowRunPanel.Controls.Add(lb);
            flowRunPanel.Controls.Add(ml); flowRunPanel.Controls.Add(modeSel);
            flowRunPanel.Controls.Add(bindC);
            flowRunPanel.Controls.Add(l1); flowRunPanel.Controls.Add(imgC);
            flowRunPanel.Controls.Add(l2); flowRunPanel.Controls.Add(mouseC);
            flowRunPanel.Controls.Add(l3); flowRunPanel.Controls.Add(keyC);

            // 画布
            canvasHost = new Panel();
            canvasHost.Location = new Point(104, 78);
            canvasHost.Size = new Size(ClientSize.Width - 108, 340);
            canvasHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            canvasHost.BackColor = Theme.CanvasBg;
            editView.Controls.Add(canvasHost);

            // 底部状态栏
            Panel status = new Panel();
            status.Location = new Point(104, 424);
            status.Size = new Size(ClientSize.Width - 108, 30);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.BackColor = Theme.Panel;
            editView.Controls.Add(status);
            Label hint = new Label();
            hint.Text = "按住CTRL + 鼠标左键可以移动        右键添加节点，双击节点配置，从节点右侧箭头拖出连线";
            hint.ForeColor = Theme.TextDim; hint.AutoSize = true; hint.Location = new Point(8, 8);
            status.Controls.Add(hint);
            countLabel = new Label(); countLabel.Text = "当前次数: 0"; countLabel.ForeColor = Theme.TextDim; countLabel.AutoSize = true; countLabel.Location = new Point(560, 8);
            status.Controls.Add(countLabel);
            zoomOut = new DarkButton("-", false, 26, 24); zoomOut.Location = new Point(status.Width - 200, 3);
            zoomIn = new DarkButton("+", false, 26, 24); zoomIn.Location = new Point(status.Width - 170, 3);
            zoomFit = new DarkButton("适配", false, 44, 24); zoomFit.Location = new Point(status.Width - 140, 3);
            zoomLabel = new Label(); zoomLabel.Text = "100.0%"; zoomLabel.ForeColor = Theme.TextDim; zoomLabel.AutoSize = true;
            zoomLabel.Location = new Point(status.Width - 88, 9);
            zoomOut.Click += delegate { canvas.ZoomOut(); UpdateZoom(); };
            zoomIn.Click += delegate { canvas.ZoomIn(); UpdateZoom(); };
            zoomFit.Click += delegate { canvas.ZoomFit(); UpdateZoom(); };
            status.Controls.Add(zoomOut); status.Controls.Add(zoomIn); status.Controls.Add(zoomFit); status.Controls.Add(zoomLabel);

            // 可拖拽分隔条：调整左侧操作栏宽度
            Divider editDiv = new Divider();
            editDiv.Location = new Point(100, 4);
            editDiv.Size = new Size(4, 646);
            editDiv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;
            editDiv.OnDrag += delegate(int dx)
            {
                int nw = Math.Max(70, Math.Min(320, ops.Width + dx));
                int actual = nw - ops.Width;
                if (actual == 0) return;
                ops.Width = nw;
                taskTabs.Left += actual; flowRunPanel.Left += actual;
                canvasHost.Left += actual; status.Left += actual;
            };
            editView.Controls.Add(editDiv);

            CreateDefaultTask();
        }

        void UpdateZoom() { zoomLabel.Text = (canvas.Zoom * 100).ToString("0.0") + "%"; }

        void ShowTemplates()
        {
            Form f = new Form();
            f.Text = "模板管理";
            f.FormBorderStyle = FormBorderStyle.Sizable;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(420, 360);
            f.BackColor = Theme.Panel;
            f.Font = Theme.FontUI;
            ListBox lb = new ListBox();
            lb.Location = new Point(10, 10); lb.Size = new Size(240, 300);
            lb.BackColor = Theme.InputBg; lb.ForeColor = Theme.Text; lb.BorderStyle = BorderStyle.FixedSingle;
            DarkButton del = new DarkButton("删除模板", false, 90, 28); del.Location = new Point(260, 10);
            DarkButton ins = new DarkButton("插入到当前", true, 110, 28); ins.Location = new Point(260, 46);
            Action fill = delegate { lb.Items.Clear(); foreach (string n in TemplateStore.Names()) lb.Items.Add(n); };
            fill();
            del.Click += delegate
            {
                if (lb.SelectedItem == null) return;
                try { File.Delete(System.IO.Path.Combine(TemplateStore.Dir, lb.SelectedItem + ".json")); fill(); } catch { }
            };
            ins.Click += delegate
            {
                if (lb.SelectedItem == null) return;
                List<FNode> ns = TemplateStore.Load(lb.SelectedItem.ToString());
                if (ns == null) return;
                FlowTask t = CurrentTask();
                int ox = 240, oy = 160;
                Dictionary<FNode, FNode> map = new Dictionary<FNode, FNode>();
                foreach (FNode n in ns)
                {
                    FNode c = t.Add(n.Type, n.X + ox, n.Y + oy);
                    c.P = new Dictionary<string, string>(n.P); c.Note = n.Note;
                    map[n] = c;
                }
                foreach (FNode n in ns)
                    foreach (FLink lk in n.Out)
                        if (map.ContainsKey(lk.To)) map[n].Out.Add(new FLink(map[lk.To], lk.Kind));
                canvas.Invalidate();
                f.Close();
            };
            f.Controls.Add(lb); f.Controls.Add(del); f.Controls.Add(ins);
            f.ShowDialog(this);
        }

        // 任务管理
        public FlowTask CurrentTask() { return CurDoc().Flow; }
        TaskDoc CurDoc()
        {
            TaskDoc d = tasks.Find(delegate(TaskDoc t) { return t.Name == taskTabs.Selected; });
            if (d == null && tasks.Count > 0) d = tasks[0];
            if (d == null) { NewTask(); d = tasks[0]; }
            return d;
        }
        void NewTask()
        {
            using (InputDialog d = new InputDialog("任务名称", "任务" + (tasks.Count + 1)))
            {
                if (d.ShowDialog(this) != DialogResult.OK || d.Value == "") return;
                string name = d.Value;
                if (tasks.Exists(delegate(TaskDoc t) { return t.Name == name; }))
                {
                    MessageBox.Show("任务已存在"); return;
                }
                TaskDoc doc = new TaskDoc();
                doc.Name = name;
                doc.Flow = new FlowTask();
                doc.Flow.Name = name;
                tasks.Add(doc);
                taskTabs.AddItem(name);
                SelectTask(name);
                Log("新建任务: " + name);
            }
        }
        void CreateDefaultTask()
        {
            if (tasks.Count > 0) return;
            TaskDoc doc = new TaskDoc();
            doc.Name = "1";
            doc.Flow = new FlowTask();
            doc.Flow.Name = "1";
            tasks.Add(doc);
            taskTabs.AddItem("1");
            SelectTask("1");
            taskNameBox.Text = "1";
        }
        void SelectTask(string name)
        {
            taskTabs.SetSelected(name);
            TaskDoc doc = CurDoc();
            if (canvas == null)
            {
                canvas = new FlowCanvas(doc.Flow);
                canvas.Dock = DockStyle.Fill;
                canvas.OpenInCodeView = OpenNodeCode;
                canvasHost.Controls.Add(canvas);
            }
            else
            {
                canvas.Task = doc.Flow;
            }
            EnsureDefaultNodes(doc.Flow);
            canvas.Invalidate();
            taskNameBox.Text = name;
            UpdateCodeStat();
            // 多功能联动：切换任务时同步代码页
            if (codeView != null) codeView.OpenOrSwitch(name, doc.Flow.Code);
        }

        // 多功能代码变化 → 同步任务代码 / 自定义节点代码，并刷新初级视图联动显示
        void OnCodeChanged(string docName, string code)
        {
            if (docName.StartsWith("节点"))
            {
                FNode n;
                if (nodeCodeBinding.TryGetValue(docName, out n)) n.P["code"] = code;
            }
            else
            {
                TaskDoc d = tasks.Find(delegate(TaskDoc t) { return t.Name == docName; });
                if (d != null) d.Flow.Code = code;
            }
            // 初级视图联动：流程预览与代码统计实时刷新
            if (miniPrev != null) miniPrev.Invalidate();
            UpdateCodeStat();
        }

        // 初级视图 → 多功能：打开当前任务的代码页（双向联动）
        void ShowTaskCode()
        {
            TaskDoc d = CurDoc();
            if (d != null && codeView != null) codeView.OpenOrSwitch(d.Name, d.Flow.Code);
            SwitchView(codeView, "切换视图");
        }

        void UpdateCodeStat()
        {
            if (codeStat == null) return;
            try
            {
                FlowTask t = CurrentTask();
                int nodes = t.Nodes.Count;
                int lines = 0;
                string code = t.Code ?? "";
                foreach (char c in code) if (c == '\n') lines++;
                if (code.Trim() != "" && lines == 0) lines = 1;
                codeStat.Text = "节点 " + nodes + " 个 · 代码 " + lines + " 行";
            }
            catch { }
        }

        // 任务重命名（初级视图改名 → 页签/多功能文档同步）
        void TryRenameTask()
        {
            TaskDoc d;
            try { d = CurDoc(); } catch { return; }
            if (d == null) return;
            string nn = (taskNameBox.Text ?? "").Trim();
            if (nn == "" || nn == d.Name) return;
            if (tasks.Exists(delegate(TaskDoc t) { return t != d && t.Name == nn; }))
            {
                taskNameBox.Text = d.Name;
                Log("任务名称已存在: " + nn);
                return;
            }
            string old = d.Name;
            d.Name = nn; d.Flow.Name = nn;
            taskTabs.Rename(old, nn);
            if (codeView != null) codeView.RenameDoc(old, nn);
            Log("任务已重命名: " + old + " → " + nn);
        }

        // 自定义节点 → 在多功能中打开其代码（修改自动写回节点）
        void OpenNodeCode(FNode n)
        {
            SwitchView(codeView, "切换视图");
            string docName = "节点" + n.Id;
            nodeCodeBinding[docName] = n;
            codeView.OpenOrSwitch(docName, n.G("code", ""));
            Log("已在多功能中打开 节点" + n.Id + " 的代码，修改会自动同步回节点");
        }

        // 空任务默认在画布中心放 开始、结束（不自动连线）
        void EnsureDefaultNodes(FlowTask t)
        {
            if (t.Nodes.Count > 0 || canvas == null) return;
            int cx = Math.Max(240, canvas.ClientSize.Width / 2);
            int cy = Math.Max(120, canvas.ClientSize.Height / 2);
            t.Add("开始", cx - 180, cy - 70);
            t.Add("结束", cx + 40, cy - 70);
        }
        void SaveTask(bool silent)
        {
            TaskDoc doc = CurDoc();
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = doc.Name + ".btask";
                sfd.Filter = "任务文件 (*.btask)|*.btask";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, FlowIO.Serialize(doc.Flow), Encoding.UTF8);
                    Log("已保存任务: " + sfd.FileName);
                }
            }
        }
        void ImportTask()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "任务文件 (*.btask)|*.btask|全部文件 (*.*)|*.*";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        FlowTask ft = FlowIO.Deserialize(File.ReadAllText(ofd.FileName, Encoding.UTF8));
                        TaskDoc doc = new TaskDoc();
                        doc.Name = ft.Name == "" ? Path.GetFileNameWithoutExtension(ofd.FileName) : ft.Name;
                        while (tasks.Exists(delegate(TaskDoc t) { return t.Name == doc.Name; })) doc.Name += "2";
                        doc.Flow = ft;
                        tasks.Add(doc);
                        taskTabs.AddItem(doc.Name);
                        SelectTask(doc.Name);
                        Log("已导入任务: " + doc.Name);
                    }
                    catch (Exception ex) { MessageBox.Show("导入失败: " + ex.Message); }
                }
            }
        }
        void ExportTask()
        {
            TaskDoc doc = CurDoc();
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = doc.Name + "_导出.btask";
                sfd.Filter = "任务文件 (*.btask)|*.btask";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, FlowIO.Serialize(doc.Flow), Encoding.UTF8);
                    Log("已导出任务: " + sfd.FileName);
                }
            }
        }

        // ================= 多功能 =================
        void BuildCodeView()
        {
            codeView = new CodeView();
            codeView.OnRun = delegate(string code) { RunCode(code, 1); };
            codeView.OnRunRepeat = delegate(string code) { RunCode(code, int.Parse(Safe(repeatBox.Text, "1"))); };
            codeView.OnStop = delegate { rt.StopFlag = true; };
        }

        // ================= 运行引擎 =================
        static string Safe(string s, string d)
        {
            int v; return int.TryParse(s, out v) ? s : d;
        }

        void SetRunning(bool r, string from)
        {
            running = r; runningFrom = from;
            MethodInvoker up = delegate
            {
                begExec.Enabled = !r; begDebug.Enabled = !r; begStop.Visible = r;
                execBtn.Enabled = !r; debugBtn.Enabled = !r; stopBtn.Visible = r;
                codeView.ShowRunning(r);
            };
            if (InvokeRequired) BeginInvoke(up); else up();
        }

        void RunBeginner(int repeat)
        {
            // 联动：流程图只有默认开始/结束节点而多功能代码有内容时，直接执行任务代码
            FlowTask t = CurrentTask();
            bool flowEmpty = true;
            foreach (FNode n in t.Nodes)
                if (n.Type != "开始" && n.Type != "结束") { flowEmpty = false; break; }
            if (flowEmpty && (t.Code ?? "").Trim() != "")
            {
                RunCode(t.Code, repeat);
                return;
            }
            RunFlow(repeat);
        }

        void RunFlow(int repeat)
        {
            if (running) return;
            FlowTask task = CurrentTask();
            List<FNode> starts = task.Nodes.FindAll(delegate(FNode n) { return n.Type == "开始"; });
            if (starts.Count == 0) { MessageBox.Show("请先添加\"开始\"节点"); return; }
            if (starts.Count == 1) { StartFlowRun(task, starts[0], repeat); return; }
            // 多个开始节点：弹出菜单选择执行入口（与原版一致）
            DarkMenu m = new DarkMenu();
            foreach (FNode s0 in starts)
            {
                FNode chosen = s0;
                string label = "开始#" + chosen.Id + (string.IsNullOrEmpty(chosen.Note) ? "" : " (" + chosen.Note + ")");
                m.Add(label, delegate { StartFlowRun(task, chosen, repeat); });
            }
            Point anchor = canvas == null ? new Point(300, 200) : new Point(canvas.Left + 160, canvas.Top + 60);
            m.ShowAt(this, PointToScreen(anchor));
        }

        void StartFlowRun(FlowTask task, FNode start, int repeat)
        {
            StartRunThread(delegate(Runtime r)
            {
                Interp interp = new Interp(r) { Sc = new Script() };
                Runtime.CurrentInterp = interp;
                for (int i = 0; i < Math.Max(1, repeat); i++)
                {
                    r.CheckStop();
                    int idx = i + 1;
                    Action<int> pf = r.ProgressFn;
                    if (pf != null) pf(idx);
                    Log("—— 第 " + idx + " 次执行 ——");
                    FlowCanvas temp = new FlowCanvas(task);
                    string err = temp.Execute(r, interp, start);
                    if (err != null) { Log("任务结束: " + err); break; }
                }
            }, "流程任务: " + task.Name);
        }

        void RunCode(string code, int repeat)
        {
            if (running) return;
            if (code.Trim() == "") return;
            StartRunThread(delegate(Runtime r)
            {
                Interp interp = new Interp(r) { Sc = new Script() };
                Runtime.CurrentInterp = interp;
                Script sc = Interp.Compile(code);
                interp.Sc = sc;
                for (int i = 0; i < Math.Max(1, repeat); i++)
                {
                    r.CheckStop();
                    interp.Run(sc.Body);
                }
            }, "脚本: " + codeView.CurrentName);
        }

        void StartRunThread(Action<Runtime> body, string name)
        {
            rt.StopFlag = false;
            ApplyBackendModes();
            if (outputPanel.Height <= 40) { outputPanel.Height = 130; LayoutOutput(); }
            begCount.Text = "当前次数: 0";
            countLabel.Text = "当前次数: 0";
            SetRunning(true, name);
            Log("开始执行 " + name);
            runThread = new Thread(delegate()
            {
                try
                {
                    body(rt);
                    Log("执行完成");
                }
                catch (StopSignal) { Log("已停止"); }
                catch (Exception ex)
                {
                    Log("执行出错: " + ex.Message);
                }
                finally
                {
                    SetRunning(false, "");
                }
            });
            runThread.SetApartmentState(ApartmentState.STA);
            runThread.IsBackground = true;
            runThread.Start();
        }

        void HotkeyRun()
        {
            if (running) { rt.StopFlag = true; return; }
            if (activeView == codeView) RunCode(codeView.CurrentCode(), 1);
            else RunFlow(1);
        }

        // ================= 设置/热键 =================
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

        static uint VkOfName(string n)
        {
            switch (n)
            {
                case "F7": return 0x76;
                case "F8": return 0x77;
                case "F9": return 0x78;
                case "F10": return 0x79;
                case "F11": return 0x7A;
                case "F12": return 0x7B;
            }
            return 0x78;
        }

        void RegisterHotkeys()
        {
            RegisterHotKey(Handle, 1, MOD_NONE, VkOfName(hotStartName));
            RegisterHotKey(Handle, 2, MOD_NONE, VkOfName(hotStopName));
        }
        void UnregisterHotkeys()
        {
            try { UnregisterHotKey(Handle, 1); UnregisterHotKey(Handle, 2); } catch { }
        }

        string SettingsPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"); } }
        void LoadSettings()
        {
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> o = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                if (o.ContainsKey("hotStart")) hotStartName = o["hotStart"].ToString();
                if (o.ContainsKey("hotStop")) hotStopName = o["hotStop"].ToString();
                if (o.ContainsKey("topmost")) topMost = Convert.ToBoolean(o["topmost"]);
                TopMost = topMost;
            }
            catch { }
        }
        void SaveSettings()
        {
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> o = new Dictionary<string, object>();
                o["hotStart"] = hotStartName; o["hotStop"] = hotStopName; o["topmost"] = topMost;
                File.WriteAllText(SettingsPath, js.Serialize(o), Encoding.UTF8);
            }
            catch { }
        }

        void ShowSettings()
        {
            using (SettingsForm sf = new SettingsForm(hotStartName, hotStopName))
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    hotStartName = sf.HotStart; hotStopName = sf.HotStop;
                    UnregisterHotkeys();
                    RegisterHotkeys();
                    SaveSettings();
                    Log("设置已保存: 启动=" + hotStartName + " 停止=" + hotStopName);
                }
            }
        }
    }

    // 区域选择器（半透明遮罩框选）
    public class RegionPicker : Form
    {
        public Rectangle Region;
        Point start, cur;
        bool dragging;

        public RegionPicker()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.35;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) { Close(); return; }
                dragging = true; start = e.Location; cur = e.Location;
            };
            MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (dragging) { cur = e.Location; Invalidate(); }
            };
            MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (!dragging) return;
                dragging = false;
                int x = Math.Min(start.X, cur.X), y = Math.Min(start.Y, cur.Y);
                int w = Math.Abs(cur.X - start.X), h = Math.Abs(cur.Y - start.Y);
                if (w < 2 || h < 2) { Close(); return; }
                Region = new Rectangle(x, y, w, h);
                DialogResult = DialogResult.OK;
                Close();
            };
            Paint += delegate(object s, PaintEventArgs e)
            {
                if (!dragging) return;
                Rectangle r = new Rectangle(Math.Min(start.X, cur.X), Math.Min(start.Y, cur.Y), Math.Abs(cur.X - start.X), Math.Abs(cur.Y - start.Y));
                using (Pen p = new Pen(Theme.Accent, 2)) e.Graphics.DrawRectangle(p, r);
            };
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return false; } }
    }
}
