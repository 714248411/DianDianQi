using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BulingOffline
{
    // 深色右键菜单（对照原版样式）
    public class DarkMenu : Form
    {
        public class Entry
        {
            public string Text;
            public Action OnClick;
            public bool Separator;
        }

        public List<Entry> Entries = new List<Entry>();
        int hover = -1;
        const int RowH = 26, SepH = 9, W = 148;

        public DarkMenu()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Panel2;
            TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public Entry Add(string text, Action onClick)
        {
            Entry e = new Entry(); e.Text = text; e.OnClick = onClick;
            Entries.Add(e);
            return e;
        }
        public void AddSep()
        {
            Entry e = new Entry(); e.Separator = true;
            Entries.Add(e);
        }

        int RowY(int i)
        {
            int y = 4;
            for (int k = 0; k < i; k++) y += Entries[k].Separator ? SepH : RowH;
            return y;
        }
        int TotalH()
        {
            int y = 8;
            foreach (Entry e in Entries) y += e.Separator ? SepH : RowH;
            return y;
        }
        int HitRow(Point p)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                int h = Entries[i].Separator ? SepH : RowH;
                if (p.Y >= RowY(i) && p.Y < RowY(i) + h) return i;
            }
            return -1;
        }

        public void ShowAt(Control anchor, Point screenPos)
        {
            int h = TotalH();
            Rectangle wa = Screen.FromControl(anchor).WorkingArea;
            int x = screenPos.X, y = screenPos.Y;
            if (x + W > wa.Right) x = wa.Right - W;
            if (y + h > wa.Bottom) y = wa.Bottom - h;
            Location = new Point(x, y);
            Size = new Size(W, h);
            Show();
            Activate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.Panel2);
            using (Pen p = new Pen(Theme.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry en = Entries[i];
                if (en.Separator)
                {
                    using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, 8, RowY(i) + SepH / 2, W - 8, RowY(i) + SepH / 2);
                    continue;
                }
                Rectangle r = new Rectangle(2, RowY(i), W - 4, RowH);
                if (i == hover)
                {
                    using (SolidBrush b = new SolidBrush(Theme.AccentDim)) g.FillRectangle(b, r);
                }
                TextRenderer.DrawText(g, en.Text, Theme.FontUI, new Point(14, RowY(i) + 4), Theme.Text);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitRow(e.Location);
            if (h != hover)
            {
                hover = (h >= 0 && !Entries[h].Separator) ? h : -1;
                Invalidate();
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = HitRow(e.Location);
            if (h >= 0 && !Entries[h].Separator && Entries[h].OnClick != null)
            {
                Close();
                Entries[h].OnClick();
            }
            else if (h >= 0 && Entries[h].Separator) { }
        }
        protected override void OnDeactivate(EventArgs e)
        {
            Close();
        }
        protected override bool ShowWithoutActivation { get { return false; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // 工具窗口，不进任务栏
                return cp;
            }
        }
    }
}
