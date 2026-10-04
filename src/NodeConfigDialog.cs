using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BulingOffline
{
    // 节点配置对话框（按类型生成字段）
    public class NodeConfigDialog : Form
    {
        FNode node;
        Dictionary<string, TextBox> boxes = new Dictionary<string, TextBox>();
        Dictionary<string, ComboBox> combos = new Dictionary<string, ComboBox>();
        Dictionary<string, CheckBox> checks = new Dictionary<string, CheckBox>();
        Dictionary<string, RichTextBox> areas = new Dictionary<string, RichTextBox>();

        class Field
        {
            public string Key, Label;
            public string[] Options;      // 下拉
            public bool Multiline;
            public int Width = 200;
        }

        public NodeConfigDialog(FNode n)
        {
            node = n;
            Text = n.Type + "节点配置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            Font = Theme.FontUI;

            List<Field> fields = FieldsFor(n);
            int y = 16, x1 = 16, x2 = 110, maxRight = 330;
            foreach (Field f in fields)
            {
                Label l = new Label();
                l.Text = f.Label + ":";
                l.ForeColor = Theme.Text;
                l.AutoSize = true;
                l.Location = new Point(x1, y + 3);
                Controls.Add(l);
                if (f.Options != null)
                {
                    ComboBox c = new ComboBox();
                    c.DropDownStyle = ComboBoxStyle.DropDownList;
                    foreach (string o in f.Options) c.Items.Add(o);
                    string cur = node.G(f.Key, f.Options[0]);
                    c.SelectedItem = cur;
                    if (c.SelectedIndex < 0) c.SelectedIndex = 0;
                    c.Location = new Point(x2, y);
                    c.Size = new Size(f.Width, 24);
                    c.FlatStyle = FlatStyle.Flat;
                    c.BackColor = Theme.InputBg; c.ForeColor = Theme.Text;
                    combos[f.Key] = c;
                    Controls.Add(c);
                    y += 30;
                }
                else if (f.Multiline)
                {
                    RichTextBox tb = new RichTextBox();
                    tb.Location = new Point(x2, y);
                    tb.Size = new Size(f.Width, 140);
                    tb.BackColor = Theme.InputBg; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
                    tb.Font = Theme.FontCode;
                    tb.Text = node.G(f.Key, "");
                    tb.AcceptsTab = true;
                    areas[f.Key] = tb;
                    Controls.Add(tb);
                    y += 150;
                    maxRight = Math.Max(maxRight, x2 + f.Width);
                }
                else
                {
                    TextBox tb = new TextBox();
                    tb.Location = new Point(x2, y);
                    tb.Size = new Size(f.Width, 24);
                    tb.BackColor = Theme.InputBg; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
                    tb.Text = node.G(f.Key, "");
                    boxes[f.Key] = tb;
                    Controls.Add(tb);
                    y += 30;
                }
            }
            y += 8;
            ClientSize = new Size(Math.Max(340, maxRight + 20), Math.Max(140, y + 44));
            DarkButton ok = new DarkButton("确定", true, 90, 30);
            ok.Location = new Point(ClientSize.Width - 200, y);
            ok.Click += delegate { Save(); DialogResult = DialogResult.OK; };
            DarkButton cancel = new DarkButton("取消", false, 90, 30);
            cancel.Location = new Point(ClientSize.Width - 104, y);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        List<Field> FieldsFor(FNode n)
        {
            List<Field> f = new List<Field>();
            switch (n.Type)
            {
                case "延迟":
                    f.Add(F("ms", "延迟时间(ms)"));
                    break;
                case "鼠标":
                    f.Add(F("act", "操作", new string[] { "鼠标移动", "相对移动", "鼠标左键单击", "鼠标左键双击", "鼠标左键按下", "鼠标左键抬起", "鼠标右键单击", "鼠标右键双击", "鼠标右键按下", "鼠标右键抬起", "鼠标中键单击", "鼠标中键双击", "鼠标中键按下", "鼠标中键抬起", "鼠标向上滚动", "鼠标向下滚动" }));
                    f.Add(F("x", "x坐标(-1不移动)")); f.Add(F("y", "y坐标"));
                    f.Add(F("rl", "随机左偏移")); f.Add(F("rt", "随机上偏移"));
                    f.Add(F("rr", "随机右偏移")); f.Add(F("rb", "随机下偏移"));
                    f.Add(F("count", "执行次数")); f.Add(F("delay", "每次延迟(ms)"));
                    break;
                case "键盘":
                    f.Add(F("act", "操作", new string[] { "单击", "按下", "抬起" }));
                    f.Add(F("key", "按键名(A/CTRL/回车...)"));
                    f.Add(F("count", "次数")); f.Add(F("delay", "间隔(ms)"));
                    break;
                case "找图":
                    f.Add(F("img", "图片名"));
                    f.Add(F("l", "范围左")); f.Add(F("t", "范围上")); f.Add(F("r", "范围右")); f.Add(F("b", "范围下"));
                    f.Add(F("devi", "偏色(000000)")); f.Add(F("sim", "相似度0.5-1"));
                    f.Add(F("vx", "x存入变量")); f.Add(F("vy", "y存入变量"));
                    break;
                case "找色":
                    f.Add(F("color", "颜色(rrggbb)"));
                    f.Add(F("l", "范围左")); f.Add(F("t", "范围上")); f.Add(F("r", "范围右")); f.Add(F("b", "范围下"));
                    f.Add(F("sim", "相似度0.5-1"));
                    f.Add(F("vx", "x存入变量")); f.Add(F("vy", "y存入变量"));
                    break;
                case "AI识图":
                    f.Add(F("mode", "模式", new string[] { "文本识别", "文本查找" }));
                    f.Add(F("text", "查找文本(|分隔多个)"));
                    f.Add(F("l", "范围左")); f.Add(F("t", "范围上")); f.Add(F("r", "范围右")); f.Add(F("b", "范围下"));
                    f.Add(F("conf", "置信度0.1-1"));
                    f.Add(F("vt", "文本存入变量"));
                    break;
                case "找字":
                    f.Add(F("text", "查找文本(|分隔多个)"));
                    f.Add(F("l", "范围左")); f.Add(F("t", "范围上")); f.Add(F("r", "范围右")); f.Add(F("b", "范围下"));
                    f.Add(F("conf", "置信度0.1-1"));
                    f.Add(F("vt", "文本存入变量"));
                    break;
                case "判断":
                    f.Add(F2("cond", "条件表达式", 240));
                    break;
                case "循环":
                    f.Add(F("mode", "方式", new string[] { "count", "cond" }));
                    f.Add(F("count", "循环次数"));
                    f.Add(F2("cond", "条件表达式(cond方式)", 240));
                    break;
                case "自定义":
                    f.Add(FM("code", "脚本代码"));
                    break;
                default:
                    break;
            }
            return f;
        }
        Field F(string k, string label) { Field f = new Field(); f.Key = k; f.Label = label; return f; }
        Field F(string k, string label, string[] opts) { Field f = new Field(); f.Key = k; f.Label = label; f.Options = opts; return f; }
        Field F2(string k, string label, int w) { Field f = new Field(); f.Key = k; f.Label = label; f.Width = w; return f; }
        Field FM(string k, string label) { Field f = new Field(); f.Key = k; f.Label = label; f.Multiline = true; f.Width = 320; return f; }

        void Save()
        {
            foreach (KeyValuePair<string, TextBox> kv in boxes) node.P[kv.Key] = kv.Value.Text;
            foreach (KeyValuePair<string, ComboBox> kv in combos) node.P[kv.Key] = kv.Value.Text;
            foreach (KeyValuePair<string, CheckBox> kv in checks) node.P[kv.Key] = kv.Value.Checked ? "1" : "0";
            foreach (KeyValuePair<string, RichTextBox> kv in areas) node.P[kv.Key] = kv.Value.Text;
        }

        public static void Show(Control owner, FNode n)
        {
            if (n.Type == "开始" || n.Type == "结束") return;
            using (NodeConfigDialog d = new NodeConfigDialog(n))
            {
                d.ShowDialog(owner.FindForm());
            }
        }
    }
}
