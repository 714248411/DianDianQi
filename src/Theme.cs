using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BulingOffline
{
    // 深色主题（对照原版截图配色）
    public static class Theme
    {
        public static readonly Color Bg        = Color.FromArgb(16, 16, 24);     // 窗口背景
        public static readonly Color Panel     = Color.FromArgb(22, 22, 32);     // 面板
        public static readonly Color Panel2    = Color.FromArgb(28, 28, 40);     // 次级面板
        public static readonly Color NodeBg    = Color.FromArgb(38, 38, 52);     // 节点背景
        public static readonly Color NodeBorder= Color.FromArgb(70, 70, 92);
        public static readonly Color Accent    = Color.FromArgb(47, 107, 255);   // 执行按钮蓝
        public static readonly Color AccentDim = Color.FromArgb(42, 42, 56);
        public static readonly Color Pink      = Color.FromArgb(255, 62, 108);   // 粉红强调（边框/备注/关闭）
        public static readonly Color Text      = Color.FromArgb(232, 232, 240);
        public static readonly Color TextDim   = Color.FromArgb(150, 150, 170);
        public static readonly Color Line      = Color.FromArgb(50, 50, 66);
        public static readonly Color InputBg   = Color.FromArgb(36, 36, 48);
        public static readonly Color Green     = Color.FromArgb(70, 200, 120);
        public static readonly Color CanvasBg  = Color.FromArgb(19, 19, 28);

        public static readonly Font FontUI   = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font FontUIB  = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        public static readonly Font FontSmall= new Font("Microsoft YaHei UI", 8.25f);
        public static readonly Font FontCode = new Font("Microsoft YaHei UI", 9.5f);
        public static readonly Font FontTitle= new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);

        public static void StyleButton(Button b, bool accent)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = accent ? Accent : AccentDim;
            b.ForeColor = accent ? Color.White : Text;
            b.Font = FontUI;
            b.Cursor = Cursors.Hand;
            b.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(72, 128, 255) : Color.FromArgb(48, 48, 64);
            b.FlatAppearance.MouseDownBackColor = accent ? Color.FromArgb(30, 84, 220) : Color.FromArgb(56, 56, 74);
        }

        public static void StyleInput(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = InputBg;
            t.ForeColor = Text;
            t.Font = FontUI;
        }

        public static void StyleCombo(ComboBox c)
        {
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = InputBg;
            c.ForeColor = Text;
            c.Font = FontUI;
        }

        // 简易分组面板（带左上角标题，类似原版“任务操作”样式）
        public static Panel Group(string title, int w, int h)
        {
            Panel p = new Panel();
            p.Size = new Size(w, h);
            p.BackColor = Panel;
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Line)) g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                Size ts = TextRenderer.MeasureText(title, FontSmall);
                using (Brush b = new SolidBrush(Panel)) g.FillRectangle(b, 8, 0, ts.Width + 4, 12);
                TextRenderer.DrawText(g, title, FontSmall, new Point(10, -1), TextDim);
            };
            return p;
        }
    }

    // 可拖拽分隔条（调整相邻面板宽度）
    public class Divider : Control
    {
        public Action<int> OnDrag;                 // 拖动回调（dx 像素）
        bool dragging;
        Point down;

        public Divider()
        {
            Width = 4;
            BackColor = Theme.Bg;
            Cursor = Cursors.VSplit;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Line)) e.Graphics.DrawLine(p, 1, 0, 1, Height);
            };
            MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; down = e.Location; } };
            MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (dragging && OnDrag != null) { int dx = e.X - down.X; if (dx != 0) { down = new Point(e.X, e.Y); OnDrag(dx); } }
            };
            MouseUp += delegate(object s, MouseEventArgs e) { dragging = false; };
        }
    }

    // 圆角深色按钮
    public class DarkButton : Control, System.Windows.Forms.IButtonControl
    {
        public bool AccentStyle;
        bool hover;
        DialogResult dialogResult;
        public DialogResult DialogResult
        {
            get { return dialogResult; }
            set { dialogResult = value; }
        }
        public void NotifyDefault(bool value) { Invalidate(); }
        public void PerformClick() { OnClick(EventArgs.Empty); }
        public DarkButton(string text, bool accent, int w, int h)
        {
            Text = text; AccentStyle = accent;
            Size = new Size(w, h);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserMouse | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.FontUI;
            MouseEnter += delegate { hover = true; Invalidate(); };
            MouseLeave += delegate { hover = false; Invalidate(); };
            GotFocus += delegate { Invalidate(); };
            LostFocus += delegate { Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Theme.Bg : Parent.BackColor);
            Color bg = AccentStyle ? Theme.Accent : Theme.AccentDim;
            if (!Enabled) bg = Color.FromArgb(30, 30, 40);
            else if (hover) bg = AccentStyle ? Color.FromArgb(72, 128, 255) : Color.FromArgb(48, 48, 64);
            using (GraphicsPath gp = Round(ClientRectangle, 4))
            {
                using (SolidBrush br = new SolidBrush(bg)) g.FillPath(br, gp);
                if (Focused)
                {
                    using (Pen p = new Pen(Color.FromArgb(120, 130, 170, 255))) g.DrawPath(p, gp);
                }
            }
            Color fg = Enabled ? (AccentStyle ? Color.White : Theme.Text) : Theme.TextDim;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            if (r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
