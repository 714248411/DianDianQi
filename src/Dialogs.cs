using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BulingOffline
{
    // ============ 捐赠 / 关于 / 更新说明 ============
    public class DonationForm : Form
    {
        public DonationForm()
        {
            Text = "捐赠支持";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 590);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;

            Label title = MakeLabel("感谢支持点点器", Theme.FontUIB, Theme.Text, new Point(0, 18), new Size(520, 30));
            title.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(title);

            Label tip = MakeLabel("如果本软件为你节省了时间，可以自愿扫码支持开发。\n捐赠完全自愿，不影响任何功能的使用。", Theme.FontUI, Theme.TextDim, new Point(30, 54), new Size(460, 48));
            tip.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(tip);

            PictureBox qr = new PictureBox();
            qr.Location = new Point(35, 112);
            qr.Size = new Size(450, 410);
            qr.SizeMode = PictureBoxSizeMode.Zoom;
            qr.BackColor = Color.White;
            qr.BorderStyle = BorderStyle.FixedSingle;
            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "donation_qr.png");
            try
            {
                if (File.Exists(imagePath))
                {
                    using (Image source = Image.FromFile(imagePath)) qr.Image = new Bitmap(source);
                }
            }
            catch { }
            Controls.Add(qr);

            if (qr.Image == null)
            {
                Label missing = MakeLabel("未找到捐赠图片：\nassets\\donation_qr.png", Theme.FontUI, Color.FromArgb(160, 40, 40), new Point(55, 275), new Size(410, 55));
                missing.TextAlign = ContentAlignment.MiddleCenter;
                Controls.Add(missing);
                missing.BringToFront();
            }

            Label safety = MakeLabel("请在付款前核对收款人信息，谨防误扫或转错。", Theme.FontSmall, Theme.TextDim, new Point(35, 531), new Size(450, 20));
            safety.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(safety);

            DarkButton close = new DarkButton("关闭", true, 84, 30);
            close.Location = new Point(401, 555);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            CancelButton = close;
        }

        static Label MakeLabel(string text, Font font, Color color, Point location, Size size)
        {
            Label label = new Label();
            label.Text = text; label.Font = font; label.ForeColor = color;
            label.Location = location; label.Size = size;
            return label;
        }
    }

    public class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "关于点点器";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(500, 430);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;

            Label product = LabelOf("点点器", new Font("Microsoft YaHei UI", 20, FontStyle.Bold), Theme.Text, 22, 44);
            product.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(product);
            Label version = LabelOf("言逸点点离线版 · 2026.10.04", Theme.FontUIB, Theme.Accent, 67, 24);
            version.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(version);
            Label summary = LabelOf("面向 Windows 的本地自动化与可视化流程工具。\n无需登录或联网验证，任务与设置均保存在本机。", Theme.FontUI, Theme.TextDim, 102, 50);
            summary.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(summary);

            Panel card = new Panel();
            card.Location = new Point(40, 165); card.Size = new Size(420, 164);
            card.BackColor = Theme.InputBg;
            Controls.Add(card);
            card.Controls.Add(CardLabel("作者 / 贡献者", "YanYi", 18));
            card.Controls.Add(CardLabel("联系 QQ", "714248411", 57));
            card.Controls.Add(CardLabel("QQ 交流群", "84284936", 96));
            Label note = new Label();
            note.Text = "反馈问题时请附上复现步骤、系统版本与截图。";
            note.ForeColor = Theme.TextDim; note.Font = Theme.FontSmall;
            note.Location = new Point(18, 132); note.Size = new Size(385, 20);
            card.Controls.Add(note);

            Label copyright = LabelOf("Copyright © 2026 YanYi", Theme.FontSmall, Theme.TextDim, 345, 22);
            copyright.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(copyright);

            DarkButton close = new DarkButton("关闭", true, 84, 30);
            close.Location = new Point(376, 386);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            CancelButton = close;
        }

        Label LabelOf(string text, Font font, Color color, int y, int height)
        {
            Label label = new Label();
            label.Text = text; label.Font = font; label.ForeColor = color;
            label.Location = new Point(30, y); label.Size = new Size(440, height);
            return label;
        }

        Control CardLabel(string caption, string value, int y)
        {
            Panel row = new Panel(); row.Location = new Point(18, y); row.Size = new Size(385, 28);
            Label left = new Label(); left.Text = caption; left.ForeColor = Theme.TextDim; left.Location = new Point(0, 3); left.Size = new Size(125, 22);
            Label right = new Label(); right.Text = value; right.ForeColor = Theme.Text; right.Font = Theme.FontUIB; right.Location = new Point(135, 3); right.Size = new Size(240, 22);
            row.Controls.Add(left); row.Controls.Add(right);
            return row;
        }
    }

    public class UpdateNotesForm : Form
    {
        public UpdateNotesForm()
        {
            Text = "更新说明";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(650, 520);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;

            Label title = new Label();
            title.Text = "点点器更新说明 · 2026.10.04";
            title.Font = Theme.FontUIB; title.ForeColor = Theme.Text;
            title.Location = new Point(20, 16); title.Size = new Size(500, 28);
            Controls.Add(title);

            RichTextBox notes = new RichTextBox();
            notes.Location = new Point(20, 52); notes.Size = new Size(610, 410);
            notes.BorderStyle = BorderStyle.FixedSingle; notes.ReadOnly = true;
            notes.BackColor = Theme.InputBg; notes.ForeColor = Theme.Text;
            notes.Font = Theme.FontUI;
            notes.Text =
                "本次更新\n\n" +
                "• 新增独立的“捐赠”入口，内置展示微信 / 支付宝捐赠二维码，并加入付款安全提示。\n\n" +
                "• 新增并完善“关于”页面，统一产品说明、版本信息和联系信息。\n\n" +
                "• 关于页面仅保留作者 YanYi、联系 QQ 714248411 和 QQ 交流群 84284936，清理无关旧资料。\n\n" +
                "• 新增“更新说明”入口，便于直接查看每次版本变化。\n\n" +
                "• 完善项目 README、更新日志和仓库忽略规则，方便从 GitHub / Gitee 获取源码并自行构建。\n\n" +
                "• 继续保留离线运行、流程图编辑、多功能代码、录制、图色、OCR 与 DLL 调用等能力。";
            Controls.Add(notes);

            DarkButton close = new DarkButton("关闭", true, 84, 30);
            close.Location = new Point(546, 477);
            close.Click += delegate { Close(); };
            Controls.Add(close);
            CancelButton = close;
        }
    }

    // ============ 免责声明 ============
    public class DisclaimerForm : Form
    {
        public DisclaimerForm()
        {
            Text = "布灵点点-高级版";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 360);
            BackColor = Color.FromArgb(24, 24, 32);
            Font = Theme.FontUI;

            Label title = new Label();
            title.Text = "免责声明";
            title.ForeColor = Theme.Text; title.Font = Theme.FontUIB;
            title.Location = new Point(20, 14); title.AutoSize = true;
            Controls.Add(title);

            RichTextBox body = new RichTextBox();
            body.BorderStyle = BorderStyle.None;
            body.BackColor = Color.FromArgb(24, 24, 32);
            body.ForeColor = Theme.Text;
            body.ReadOnly = true;
            body.Font = Theme.FontUI;
            body.Location = new Point(20, 44);
            body.Size = new Size(580, 240);
            body.Text = "1、本软件仅供学习和研究目的，作者不对该软件的使用后屏幕承担任何责任。\n用户使用本软件所产生的一切后果需自行承担，与作者无关。\n\n2、用户在使用本软件前应仔细阅读本声明，并同意自行承担使用本软件的所有风险。\n如果用户不同意本声明的任何内容，应立即停止使用本软件。\n\n3、本软件是作者根据自身的学识和实践成果开发的，可能存在不足之处。\n4、用户在使用本软件时，应遵守国家相关法律法规，不得利用本软件从事任何违法活动。\n对于用户使用本软件违反国家法律法规的行为，作者不承担任何责任。\n\n5、用户一旦开始使用本软件，则视为完全理解并接受本声明的所有内容。\n6、作者保留对本软件的最终解释权。";
            Controls.Add(body);

            DarkButton ok = new DarkButton("同意", true, 84, 30);
            ok.Location = new Point(430, 306);
            ok.Click += delegate { DialogResult = DialogResult.OK; };
            DarkButton no = new DarkButton("不同意", false, 84, 30);
            no.Location = new Point(524, 306);
            no.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(ok); Controls.Add(no);
            // 不设默认按钮：回车不会默认同意，必须显式点击“同意”
            CancelButton = no;
        }
    }

    // ============ 设置 ============
    public class SettingsForm : Form
    {
        public string HotStart = "F9";
        public string HotStop = "F10";
        ComboBox cbStart, cbStop;

        public SettingsForm(string hs, string ht)
        {
            HotStart = hs; HotStop = ht;
            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 170);
            BackColor = Theme.Panel;
            Font = Theme.FontUI;

            Label l1 = new Label(); l1.Text = "启动/重复执行热键:"; l1.ForeColor = Theme.Text; l1.AutoSize = true; l1.Location = new Point(20, 24);
            cbStart = HotkeyCombo(); cbStart.Location = new Point(180, 20);
            Label l2 = new Label(); l2.Text = "停止脚本热键:"; l2.ForeColor = Theme.Text; l2.AutoSize = true; l2.Location = new Point(20, 62);
            cbStop = HotkeyCombo(); cbStop.Location = new Point(180, 58);
            if (cbStart.Items.Contains(hs)) cbStart.SelectedItem = hs;
            if (cbStop.Items.Contains(ht)) cbStop.SelectedItem = ht;

            Label tip = new Label();
            tip.Text = "热键作用于当前视图的任务（执行=按重复次数执行，再次按停止）";
            tip.ForeColor = Theme.TextDim; tip.AutoSize = true; tip.Location = new Point(20, 96);

            DarkButton ok = new DarkButton("确定", true, 84, 30); ok.Location = new Point(160, 128);
            ok.Click += delegate { HotStart = cbStart.Text; HotStop = cbStop.Text; DialogResult = DialogResult.OK; };
            DarkButton cancel = new DarkButton("取消", false, 84, 30); cancel.Location = new Point(256, 128);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            Controls.Add(l1); Controls.Add(cbStart); Controls.Add(l2); Controls.Add(cbStop); Controls.Add(tip);
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }
        ComboBox HotkeyCombo()
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string k in new string[] { "F9", "F10", "F11", "F12", "F8", "F7" }) c.Items.Add(k);
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Theme.InputBg; c.ForeColor = Theme.Text;
            return c;
        }
    }

    // ============ 窗口绑定准星 ============
    public class PickerOverlay : Form
    {
        public IntPtr Picked = IntPtr.Zero;
        public Point PickedPoint = Point.Empty;
        Label tip;

        public PickerOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.25;
            Cursor = Cursors.Cross;
            tip = new Label();
            tip.Text = "点击要绑定的窗口（右键取消）";
            tip.ForeColor = Color.White;
            tip.Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold);
            tip.AutoSize = true;
            Controls.Add(tip);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            tip.Location = new Point((Width - tip.Width) / 2, 60);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Right) { Close(); return; }
            Native.POINT p; Native.GetCursorPos(out p);
            PickedPoint = new Point(p.X, p.Y);
            // 排除自身进程窗口，取该点最顶层的其它窗口（修复拾取到遮罩自身的问题）
            IntPtr h = Native.TopmostWindowAtPoint(p, System.Diagnostics.Process.GetCurrentProcess().Id);
            Picked = h;
            DialogResult = DialogResult.OK;
            Close();
        }
        protected override bool ProcessKeyPreview(ref Message m) { return false; }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); }
    }

    // ============ 录制 ============
    // 已迁移到 src/Recorder.cs 的 RecorderForm（智能录制，录制完直接转多功能代码）
}
