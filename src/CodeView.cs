using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BulingOffline
{
    using System.Runtime.InteropServices;

    // 多功能视图：命令树 + 代码编辑器
    public class CodeView : Panel
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        const int WM_SETREDRAW = 0x000B;
        public class CodeDoc
        {
            public string Name;
            public RichTextBox Box;
        }
        public List<CodeDoc> Docs = new List<CodeDoc>();
        public CodeDoc Current;
        TabStrip tabs;
        TreeView tree;
        RichTextBox usage;
        Panel left, right;
        DarkButton insertBtn;
        public Action<string> OnRun;      // 调试(单次)
        public Action<string> OnRunRepeat;
        public Action OnStop;
        public Func<bool> IsRunning;
        DarkButton runBtn, debugBtn, stopBtn;
        System.Windows.Forms.Timer hlTimer;

        public CodeView()
        {
            BackColor = Theme.Bg;
            Dock = DockStyle.Fill;

            left = new Panel(); left.Width = 320; left.Dock = DockStyle.Left; left.BackColor = Theme.Panel;
            right = new Panel(); right.Dock = DockStyle.Fill; right.BackColor = Theme.Bg;
            Divider codeDiv = new Divider();
            codeDiv.Dock = DockStyle.Left;
            codeDiv.Width = 4;
            codeDiv.OnDrag += delegate(int dx)
            {
                left.Width = Math.Max(200, Math.Min(520, left.Width + dx));
            };
            Controls.Add(right); Controls.Add(codeDiv); Controls.Add(left);

            // 顶部工具条
            Panel tool = new Panel(); tool.Dock = DockStyle.Top; tool.Height = 40; tool.BackColor = Theme.Panel;
            runBtn = new DarkButton("执行", true, 66, 26); runBtn.Location = new Point(10, 7);
            debugBtn = new DarkButton("调试", false, 60, 26); debugBtn.Location = new Point(82, 7);
            stopBtn = new DarkButton("停止", false, 60, 26); stopBtn.Location = new Point(148, 26); stopBtn.Visible = false;
            stopBtn.Location = new Point(148, 7);
            runBtn.Click += delegate { if (OnRunRepeat != null) OnRunRepeat(CurrentCode()); };
            debugBtn.Click += delegate { if (OnRun != null) OnRun(CurrentCode()); };
            stopBtn.Click += delegate { if (OnStop != null) OnStop(); };
            DarkButton saveBtn = new DarkButton("保存", false, 60, 26); saveBtn.Location = new Point(214, 7);
            saveBtn.Click += delegate { SaveCurrent(); };
            tool.Controls.Add(runBtn); tool.Controls.Add(debugBtn); tool.Controls.Add(stopBtn); tool.Controls.Add(saveBtn);
            right.Controls.Add(tool);

            tabs = new TabStrip();
            tabs.Dock = DockStyle.Top; tabs.Height = 30; tabs.BackColor = Theme.Bg;
            tabs.OnClose += delegate(string name) { CloseDoc(name); };
            tabs.OnSelect += delegate(string name) { SwitchDoc(name); };
            right.Controls.Add(tabs);
            right.Controls.SetChildIndex(tabs, 1);

            // 左侧：搜索 + 命令树 + 用法 + 生成代码
            TextBox search = new TextBox();
            search.Dock = DockStyle.Top; search.Height = 24;
            search.BackColor = Theme.InputBg; search.ForeColor = Theme.Text; search.BorderStyle = BorderStyle.FixedSingle;
            search.Font = Theme.FontUI;
            search.ForeColor = Theme.Text;
            left.Controls.Add(search);

            insertBtn = new DarkButton("生成代码", true, 296, 28);
            insertBtn.Dock = DockStyle.Bottom;
            insertBtn.Click += delegate { InsertSelected(); };
            left.Controls.Add(insertBtn);

            usage = new RichTextBox();
            usage.Dock = DockStyle.Bottom; usage.Height = 230;
            usage.BackColor = Theme.Panel2; usage.ForeColor = Theme.TextDim;
            usage.BorderStyle = BorderStyle.None; usage.ReadOnly = true;
            usage.Font = Theme.FontSmall;
            usage.DetectUrls = false;
            left.Controls.Add(usage);

            tree = new TreeView();
            tree.Dock = DockStyle.Fill;
            tree.BackColor = Theme.Panel; tree.ForeColor = Theme.Text;
            tree.BorderStyle = BorderStyle.None;
            tree.Font = Theme.FontUI;
            tree.ShowLines = false;
            tree.FullRowSelect = true;
            tree.HideSelection = true;
            BuildTree("");
            left.Controls.Add(tree);
            tree.BringToFront();

            tree.AfterSelect += delegate(object s, TreeViewEventArgs e)
            {
                if (e.Node.Tag is CommandCatalog.Cmd)
                {
                    CommandCatalog.Cmd c = (CommandCatalog.Cmd)e.Node.Tag;
                    usage.Text = StripHtml(c.Usage);
                }
            };
            tree.NodeMouseDoubleClick += delegate(object s, TreeNodeMouseClickEventArgs e)
            {
                if (e.Node.Tag is CommandCatalog.Cmd) InsertSelected();
            };

            search.TextChanged += delegate { BuildTree(search.Text.Trim()); };

            // 语法高亮
            hlTimer = new System.Windows.Forms.Timer();
            hlTimer.Interval = 400;
            hlTimer.Tick += delegate { hlTimer.Stop(); Highlight(Current == null ? null : Current.Box); };

            NewDoc("脚本1");
        }

        public string CurrentCode() { return Current == null ? "" : Current.Box.Text; }

        public void ShowRunning(bool running)
        {
            runBtn.Enabled = !running; debugBtn.Enabled = !running;
            stopBtn.Visible = running;
            Invalidate();
        }

        public void NewDoc(string name)
        {
            string baseName = name; int i = 1;
            while (Docs.Exists(delegate(CodeDoc d) { return d.Name == baseName; })) baseName = name + (++i);
            RichTextBox box = new RichTextBox();
            box.Dock = DockStyle.Fill;
            box.BackColor = Theme.Bg; box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.None;
            box.Font = Theme.FontCode;
            box.WordWrap = false;
            box.AcceptsTab = true;
            box.Text = "";
            CodeDoc doc = new CodeDoc(); doc.Name = baseName; doc.Box = box;
            box.TextChanged += delegate
            {
                hlTimer.Stop(); hlTimer.Start(); tabs.Invalidate();
                if (OnCodeChanged != null) OnCodeChanged(doc.Name, box.Text);
            };
            right.Controls.Add(box);
            box.BringToFront();
            Docs.Add(doc);
            tabs.AddItem(baseName);
            SwitchDoc(baseName);
        }

        // 重命名文档（任务重命名时联动）
        public void RenameDoc(string oldName, string newName)
        {
            CodeDoc doc = Docs.Find(delegate(CodeDoc d) { return d.Name == oldName; });
            if (doc == null) return;
            doc.Name = newName;
            tabs.Rename(oldName, newName);
        }

        // 代码变化回调（联动任务/节点代码），参数: 文档名, 新代码
        public Action<string, string> OnCodeChanged;

        void SwitchDoc(string name)
        {
            foreach (CodeDoc d in Docs)
            {
                d.Box.Visible = d.Name == name;
                if (d.Name == name) Current = d;
            }
            tabs.SetSelected(name);
            if (Current != null) Highlight(Current.Box);
        }

        public void CloseDoc(string name)
        {
            CodeDoc doc = Docs.Find(delegate(CodeDoc d) { return d.Name == name; });
            if (doc == null) return;
            Docs.Remove(doc);
            right.Controls.Remove(doc.Box);
            tabs.RemoveItem(name);
            if (Current == doc)
            {
                Current = null;
                if (Docs.Count > 0) SwitchDoc(Docs[Docs.Count - 1].Name);
                else NewDoc("脚本");
            }
        }

        public void OpenOrSwitch(string name, string code)
        {
            if (Current != null && Current.Name == name)
            {
                // 已是该文档且内容一致则不动，避免光标重置
                if (Current.Box.Text == (code ?? "").Replace("\r\n", "\n")) return;
            }
            CodeDoc doc = Docs.Find(delegate(CodeDoc d) { return d.Name == name; });
            if (doc == null)
            {
                NewDoc(name);
                doc = Docs.Find(delegate(CodeDoc d) { return d.Name == name; });
            }
            doc.Box.Text = code;
            SwitchDoc(name);
        }

        public void SaveCurrent()
        {
            if (Current == null) return;
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.FileName = Current.Name + ".txt";
                sfd.Filter = "脚本文件 (*.txt)|*.txt";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(sfd.FileName, Current.Box.Text, Encoding.UTF8);
                }
            }
        }

        public string CurrentName { get { return Current == null ? "脚本" : Current.Name; } }

        void InsertSelected()
        {
            if (Current == null || tree.SelectedNode == null) return;
            if (!(tree.SelectedNode.Tag is CommandCatalog.Cmd)) return;
            CommandCatalog.Cmd c = (CommandCatalog.Cmd)tree.SelectedNode.Tag;
            // 统一换行为 CRLF，避免 \r\r\n
            string insert = c.Code.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (!insert.EndsWith("\r\n")) insert += "\r\n";
            // 光标不在行首时先换行，保证按行生成
            RichTextBox box = Current.Box;
            int pos = box.SelectionStart;
            string txt = box.Text ?? "";
            if (pos > 0 && pos <= txt.Length && txt[pos - 1] != '\n') insert = "\r\n" + insert;
            if (pos > txt.Length) pos = txt.Length;
            // RichTextBox.SelectedText 对中文会转成 '?'，改用整体文本替换（Unicode 安全）
            box.Text = txt.Insert(pos, insert);
            box.SelectionStart = pos + insert.Length;
            box.Focus();
        }

        void BuildTree(string filter)
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            foreach (string cat in CommandCatalog.CategoryOrder)
            {
                TreeNode catNode = null;
                foreach (CommandCatalog.Cmd c in CommandCatalog.All)
                {
                    if (c.Cat != cat) continue;
                    if (filter != "" && c.Name.IndexOf(filter, StringComparison.Ordinal) < 0 && cat.IndexOf(filter, StringComparison.Ordinal) < 0) continue;
                    if (catNode == null)
                    {
                        catNode = new TreeNode(cat);
                        tree.Nodes.Add(catNode);
                    }
                    TreeNode n = new TreeNode(c.Name);
                    n.Tag = c;
                    catNode.Nodes.Add(n);
                }
            }
            tree.ExpandAll();
            tree.EndUpdate();
        }

        static string StripHtml(string html)
        {
            if (html == null) return "";
            string s = html.Replace("<br/>", "\n").Replace("<br>", "\n").Replace("<br />", "\n");
            s = Regex.Replace(s, @"</p>", "\n");
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = s.Replace("&nbsp;", " ").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        // ---------- 语法高亮 ----------
        static readonly string[] Keywords = new string[] { "变量", "如果", "否则如果", "否则", "循环", "开始", "结束", "跳出循环", "继续循环", "子任务", "子任务完成", "返回", "执行任务文件" };
        static readonly Color KwColor = Color.FromArgb(120, 160, 255);
        static readonly Color StrColor = Color.FromArgb(230, 170, 90);
        static readonly Color CmtColor = Color.FromArgb(110, 170, 110);
        static readonly Color FnColor = Color.FromArgb(220, 220, 140);
        static readonly Color NumColor = Color.FromArgb(190, 140, 220);

        public static void Highlight(RichTextBox box)
        {
            if (box == null) return;
            int selStart = box.SelectionStart, selLen = box.SelectionLength;
            box.SuspendLayout();
            // 暂停重绘，消除闪烁
            bool created = box.IsHandleCreated;
            if (created) SendMessage(box.Handle, WM_SETREDRAW, (IntPtr)0, IntPtr.Zero);
            box.SelectAll();
            box.SelectionColor = Theme.Text;
            box.DeselectAll();
            string text = box.Text;
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (c == '#')
                {
                    int j = text.IndexOf('\n', i); if (j < 0) j = n;
                    ColorRange(box, i, j - i, CmtColor);
                    i = j; continue;
                }
                if (c == '"')
                {
                    int j = i + 1;
                    while (j < n && text[j] != '"' && text[j] != '\n') j++;
                    if (j < n && text[j] == '"') j++;
                    ColorRange(box, i, j - i, StrColor);
                    i = j; continue;
                }
                if (char.IsDigit(c))
                {
                    int j = i;
                    while (j < n && (char.IsDigit(text[j]) || text[j] == '.')) j++;
                    ColorRange(box, i, j - i, NumColor);
                    i = j; continue;
                }
                if (IsIdChar(c))
                {
                    int j = i;
                    while (j < n && IsIdChar(text[j])) j++;
                    string word = text.Substring(i, j - i);
                    // 是否函数调用
                    int k = j;
                    while (k < n && (text[k] == ' ' || text[k] == '\t')) k++;
                    bool isCall = k < n && text[k] == '(';
                    bool isKw = false;
                    foreach (string kw in Keywords) if (word == kw) { isKw = true; break; }
                    if (isKw) ColorRange(box, i, j - i, KwColor);
                    else if (isCall) ColorRange(box, i, j - i, FnColor);
                    i = j; continue;
                }
                i++;
            }
            box.Select(selStart, selLen);
            box.SelectionColor = Theme.Text;
            if (created) SendMessage(box.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
            box.ResumeLayout();
            box.Invalidate();
        }
        static bool IsIdChar(char c)
        {
            return char.IsLetter(c) || char.IsDigit(c) || c == '_' || (c >= 0x4e00 && c <= 0x9fa5);
        }
        static void ColorRange(RichTextBox box, int start, int len, Color color)
        {
            if (len <= 0) return;
            box.Select(start, len);
            box.SelectionColor = color;
        }
    }

    // 自绘标签条（任务页签）
    public class TabStrip : Control
    {
        public delegate void NameHandler(string name);
        public List<string> Items = new List<string>();
        public string Selected = "";
        public event NameHandler OnClose;
        public event NameHandler OnSelect;
        int hoverIdx = -1, hoverClose = -1;

        public TabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 30;
            MouseMove += TabStripMouseMove;
            MouseLeave += delegate { hoverIdx = -1; hoverClose = -1; Invalidate(); };
            MouseClick += TabStripMouseClick;
        }
        public void AddItem(string name) { if (!Items.Contains(name)) Items.Add(name); Selected = name; Invalidate(); }
        public void RemoveItem(string name) { Items.Remove(name); if (Selected == name) Selected = Items.Count > 0 ? Items[Items.Count - 1] : ""; Invalidate(); }
        public void SetSelected(string name) { Selected = name; Invalidate(); }
        public void Rename(string oldName, string newName)
        {
            int i = Items.IndexOf(oldName);
            if (i >= 0) Items[i] = newName;
            if (Selected == oldName) Selected = newName;
            Invalidate();
        }

        List<Rectangle> rects = new List<Rectangle>();
        List<Rectangle> closes = new List<Rectangle>();
        void LayoutTabs()
        {
            rects.Clear(); closes.Clear();
            int x = 4;
            using (Graphics g = CreateGraphics())
            {
                foreach (string s in Items)
                {
                    int w = TextRenderer.MeasureText(s, Theme.FontUI).Width + 34;
                    rects.Add(new Rectangle(x, 3, w, Height - 6));
                    closes.Add(new Rectangle(x + w - 18, 9, 14, 14));
                    x += w + 2;
                }
            }
        }

        void TabStripMouseMove(object s, MouseEventArgs e)
        {
            LayoutTabs();
            hoverIdx = -1; hoverClose = -1;
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Contains(e.Location)) hoverIdx = i;
                if (closes[i].Contains(e.Location)) hoverClose = i;
            }
            Invalidate();
        }
        void TabStripMouseClick(object s, MouseEventArgs e)
        {
            LayoutTabs();
            for (int i = 0; i < rects.Count; i++)
            {
                if (closes[i].Contains(e.Location)) { if (OnClose != null) OnClose(Items[i]); return; }
                if (rects[i].Contains(e.Location)) { if (OnSelect != null) OnSelect(Items[i]); return; }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            LayoutTabs();
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            for (int i = 0; i < Items.Count; i++)
            {
                bool sel = Items[i] == Selected;
                Rectangle r = rects[i];
                using (GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int rad = 5;
                    gp.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
                    gp.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
                    gp.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                    gp.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                    gp.CloseFigure();
                    using (SolidBrush br = new SolidBrush(sel ? Theme.Panel2 : Color.FromArgb(24, 24, 34)))
                        g.FillPath(br, gp);
                }
                Color tc = sel ? Theme.Text : Theme.TextDim;
                TextRenderer.DrawText(g, Items[i], Theme.FontUI, new Rectangle(r.X + 8, r.Y, r.Width - 22, r.Height), tc,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                // 关闭 ×
                Color xc = (hoverClose == i) ? Theme.Pink : Theme.TextDim;
                TextRenderer.DrawText(g, "×", Theme.FontUIB, closes[i], xc,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
