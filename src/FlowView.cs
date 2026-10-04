using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace BulingOffline
{
    // ============ 流程节点模型 ============
    public class FNode
    {
        public int Id;
        public string Type;
        public string Note = "";
        public int X, Y;
        public int W = 130, H = 46;     // 手动调整后的尺寸
        public bool SizeSet;             // 是否被用户调整过
        public Dictionary<string, string> P = new Dictionary<string, string>();
        public List<FLink> Out = new List<FLink>();
        public static readonly string[] Types = new string[] { "开始", "结束", "自定义", "延迟", "鼠标", "键盘", "找图", "找色", "AI识图", "找字", "判断", "循环" };

        public string Title
        {
            get
            {
                switch (Type)
                {
                    case "延迟": return "延迟";
                    case "鼠标": return G("act", "鼠标移动");
                    case "键盘": return "键盘" + G("act", "单击");
                    case "找图": return "找图节点";
                    case "找色": return "找色节点";
                    case "AI识图": return G("mode", "文本识别") + "节点";
                    case "找字": return "找字节点";
                    case "判断": return "判断";
                    case "循环": return "循环";
                    default: return Type;
                }
            }
        }
        public string G(string k, string d) { string v; return P.TryGetValue(k, out v) && !string.IsNullOrEmpty(v) ? v : d; }

        public List<string> SubLines()
        {
            List<string> ls = new List<string>();
            switch (Type)
            {
                case "延迟": ls.Add(G("ms", "1000") + "ms"); break;
                case "鼠标":
                    {
                        string act = G("act", "鼠标移动");
                        if (act.Contains("移动")) ls.Add("坐标 x: " + G("x", "0") + ", y: " + G("y", "0"));
                        else ls.Add("坐标 x: " + G("x", "0") + ", y: " + G("y", "0"));
                        break;
                    }
                case "键盘": ls.Add("按键: " + G("key", "A") + (G("count", "1") != "1" ? " x" + G("count", "1") : "")); break;
                case "找图": ls.Add("图片: " + G("img", "1.bmp") + "  相似度: " + G("sim", "0.95")); break;
                case "找色": ls.Add("颜色: " + G("color", "ff0000") + "  相似度: " + G("sim", "0.95")); break;
                case "AI识图": ls.Add(G("text", "") == "" ? "识别全部文本" : "查找: " + G("text", "")); break;
                case "找字": ls.Add("查找: " + G("text", "")); break;
                case "判断": ls.Add(G("cond", "1==1")); break;
                case "循环": ls.Add(G("mode", "count") == "count" ? "循环 " + G("count", "1") + " 次" : "条件: " + G("cond", "1")); break;
                case "自定义": ls.Add(G("code", "").Replace("\r", "").Replace("\n", " ; ")); break;
            }
            if (ls.Count == 0) ls.Add("");
            return ls;
        }
    }

    public class FLink
    {
        public FNode To;
        public int Kind; // 0=默认 1=真 2=假 3=循环体 4=循环完成
        public FLink(FNode to, int kind) { To = to; Kind = kind; }
    }

    public class FlowTask
    {
        public string Name = "1";
        public string Code = "";    // 多功能脚本代码（与流程图联动保存）
        public List<FNode> Nodes = new List<FNode>();
        int nextId = 1;
        public FNode Add(string type, int x, int y)
        {
            FNode n = new FNode(); n.Id = nextId++; n.Type = type; n.X = x; n.Y = y;
            if (type == "延迟") n.P["ms"] = "1000";
            if (type == "鼠标") { n.P["act"] = "鼠标移动"; n.P["x"] = "0"; n.P["y"] = "0"; }
            if (type == "键盘") { n.P["act"] = "单击"; n.P["key"] = "A"; n.P["count"] = "1"; }
            if (type == "找图") { n.P["l"] = "0"; n.P["t"] = "0"; n.P["r"] = "800"; n.P["b"] = "600"; n.P["img"] = "1.bmp"; n.P["sim"] = "0.95"; n.P["vx"] = "x"; n.P["vy"] = "y"; }
            if (type == "找色") { n.P["l"] = "0"; n.P["t"] = "0"; n.P["r"] = "800"; n.P["b"] = "600"; n.P["color"] = "ff0000"; n.P["sim"] = "0.95"; n.P["vx"] = "x"; n.P["vy"] = "y"; }
            if (type == "AI识图") { n.P["mode"] = "文本识别"; n.P["l"] = "0"; n.P["t"] = "0"; n.P["r"] = "800"; n.P["b"] = "600"; n.P["conf"] = "0.6"; n.P["text"] = ""; n.P["vt"] = "text"; }
            if (type == "找字") { n.P["l"] = "0"; n.P["t"] = "0"; n.P["r"] = "800"; n.P["b"] = "600"; n.P["conf"] = "0.6"; n.P["text"] = ""; n.P["vt"] = "text"; }
            if (type == "判断") n.P["cond"] = "1==1";
            if (type == "循环") { n.P["mode"] = "count"; n.P["count"] = "1"; }
            if (type == "自定义") n.P["code"] = "";
            Nodes.Add(n);
            return n;
        }
        public void SyncId()
        {
            foreach (FNode n in Nodes) if (n.Id >= nextId) nextId = n.Id + 1;
        }
        public FNode FirstStart()
        {
            foreach (FNode n in Nodes) if (n.Type == "开始") return n;
            return null;
        }
    }

    // ============ 画布 ============
    public class FlowCanvas : Control
    {
        public FlowTask Task;
        public float Zoom = 1f;
        public int PanX, PanY;
        public List<FNode> Selected = new List<FNode>();
        public FNode HoverNode;
        public FNode LinkFrom;   // 正在连线
        public Point LinkCur;
        public Action<FNode> OpenInCodeView;   // 在多功能中编辑自定义节点代码
        bool draggingNode; Point dragStart; Dictionary<FNode, Point> dragOrig;
        bool rubber; Point rbStart, rbCur;
        bool panning; Point panStart;
        bool resizing; FNode resNode; Size resOrig;
        ContextMenu menu;

        bool GripAt(FNode n, Point sp)
        {
            RectangleF r = WorldRect(n);
            Point g = WorldToScreen(r.Right - 10, r.Bottom - 10);
            return sp.X >= g.X - 3 && sp.Y >= g.Y - 3 && sp.X <= g.X + 13 && sp.Y <= g.Y + 13;
        }

        public FlowCanvas(FlowTask task)
        {
            Task = task;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.CanvasBg;
            Cursor = Cursors.Arrow;
            BuildMenu();
        }

        public RectangleF WorldRect(FNode n)
        {
            List<string> subs = n.SubLines();
            float w = n.SizeSet ? n.W : 130;
            float h = n.SizeSet ? n.H : 26 + subs.Count * 16 + 6;
            return new RectangleF(n.X, n.Y, w, h);
        }
        PointF ScreenToWorld(Point p) { return new PointF((p.X - PanX) / Zoom, (p.Y - PanY) / Zoom); }
        Point WorldToScreen(float x, float y) { return new Point((int)(x * Zoom + PanX), (int)(y * Zoom + PanY)); }

        FNode NodeAt(Point sp)
        {
            PointF w = ScreenToWorld(sp);
            for (int i = Task.Nodes.Count - 1; i >= 0; i--)
            {
                RectangleF r = WorldRect(Task.Nodes[i]);
                if (r.Contains(w)) return Task.Nodes[i];
            }
            return null;
        }
        FNode OutPortAt(Point sp)
        {
            PointF w = ScreenToWorld(sp);
            foreach (FNode n in Task.Nodes)
            {
                RectangleF r = WorldRect(n);
                RectangleF port = new RectangleF(r.Right - 12, r.Y + r.Height / 2 - 10, 22, 20);
                if (port.Contains(w)) return n;
            }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CanvasBg);
            // 网格
            int grid = (int)(32 * Zoom);
            if (grid >= 12)
            {
                using (Pen gp = new Pen(Color.FromArgb(26, 26, 38)))
                {
                    int ox = PanX % grid; if (ox < 0) ox += grid;
                    int oy = PanY % grid; if (oy < 0) oy += grid;
                    for (int x = ox; x < Width; x += grid) g.DrawLine(gp, x, 0, x, Height);
                    for (int y = oy; y < Height; y += grid) g.DrawLine(gp, 0, y, Width, y);
                }
            }
            // 连线
            foreach (FNode n in Task.Nodes)
            {
                foreach (FLink lk in n.Out)
                {
                    if (lk.To == null) continue;
                    RectangleF r1 = WorldRect(n), r2 = WorldRect(lk.To);
                    PointF p1 = WorldToScreen(r1.Right - 2, r1.Y + r1.Height / 2);
                    PointF p2 = WorldToScreen(r2.X + 2, r2.Y + r2.Height / 2);
                    Color c = Theme.NodeBorder;
                    if (lk.Kind == 1) c = Theme.Green;
                    else if (lk.Kind == 2) c = Theme.Pink;
                    else if (lk.Kind == 3) c = Color.FromArgb(80, 150, 255);
                    else if (lk.Kind == 4) c = Color.FromArgb(170, 130, 255);
                    using (Pen pen = new Pen(c, 1.6f))
                    {
                        pen.CustomEndCap = new AdjustableArrowCap(6, 7, true);
                        int midx = (int)((p1.X + p2.X) / 2);
                        g.DrawBezier(pen, p1.X, p1.Y, midx, p1.Y, midx, p2.Y, p2.X, p2.Y);
                    }
                    if (lk.Kind == 1 || lk.Kind == 2)
                    {
                        using (Font f = new Font("Microsoft YaHei UI", 7.5f))
                            g.DrawString(lk.Kind == 1 ? "真" : "假", f, new SolidBrush(c), (p1.X + p2.X) / 2 - 6, (p1.Y + p2.Y) / 2 - 14);
                    }
                }
            }
            // 正在拖的连线
            if (LinkFrom != null)
            {
                RectangleF r1 = WorldRect(LinkFrom);
                PointF p1 = WorldToScreen(r1.Right - 2, r1.Y + r1.Height / 2);
                using (Pen pen = new Pen(Theme.Accent, 1.6f)) g.DrawLine(pen, p1.X, p1.Y, LinkCur.X, LinkCur.Y);
            }
            // 节点
            foreach (FNode n in Task.Nodes)
            {
                RectangleF wr = WorldRect(n);
                Rectangle r = new Rectangle(WorldToScreen(wr.X, wr.Y), new Size((int)(wr.Width * Zoom), (int)(wr.Height * Zoom)));
                bool sel = Selected.Contains(n);
                using (GraphicsPath path = DarkButton.Round(new Rectangle(r.X, r.Y, r.Width, r.Height), 5))
                using (SolidBrush br = new SolidBrush(sel ? Color.FromArgb(46, 52, 74) : Theme.NodeBg))
                    g.FillPath(br, path);
                using (Pen pen = new Pen(sel ? Theme.Accent : (HoverNode == n ? Color.FromArgb(96, 96, 124) : Theme.NodeBorder)))
                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                // 编号
                TextRenderer.DrawText(g, n.Id.ToString(), Theme.FontSmall, new Point(r.Left + 4, r.Top + 2), Theme.TextDim);
                // 出口箭头
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Theme.TextDim, 1.5f))
                {
                    pen.CustomEndCap = new AdjustableArrowCap(5, 6, true);
                    int my = r.Y + r.Height / 2;
                    g.DrawLine(pen, r.Right - 10, my, r.Right + 4, my);
                }
                // 内容
                Rectangle inner = new Rectangle(r.Left + 6, r.Top + 14, r.Width - 12, r.Height - 18);
                TextRenderer.DrawText(g, n.Title, Theme.FontUIB, new Rectangle(inner.X, inner.Y, inner.Width, 18), Theme.Text, TextFormatFlags.HorizontalCenter);
                List<string> subs = n.SubLines();
                for (int i = 0; i < subs.Count; i++)
                    TextRenderer.DrawText(g, subs[i], Theme.FontSmall, new Rectangle(inner.X, inner.Y + 17 + i * 14, inner.Width, 15), Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                // 备注
                if (!string.IsNullOrEmpty(n.Note))
                    TextRenderer.DrawText(g, n.Note, Theme.FontSmall, new Point(r.X, r.Bottom + 1), Theme.Pink);
                // 右下角调整大小把手
                using (Pen gp2 = new Pen(Theme.TextDim))
                {
                    g.DrawLine(gp2, r.Right - 3, r.Bottom - 3, r.Right - 9, r.Bottom - 3);
                    g.DrawLine(gp2, r.Right - 3, r.Bottom - 3, r.Right - 3, r.Bottom - 9);
                }
            }
            // 框选
            if (rubber)
            {
                Rectangle rc = RectFrom(rbStart, rbCur);
                using (Pen pen = new Pen(Theme.Accent)) g.DrawRectangle(pen, rc);
                using (SolidBrush br = new SolidBrush(Color.FromArgb(30, 47, 107, 255))) g.FillRectangle(br, rc);
            }
        }
        static Rectangle RectFrom(Point a, Point b)
        {
            return new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        void BuildMenu()
        {
            menu = new ContextMenu();
            foreach (string t in FNode.Types)
            {
                MenuItem mi = new MenuItem(t);
                mi.Click += delegate(object s, EventArgs e)
                {
                    PointF w = ScreenToWorld(menuPoint);
                    FNode n = Task.Add(t, (int)w.X, (int)w.Y);
                    if (t == "自定义" || t == "判断" || t == "循环")
                    {
                        ShowConfig(n);
                    }
                    Invalidate();
                };
                menu.MenuItems.Add(mi);
            }
            menu.MenuItems.Add(new MenuItem("-"));
            MenuItem tpl = new MenuItem("插入模板");
            tpl.Popup += delegate
            {
                tpl.MenuItems.Clear();
                foreach (string name in TemplateStore.Names())
                {
                    MenuItem t2 = new MenuItem(name);
                    string nm = name;
                    t2.Click += delegate
                    {
                        List<FNode> ns = TemplateStore.Load(nm);
                        if (ns == null) return;
                        PointF w = ScreenToWorld(menuPoint);
                        int minx = int.MaxValue, miny = int.MaxValue;
                        foreach (FNode n in ns) { minx = Math.Min(minx, n.X); miny = Math.Min(miny, n.Y); }
                        Dictionary<FNode, FNode> map = new Dictionary<FNode, FNode>();
                        foreach (FNode n in ns)
                        {
                            FNode c = Task.Add(n.Type, n.X - minx + (int)w.X, n.Y - miny + (int)w.Y);
                            c.P = new Dictionary<string, string>(n.P); c.Note = n.Note;
                            map[n] = c;
                        }
                        foreach (FNode n in ns)
                            foreach (FLink lk in n.Out)
                                if (map.ContainsKey(lk.To)) map[n].Out.Add(new FLink(map[lk.To], lk.Kind));
                        Invalidate();
                    };
                    tpl.MenuItems.Add(t2);
                }
            };
            menu.MenuItems.Add(tpl);
        }
        Point menuPoint;

        void ShowConfig(FNode n)
        {
            NodeConfigDialog.Show(this, n);
            Invalidate();
        }

        // 调试钩子：addnode.req 存在时注入示例节点（与右键菜单同路径）
        public void DebugAddNodes()
        {
            FNode s = Task.Add("开始", 380, 260);
            FNode d = Task.Add("延迟", 540, 260);
            d.P["ms"] = "500";
            FNode m2 = Task.Add("鼠标", 700, 260);
            m2.P["act"] = "鼠标左键单击";
            m2.P["x"] = "500"; m2.P["y"] = "300";
            FNode c = Task.Add("自定义", 860, 260);
            c.P["code"] = "输出调试(\"流程执行到自定义节点\")";
            s.Out.Add(new FLink(d, 0));
            d.Out.Add(new FLink(m2, 0));
            m2.Out.Add(new FLink(c, 0));
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            FNode n = NodeAt(e.Location);
            if (e.Button == MouseButtons.Left)
            {
                // 节点右下角调整大小
                FNode gripNode = NodeAt(e.Location);
                if (gripNode != null && GripAt(gripNode, e.Location))
                {
                    resizing = true; resNode = gripNode;
                    RectangleF r = WorldRect(gripNode);
                    resOrig = new Size((int)r.Width, (int)r.Height);
                    dragStart = e.Location;
                    return;
                }
                FNode port = OutPortAt(e.Location);
                if (port != null)
                {
                    LinkFrom = port; LinkCur = e.Location;
                    Invalidate(); return;
                }
                if (n != null)
                {
                    if ((ModifierKeys & Keys.Control) != 0 || Selected.Count == 0 || !Selected.Contains(n))
                    {
                        if ((ModifierKeys & Keys.Control) == 0) Selected.Clear();
                        if (!Selected.Contains(n)) Selected.Add(n);
                    }
                    draggingNode = true;
                    dragStart = e.Location;
                    dragOrig = new Dictionary<FNode, Point>();
                    foreach (FNode s in Selected) dragOrig[s] = new Point(s.X, s.Y);
                    Invalidate(); return;
                }
                // 空白：ctrl 平移 / 框选
                if ((ModifierKeys & Keys.Control) != 0)
                {
                    panning = true; panStart = e.Location; return;
                }
                Selected.Clear();
                rubber = true; rbStart = e.Location; rbCur = e.Location;
                Invalidate(); return;
            }
            if (e.Button == MouseButtons.Right)
            {
                menuPoint = e.Location;
                n = NodeAt(e.Location);
                DarkMenu m = new DarkMenu();
                if (n != null)
                {
                    if (!Selected.Contains(n)) { Selected.Clear(); Selected.Add(n); }
                    FNode target = n;
                    m.Add("配置", delegate { ShowConfig(target); });
                    if (target.Type == "自定义" && OpenInCodeView != null)
                    {
                        FNode codeNode = target;
                        m.Add("在多功能中编辑", delegate { OpenInCodeView(codeNode); });
                    }
                    m.Add("备注", delegate
                    {
                        InputDialog d = new InputDialog("备注", target.Note);
                        if (d.ShowDialog(this) == DialogResult.OK) target.Note = d.Value;
                        Invalidate();
                    });
                    m.Add("断开连接", delegate
                    {
                        target.Out.Clear();
                        foreach (FNode o in Task.Nodes)
                            o.Out.RemoveAll(delegate(FLink lk) { return lk.To == target; });
                        Invalidate();
                    });
                    m.Add("断开前连接", delegate
                    {
                        foreach (FNode o in Task.Nodes)
                            o.Out.RemoveAll(delegate(FLink lk) { return lk.To == target; });
                        Invalidate();
                    });
                    m.Add("断开后连接", delegate { target.Out.Clear(); Invalidate(); });
                    m.Add("存为模板", delegate
                    {
                        InputDialog d = new InputDialog("模板名称", target.Type + "模板");
                        if (d.ShowDialog(this) == DialogResult.OK && d.Value != "")
                            TemplateStore.Save(d.Value, new List<FNode> { target });
                    });
                    m.Add("删除", delegate { RemoveNodes(Selected); });
                }
                else
                {
                    m.Add("全选", delegate { Selected = new List<FNode>(Task.Nodes); Invalidate(); });
                    m.Add("选中存为模板", delegate
                    {
                        if (Selected.Count == 0) return;
                        InputDialog d = new InputDialog("模板名称", "我的模板");
                        if (d.ShowDialog(this) == DialogResult.OK && d.Value != "")
                            TemplateStore.Save(d.Value, Selected);
                    });
                    m.AddSep();
                    foreach (string t in FNode.Types)
                    {
                        string type = t;
                        m.Add(t, delegate
                        {
                            PointF w = ScreenToWorld(menuPoint);
                            FNode nn = Task.Add(type, (int)w.X, (int)w.Y);
                            if (type == "自定义" || type == "判断" || type == "循环") ShowConfig(nn);
                            Invalidate();
                        });
                    }
                    m.AddSep();
                    m.Add("插入模板", delegate
                    {
                        List<string> names = TemplateStore.Names();
                        if (names.Count == 0) return;
                        DarkMenu sub = new DarkMenu();
                        foreach (string name in names)
                        {
                            string nm = name;
                            sub.Add(nm, delegate
                            {
                                List<FNode> ns = TemplateStore.Load(nm);
                                if (ns == null) return;
                                PointF w = ScreenToWorld(menuPoint);
                                int minx = int.MaxValue, miny = int.MaxValue;
                                foreach (FNode tn in ns) { minx = Math.Min(minx, tn.X); miny = Math.Min(miny, tn.Y); }
                                Dictionary<FNode, FNode> map = new Dictionary<FNode, FNode>();
                                foreach (FNode tn in ns)
                                {
                                    FNode cp = Task.Add(tn.Type, tn.X - minx + (int)w.X, tn.Y - miny + (int)w.Y);
                                    cp.P = new Dictionary<string, string>(tn.P); cp.Note = tn.Note;
                                    map[tn] = cp;
                                }
                                foreach (FNode tn in ns)
                                    foreach (FLink lk in tn.Out)
                                        if (map.ContainsKey(lk.To)) map[tn].Out.Add(new FLink(map[lk.To], lk.Kind));
                                Invalidate();
                            });
                        }
                        Point sp = PointToScreen(menuPoint);
                        sub.ShowAt(this, new Point(sp.X + 150, sp.Y));
                    });
                }
                m.ShowAt(this, PointToScreen(e.Location));
                Invalidate();
            }
        }

        void RemoveNodes(List<FNode> list)
        {
            foreach (FNode n in list)
            {
                Task.Nodes.Remove(n);
                foreach (FNode o in Task.Nodes)
                    o.Out.RemoveAll(delegate(FLink lk) { return lk.To == n; });
            }
            list.Clear();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (resizing)
            {
                int nw = Math.Max(90, resOrig.Width + (int)((e.X - dragStart.X) / Zoom));
                int nh = Math.Max(40, resOrig.Height + (int)((e.Y - dragStart.Y) / Zoom));
                resNode.W = nw; resNode.H = nh; resNode.SizeSet = true;
                Invalidate(); return;
            }
            if (LinkFrom != null)
            {
                LinkCur = e.Location; Invalidate(); return;
            }
            if (draggingNode)
            {
                int dx = (int)((e.X - dragStart.X) / Zoom), dy = (int)((e.Y - dragStart.Y) / Zoom);
                foreach (FNode s in Selected)
                {
                    Point o = dragOrig[s];
                    s.X = o.X + dx; s.Y = o.Y + dy;
                }
                Invalidate(); return;
            }
            if (panning)
            {
                PanX += e.X - panStart.X; PanY += e.Y - panStart.Y;
                panStart = e.Location; Invalidate(); return;
            }
            if (rubber)
            {
                rbCur = e.Location;
                Selected = Task.Nodes.FindAll(delegate(FNode n) { return WorldRect(n).IntersectsWith(new RectangleF(ScreenToWorld(rbStart).X, ScreenToWorld(rbStart).Y, rbCur.X - rbStart.X, rbCur.Y - rbStart.Y)); });
                Invalidate(); return;
            }
            FNode h = NodeAt(e.Location);
            Cursor = (h != null && GripAt(h, e.Location)) ? Cursors.SizeNWSE : Cursors.Arrow;
            if (h != HoverNode) { HoverNode = h; Invalidate(); }
            else if (h != null) Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (LinkFrom != null)
            {
                FNode target = NodeAt(e.Location);
                if (target != null && target != LinkFrom)
                {
                    int kind = 0;
                    if (LinkFrom.Type == "判断")
                        kind = LinkFrom.Out.Exists(delegate(FLink l) { return l.Kind == 1; }) ? 2 : 1;
                    else if (LinkFrom.Type == "循环")
                        kind = LinkFrom.Out.Exists(delegate(FLink l) { return l.Kind == 3; }) ? 4 : 3;
                    LinkFrom.Out.Add(new FLink(target, kind));
                }
                LinkFrom = null; Invalidate(); return;
            }
            if (draggingNode) draggingNode = false;
            if (panning) panning = false;
            if (rubber) rubber = false;
            if (resizing) resizing = false;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            FNode n = NodeAt(e.Location);
            if (n != null) ShowConfig(n);
        }

        public void ZoomIn() { Zoom = Math.Min(2.5f, Zoom * 1.2f); Invalidate(); }
        public void ZoomOut() { Zoom = Math.Max(0.3f, Zoom / 1.2f); Invalidate(); }
        public void ZoomFit()
        {
            if (Task.Nodes.Count == 0) { Zoom = 1; PanX = PanY = 10; Invalidate(); return; }
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
            foreach (FNode n in Task.Nodes)
            {
                RectangleF r = WorldRect(n);
                minx = (int)Math.Min(minx, r.X); miny = (int)Math.Min(miny, r.Y);
                maxx = (int)Math.Max(maxx, r.Right); maxy = (int)Math.Max(maxy, r.Bottom);
            }
            int w = maxx - minx + 100, h = maxy - miny + 100;
            Zoom = Math.Min(2f, Math.Min((float)Width / w, (float)Height / h));
            if (Zoom < 0.2f) Zoom = 0.2f;
            PanX = (int)((Width - w * Zoom) / 2 - (minx - 50) * Zoom);
            PanY = (int)((Height - h * Zoom) / 2 - (miny - 50) * Zoom);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) RemoveNodes(Selected);
            if (e.Control && e.KeyCode == Keys.A) { Selected = new List<FNode>(Task.Nodes); Invalidate(); }
            base.OnKeyDown(e);
        }

        // ---------- 执行 ----------
        public string Execute(Runtime rt, Interp interp)
        {
            return Execute(rt, interp, Task.FirstStart());
        }

        public string Execute(Runtime rt, Interp interp, FNode start)
        {
            if (start == null) return "未找到 开始 节点";
            FNode cur = start;
            int steps = 0;
            while (cur != null)
            {
                rt.CheckStop();
                if (++steps > 500000) return "执行步数超限（可能存在死循环）";
                FLink next = null;
                try
                {
                    next = ExecNode(cur, rt, interp);
                }
                catch (StopSignal) { return null; }
                catch (Exception ex) { return cur.Type + "节点错误: " + ex.Message; }
                cur = next == null ? null : next.To;
            }
            return null;
        }

        FLink ExecNode(FNode n, Runtime rt, Interp interp)
        {
            CallArgs a = new CallArgs();
            switch (n.Type)
            {
                case "开始": return FirstLink(n, 0);
                case "结束": throw new StopSignal();
                case "延迟":
                    rt.Delay(Pi(n, "ms", 1000));
                    return FirstLink(n, 0);
                case "鼠标":
                    {
                        string act = n.G("act", "鼠标移动");
                        BuildMouseArgs(n, a);
                        string cmd = "鼠标移动";
                        if (act == "鼠标移动") cmd = "鼠标移动";
                        else if (act == "相对移动") cmd = "鼠标相对移动";
                        else
                        {
                            if (act.Contains("左键")) { cmd = "鼠标左键"; a.AddVal(Value.Int(xi(n, "x"))); a.AddVal(Value.Int(xi(n, "y"))); }
                            else if (act.Contains("右键")) { cmd = "鼠标右键"; a.AddVal(Value.Int(xi(n, "x"))); a.AddVal(Value.Int(xi(n, "y"))); }
                            else if (act.Contains("中键")) { cmd = "鼠标中键"; a.AddVal(Value.Int(xi(n, "x"))); a.AddVal(Value.Int(xi(n, "y"))); }
                            if (act.Contains("双击")) cmd += "双击";
                            else if (act.Contains("单击")) cmd += "单击";
                            else if (act.Contains("按下")) cmd += "按下";
                            else if (act.Contains("抬起")) cmd += "抬起";
                            else if (act.Contains("上滚")) cmd = "鼠标向上滚动";
                            else if (act.Contains("下滚")) cmd = "鼠标向下滚动";
                            if (act.Contains("单击") || act.Contains("双击"))
                            {
                                int cnt = Pi(n, "count", 1), per = Pi(n, "delay", 0);
                                if (cnt > 1 || per > 0) { a.AddVal(Value.Int(cnt)); a.AddVal(Value.Int(per)); }
                            }
                            rt.Call(cmd, a);
                            return FirstLink(n, 0);
                        }
                        rt.Call(cmd, a);
                        return FirstLink(n, 0);
                    }
                case "键盘":
                    {
                        string cmd = "键盘" + n.G("act", "单击");
                        a.AddVal(Value.Str(n.G("key", "A")));
                        if (n.G("act", "单击") == "单击")
                        {
                            int cnt = Pi(n, "count", 1), per = Pi(n, "delay", 0);
                            if (cnt > 1 || per > 0) { a.AddVal(Value.Int(cnt)); a.AddVal(Value.Int(per)); }
                        }
                        rt.Call(cmd, a);
                        return FirstLink(n, 0);
                    }
                case "找图":
                    {
                        a.AddVal(Value.Int(xi(n, "l"))); a.AddVal(Value.Int(xi(n, "t"))); a.AddVal(Value.Int(xi(n, "r"))); a.AddVal(Value.Int(xi(n, "b")));
                        a.AddVal(Value.Str(n.G("img", "1.bmp")));
                        a.AddVal(Value.Str(n.G("devi", "000000")));
                        a.AddVal(Value.Dbl(Pd(n, "sim", 0.95)));
                        a.AddVal(Value.Int(0));
                        a.AddRef(interp.GetSlot(n.G("vx", "x")), -1);
                        a.AddRef(interp.GetSlot(n.G("vy", "y")), -1);
                        rt.Call("查找图片", a);
                        return FirstLink(n, 0);
                    }
                case "找色":
                    {
                        a.AddVal(Value.Int(xi(n, "l"))); a.AddVal(Value.Int(xi(n, "t"))); a.AddVal(Value.Int(xi(n, "r"))); a.AddVal(Value.Int(xi(n, "b")));
                        a.AddVal(Value.Str(n.G("color", "ff0000")));
                        a.AddVal(Value.Dbl(Pd(n, "sim", 0.95)));
                        a.AddVal(Value.Int(0));
                        a.AddRef(interp.GetSlot(n.G("vx", "x")), -1);
                        a.AddRef(interp.GetSlot(n.G("vy", "y")), -1);
                        rt.Call("查找颜色", a);
                        return FirstLink(n, 0);
                    }
                case "AI识图":
                case "找字":
                    {
                        string mode = n.G("mode", "文本识别");
                        if (n.Type == "找字") mode = "文本查找";
                        a.AddVal(Value.Int(xi(n, "l"))); a.AddVal(Value.Int(xi(n, "t"))); a.AddVal(Value.Int(xi(n, "r"))); a.AddVal(Value.Int(xi(n, "b")));
                        a.AddVal(Value.Dbl(Pd(n, "conf", 0.6)));
                        a.AddVal(Value.Dbl(0.3));
                        if (mode == "文本查找") a.AddVal(Value.Str(n.G("text", "")));
                        a.AddVal(Value.Int(0));
                        if (mode == "文本查找") a.AddVal(Value.Int(0));
                        a.AddRef(interp.GetSlot(n.G("vl", "left")), -1);
                        a.AddRef(interp.GetSlot(n.G("vt2", "top")), -1);
                        a.AddRef(interp.GetSlot(n.G("vr", "right")), -1);
                        a.AddRef(interp.GetSlot(n.G("vb", "bottom")), -1);
                        a.AddRef(interp.GetSlot(n.G("vt", "text")), -1);
                        rt.Call(mode, a);
                        return FirstLink(n, 0);
                    }
                case "判断":
                    {
                        Value v;
                        string cond = n.G("cond", "1==1");
                        try { v = rt.EvalExpr(interp, cond); }
                        catch { v = Value.Int(0); }
                        int want = v.Truthy() ? 1 : 2;
                        foreach (FLink lk in n.Out) if (lk.Kind == want) return lk;
                        foreach (FLink lk in n.Out) if (lk.Kind == 0) return lk;
                        return null;
                    }
                case "循环":
                    {
                        int iterations = 1;
                        if (n.G("mode", "count") == "count") iterations = Math.Max(1, Pi(n, "count", 1));
                        else
                        {
                            Value v = rt.EvalExpr(interp, n.G("cond", "1"));
                            iterations = v.Truthy() ? 1000000 : 0; // 条件循环由体执行控制，这里给上限
                        }
                        FLink body = null, done = null;
                        foreach (FLink lk in n.Out)
                        {
                            if (lk.Kind == 3) body = lk;
                            else if (lk.Kind == 4) done = lk;
                        }
                        for (int i = 0; i < iterations; i++)
                        {
                            rt.CheckStop();
                            FLink c = body;
                            int guard = 0;
                            while (c != null && c.To != n)
                            {
                                if (++guard > 200000) throw new Exception("循环体过长");
                                rt.CheckStop();
                                c = ExecNode(c.To, rt, interp);
                            }
                        }
                        return done;
                    }
                case "自定义":
                    {
                        string code = n.G("code", "");
                        if (code.Trim() != "") rt.RunCodeInto(interp, code);
                        return FirstLink(n, 0);
                    }
            }
            return FirstLink(n, 0);
        }

        FLink FirstLink(FNode n, int kind)
        {
            foreach (FLink lk in n.Out) if (lk.Kind == kind) return lk;
            return n.Out.Count > 0 ? n.Out[0] : null;
        }
        void BuildMouseArgs(FNode n, CallArgs a)
        {
            a.AddVal(Value.Int(xi(n, "x"))); a.AddVal(Value.Int(xi(n, "y")));
            if (P(n, "rl") != "" || P(n, "rt") != "" || P(n, "rr") != "" || P(n, "rb") != "")
            {
                a.AddVal(Value.Int(xi(n, "rl"))); a.AddVal(Value.Int(xi(n, "rt")));
                a.AddVal(Value.Int(xi(n, "rr"))); a.AddVal(Value.Int(xi(n, "rb")));
            }
        }
        string P(FNode n, string k) { string v; return n.P.TryGetValue(k, out v) ? v : ""; }
        int xi(FNode n, string k) { int v; return int.TryParse(P(n, k), out v) ? v : 0; }
        int Pi(FNode n, string k, int d) { int v; return int.TryParse(P(n, k), out v) ? v : d; }
        double Pd(FNode n, string k, double d) { double v; return double.TryParse(P(n, k), out v) ? v : d; }
    }

    // ============ 初级视图流程小预览（与编辑/多功能联动，点击进入编辑视图）============
    public class MiniFlowPreview : Control
    {
        public Func<FlowTask> TaskProvider;
        public Action OnOpenEdit;
        System.Windows.Forms.Timer refresh;

        public MiniFlowPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CanvasBg;
            Cursor = Cursors.Hand;
            Click += delegate { if (OnOpenEdit != null) OnOpenEdit(); };
            refresh = new System.Windows.Forms.Timer();
            refresh.Interval = 800;
            refresh.Tick += delegate(object s, EventArgs e)
            {
                if (Visible && Parent != null && Parent.Visible) Invalidate();
            };
            refresh.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CanvasBg);
            FlowTask t = TaskProvider != null ? TaskProvider() : null;
            if (t == null || t.Nodes.Count == 0)
            {
                TextRenderer.DrawText(g, "任务流程预览（空）- 点击进入编辑视图", Theme.FontSmall,
                    ClientRectangle, Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            // 世界范围
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
            foreach (FNode n in t.Nodes)
            {
                minx = Math.Min(minx, n.X); miny = Math.Min(miny, n.Y);
                maxx = Math.Max(maxx, n.X + n.W); maxy = Math.Max(maxy, n.Y + 60);
            }
            float w = Math.Max(1, maxx - minx + 80), h = Math.Max(1, maxy - miny + 60);
            float scale = Math.Min(1f, Math.Min((Width - 14) / w, (Height - 14) / h));
            float ox = (Width - (maxx - minx) * scale) / 2 - minx * scale;
            float oy = (Height - (maxy - miny) * scale) / 2 - miny * scale;
            Func<FNode, RectangleF> rect = delegate(FNode n)
            {
                return new RectangleF(n.X * scale + ox, n.Y * scale + oy, Math.Max(14, n.W * scale), Math.Max(10, 46 * scale));
            };
            // 连线
            foreach (FNode n in t.Nodes)
                foreach (FLink lk in n.Out)
                {
                    if (lk.To == null) continue;
                    RectangleF r1 = rect(n), r2 = rect(lk.To);
                    using (Pen pen = new Pen(lk.Kind == 1 ? Theme.Green : lk.Kind == 2 ? Theme.Pink : Theme.NodeBorder))
                        g.DrawLine(pen, r1.Right, r1.Top + r1.Height / 2, r2.Left, r2.Top + r2.Height / 2);
                }
            // 节点
            foreach (FNode n in t.Nodes)
            {
                RectangleF r = rect(n);
                bool isStart = n.Type == "开始", isEnd = n.Type == "结束";
                using (GraphicsPath gp = DarkButton.Round(new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), 3))
                using (SolidBrush br = new SolidBrush(isStart ? Color.FromArgb(40, 70, 130) : isEnd ? Color.FromArgb(110, 44, 62) : Theme.NodeBg))
                    g.FillPath(br, gp);
                using (Pen pen = new Pen(Theme.NodeBorder))
                    g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                TextRenderer.DrawText(g, n.Title, Theme.FontSmall,
                    new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), Theme.TextDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // ============ 模板存储 ============
    public static class TemplateStore
    {
        public static string Dir
        {
            get
            {
                string d = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "模板");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }
        public static List<string> Names()
        {
            List<string> l = new List<string>();
            foreach (string f in Directory.GetFiles(Dir, "*.json")) l.Add(Path.GetFileNameWithoutExtension(f));
            return l;
        }
        public static void Save(string name, List<FNode> nodes)
        {
            Dictionary<FNode, int> ids = new Dictionary<FNode, int>();
            int i = 1;
            foreach (FNode n in nodes) ids[n] = i++;
            List<NodeDto> dtos = FlowIO.ToDto(nodes, ids);
            JavaScriptSerializer js = new JavaScriptSerializer();
            File.WriteAllText(Path.Combine(Dir, name + ".json"), js.Serialize(dtos), Encoding.UTF8);
        }
        public static List<FNode> Load(string name)
        {
            string p = Path.Combine(Dir, name + ".json");
            if (!File.Exists(p)) return null;
            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                List<NodeDto> dtos = js.Deserialize<List<NodeDto>>(File.ReadAllText(p, Encoding.UTF8));
                return FlowIO.FromDto(dtos);
            }
            catch { return null; }
        }
    }

    // ============ 序列化 ============
    public class NodeDto
    {
        public int id; public string type; public string note; public int x, y;
        public int w, h; public bool sz;
        public Dictionary<string, string> p;
        public List<int[]> links; // [toId, kind]
    }

    public static class FlowIO
    {
        public static List<NodeDto> ToDto(List<FNode> nodes, Dictionary<FNode, int> ids)
        {
            List<NodeDto> l = new List<NodeDto>();
            foreach (FNode n in nodes)
            {
                NodeDto d = new NodeDto();
                d.id = ids[n]; d.type = n.Type; d.note = n.Note; d.x = n.X; d.y = n.Y; d.p = n.P;
                d.w = n.W; d.h = n.H; d.sz = n.SizeSet;
                d.links = new List<int[]>();
                if (ids.Count > 0)
                    foreach (FLink lk in n.Out)
                        if (ids.ContainsKey(lk.To)) d.links.Add(new int[] { ids[lk.To], lk.Kind });
                l.Add(d);
            }
            return l;
        }
        public static List<FNode> FromDto(List<NodeDto> dtos)
        {
            List<FNode> ns = new List<FNode>();
            Dictionary<int, FNode> map = new Dictionary<int, FNode>();
            FlowTask tmp = new FlowTask();
            foreach (NodeDto d in dtos)
            {
                FNode n = new FNode(); n.Id = d.id; n.Type = d.type; n.Note = d.note ?? ""; n.X = d.x; n.Y = d.y;
                n.W = d.w > 0 ? d.w : 130; n.H = d.h > 0 ? d.h : 46; n.SizeSet = d.sz;
                n.P = d.p ?? new Dictionary<string, string>();
                ns.Add(n); map[d.id] = n;
            }
            foreach (NodeDto d in dtos)
                if (d.links != null)
                    foreach (int[] lk in d.links)
                        if (map.ContainsKey(lk[0])) map[d.id].Out.Add(new FLink(map[lk[0]], lk[1]));
            return ns;
        }
        public static string Serialize(FlowTask t)
        {
            Dictionary<FNode, int> ids = new Dictionary<FNode, int>();
            foreach (FNode n in t.Nodes) ids[n] = n.Id;
            JavaScriptSerializer js = new JavaScriptSerializer();
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["name"] = t.Name;
            root["code"] = t.Code;
            root["nodes"] = ToDto(t.Nodes, ids);
            return js.Serialize(root);
        }
        static object[] RootArray(Dictionary<string, object> root, string key)
        {
            object o = root.ContainsKey(key) ? root[key] : null;
            return AsArray(o);
        }
        static object[] AsArray(object o)
        {
            if (o is object[]) return (object[])o;
            System.Collections.IEnumerable en = o as System.Collections.IEnumerable;
            if (en == null) return new object[0];
            List<object> l = new List<object>();
            foreach (object x in en) l.Add(x);
            return l.ToArray();
        }
        public static FlowTask Deserialize(string json)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            Dictionary<string, object> root = js.Deserialize<Dictionary<string, object>>(json);
            FlowTask t = new FlowTask();
            t.Name = root.ContainsKey("name") ? (root["name"] ?? "1").ToString() : "1";
            t.Code = root.ContainsKey("code") ? (root["code"] ?? "").ToString() : "";
            object[] arr = RootArray(root, "nodes");
            List<NodeDto> dtos = new List<NodeDto>();
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                NodeDto nd = new NodeDto();
                nd.id = Convert.ToInt32(d["id"]); nd.type = d["type"].ToString();
                nd.note = d.ContainsKey("note") ? (d["note"] ?? "").ToString() : "";
                nd.x = Convert.ToInt32(d["x"]); nd.y = Convert.ToInt32(d["y"]);
                nd.p = new Dictionary<string, string>();
                if (d.ContainsKey("p") && d["p"] is Dictionary<string, object>)
                    foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)d["p"])
                        nd.p[kv.Key] = kv.Value == null ? "" : kv.Value.ToString();
                nd.links = new List<int[]>();
                if (d.ContainsKey("links"))
                    foreach (object lo in AsArray(d["links"]))
                    {
                        object[] la = AsArray(lo);
                        nd.links.Add(new int[] { Convert.ToInt32(la[0]), Convert.ToInt32(la[1]) });
                    }
                dtos.Add(nd);
            }
            t.Nodes = FromDto(dtos);
            t.SyncId();
            return t;
        }
    }

    // ============ 输入对话框 ============
    public class InputDialog : Form
    {
        public string Value = "";
        TextBox tb;
        public InputDialog(string title, string initial)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(340, 120);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;
            Label l = new Label(); l.Text = title + ":"; l.ForeColor = Theme.Text; l.Location = new Point(14, 14); l.AutoSize = true;
            tb = new TextBox(); tb.Location = new Point(14, 38); tb.Size = new Size(310, 24);
            tb.BackColor = Theme.InputBg; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Text = initial ?? "";
            DarkButton ok = new DarkButton("确定", true, 90, 28); ok.Location = new Point(140, 80);
            ok.Click += delegate { Value = tb.Text; DialogResult = DialogResult.OK; };
            DarkButton cancel = new DarkButton("取消", false, 90, 28); cancel.Location = new Point(236, 80);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(l); Controls.Add(tb); Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }
    }
}
