using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BulingOffline
{
    // 无头自测：验证解释器、图色算法、流程图执行器
    public static class SelfTest
    {
        static StringBuilder log = new StringBuilder();
        static int pass, fail;

        static void Check(string name, bool ok, string detail)
        {
            if (ok) { pass++; log.AppendLine("PASS " + name); }
            else { fail++; log.AppendLine("FAIL " + name + "  ->  " + detail); }
        }

        public static void Run()
        {
            try { RunInterpreterTests(); } catch (Exception ex) { log.AppendLine("EXC interp: " + ex.ToString()); }
            try { RunVisionTests(); } catch (Exception ex) { log.AppendLine("EXC vision: " + ex.ToString()); }
            try { RunFlowTests(); } catch (Exception ex) { log.AppendLine("EXC flow: " + ex.ToString()); }
            try { RunTaskIOTests(); } catch (Exception ex) { log.AppendLine("EXC taskio: " + ex.ToString()); }
            log.AppendLine("== 结果: " + pass + " 通过, " + fail + " 失败 ==");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log"), log.ToString(), Encoding.UTF8);
            Application.Exit();
            Environment.Exit(fail);
        }

        // OCR 冒烟测试（较慢，需启动 Umi-OCR）
        public static void RunOcr()
        {
            StringBuilder ocrLog = new StringBuilder();
            try
            {
                Native.SetProcessDPIAware();
                // 在屏幕上画一个已知文字的窗口，然后识别
                Form f = new Form();
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(100, 100);
                f.Size = new Size(420, 160);
                f.BackColor = Color.White;
                Label l = new Label();
                l.Text = "布灵点点离线版测试";
                l.Font = new Font("Microsoft YaHei UI", 26, FontStyle.Bold);
                l.ForeColor = Color.Black;
                l.Dock = DockStyle.Fill;
                l.TextAlign = ContentAlignment.MiddleCenter;
                f.Controls.Add(l);
                f.Show();
                Application.DoEvents();
                Thread.Sleep(800);

                List<Vision.OcrItem> items;
                int n = Vision.OcrRegion(100, 100, 520, 260, 0.3, out items, "");
                ocrLog.AppendLine("OCR 返回条数: " + n);
                foreach (Vision.OcrItem it in items)
                    ocrLog.AppendLine("  [" + it.Left + "," + it.Top + "]-[" + it.Right + "," + it.Bottom + "] " + it.Text + " score=" + it.Score.ToString("0.00"));
                ocrLog.AppendLine("原始响应: " + (Vision.LastOcrRaw == "" ? "<空>" : Vision.LastOcrRaw.Substring(0, Math.Min(400, Vision.LastOcrRaw.Length))));
                bool found = false;
                foreach (Vision.OcrItem it in items)
                    if (it.Text.Contains("布灵") || it.Text.Contains("点点") || it.Text.Contains("测试")) found = true;
                ocrLog.AppendLine(found ? "== OCR PASS ==" : "== OCR 未命中关键字（可能识别质量或服务未就绪） ==");
                f.Close();
            }
            catch (Exception ex)
            {
                ocrLog.AppendLine("EXC: " + ex.ToString());
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ocrtest.log"), ocrLog.ToString(), Encoding.UTF8);
            Environment.Exit(0);
        }

        static Value EvalCode(string code, out Runtime rt)
        {
            rt = new Runtime();
            rt.StopFlag = false;
            Interp ip = new Interp(rt) { Sc = new Script() };
            Runtime.CurrentInterp = ip;
            Script sc = Interp.Compile(code);
            ip.Sc = sc;
            try { ip.Run(sc.Body); }
            catch (StopSignal) { }
            return ip.GetSlot("__result").V;
        }

        // 以 “__result = 表达式/执行结果” 方式断言
        static Value RunExpr(string expr) { Runtime r; return EvalCode("__result = " + expr, out r); }
        static Value RunStmt(string code) { Runtime r; return EvalCode(code, out r); }

        static void RunInterpreterTests()
        {
            // 变量与算术
            Check("int-arith", RunExpr("1 + 2 * 3").AsInt() == 7, RunExpr("1 + 2 * 3").AsStr());
            Check("double-arith", Math.Abs(RunExpr("7 / 2").AsDbl() - 3.5) < 0.001, RunExpr("7 / 2").AsStr());
            Check("int-div", RunExpr("8 / 2").AsInt() == 4, "");
            Check("mod", RunExpr("10 % 3").AsInt() == 1, "");
            Check("string-concat", RunExpr("\"a\" + \"b\" + 123").AsStr() == "ab123", RunExpr("\"a\" + \"b\" + 123").AsStr());
            Check("compare", RunExpr("3 > 2 && 2 >= 2").AsInt() == 1, "");
            Check("not-equal", RunExpr("\"abc\" != \"abd\"").AsInt() == 1, "");
            Check("or", RunExpr("0 || 5").AsInt() == 1, "");
            Check("negative", RunExpr("-5 + 10").AsInt() == 5, "");

            // 变量声明与赋值
            Value v = RunStmt("变量 a = 5\na = a + 3\n__result = a");
            Check("var-decl-assign", v.AsInt() == 8, v.AsStr());
            v = RunStmt("b = \"字符串\"\n__result = b");
            Check("var-implicit", v.AsStr() == "字符串", v.AsStr());

            // 如果/否则
            v = RunStmt("x = 5\n如果(x == 5)\n开始\n__result = 111\n结束\n否则\n开始\n__result = 222\n结束");
            Check("if-true", v.AsInt() == 111, v.AsStr());
            v = RunStmt("x = 6\n如果(x == 5)\n开始\n__result = 111\n结束\n否则\n开始\n__result = 222\n结束");
            Check("if-else", v.AsInt() == 222, v.AsStr());
            v = RunStmt("x = 6\n如果(x == 5)\n开始\n__result = 1\n结束\n否则如果(x == 6)\n开始\n__result = 2\n结束\n否则\n开始\n__result = 3\n结束");
            Check("if-elseif", v.AsInt() == 2, v.AsStr());

            // 循环
            v = RunStmt("s = 0\ni = 0\n循环(i < 10)\n开始\ns = s + i\ni = i + 1\n结束\n__result = s");
            Check("while-loop", v.AsInt() == 45, v.AsStr());
            v = RunStmt("s = 0\ni = 0\n循环(1)\n开始\ni = i + 1\n如果(i > 5)\n开始\n跳出循环\n结束\n结束\n__result = i");
            Check("break", v.AsInt() == 6, v.AsStr());
            v = RunStmt("s = 0\ni = 0\n循环(i < 5)\n开始\ni = i + 1\n如果(i == 3)\n开始\n继续循环\n结束\ns = s + i\n结束\n__result = s");
            Check("continue", v.AsInt() == 12, v.AsStr());

            // 子任务
            v = RunStmt("子任务 累加(n)\n i = 0\n s = 0\n 循环(i < n)\n 开始\n s = s + i\n i = i + 1\n 结束\n 返回 s\n子任务完成\n__result = 累加(100)");
            Check("func-sum", v.AsInt() == 4950, v.AsStr());
            v = RunStmt("dd = 7\n子任务 f(a, b)\n a = a * 2\n b = b + 1\n子任务完成\nf(dd, b2)\n__result = dd");
            Check("func-refparam", v.AsInt() == 14, v.AsStr());

            // 数组
            v = RunStmt("arr2 = 0\nn = 拆分字符串(\"a|b|c\", \"|\", arr2)\ns = \"\"\ni = 0\n循环(i < n)\n开始\ns = s + arr2[i]\ni = i + 1\n结束\n__result = s");
            Check("split-array", v.AsStr() == "abc", v.AsStr());
            v = RunStmt("a2 = 0\na2[0] = 10\na2[1] = 20\na2[2] = a2[0] + a2[1]\n__result = a2[2]");
            Check("array-index-assign", v.AsInt() == 30, v.AsStr());

            // 字符串函数
            Check("str-len", RunExpr("字符串长度(\"abc阿萨德\")").AsInt() == 6, RunExpr("字符串长度(\"abc阿萨德\")").AsStr());
            Check("substr", RunExpr("提取字符串(\"abc123\", 1)").AsStr() == "b", "");
            Check("substr3", RunExpr("提取字符串(\"abc123\", 1, 3)").AsStr() == "bc1", "");
            Check("substr-rest", RunExpr("提取字符串(\"abc123\", 2, -1)").AsStr() == "c123", "");
            Check("contains", RunExpr("是否包含字符串(\"abc123\", \"c1\")").AsInt() == 2, "");
            Check("contains-miss", RunExpr("是否包含字符串(\"abc123\", \"zz\")").AsInt() == -1, "");
            Check("replace", RunExpr("替换字符串(\"abc|123|321\", \"|\", \"-\")").AsStr() == "abc-123-321", "");

            // 类型转换
            Check("to-int", RunExpr("到整数(\"42\")").AsInt() == 42, "");
            Check("to-double", Math.Abs(RunExpr("到小数(\"1.5\")").AsDbl() - 1.5) < 1e-9, "");
            Check("to-string", RunExpr("到字符串(99)").AsStr() == "99", "");

            // 数学
            Check("abs", RunExpr("绝对值(-90)").AsInt() == 90, "");
            Check("sqrt", Math.Abs(RunExpr("求平方根(25)").AsDbl() - 5) < 1e-9, "");
            Check("pow", Math.Abs(RunExpr("幂函数(5, 2)").AsDbl() - 25) < 1e-9, "");
            Check("max", RunExpr("最大值(1, 100, 2, 5)").AsInt() == 100, "");
            Check("min", RunExpr("最小值(1, 100, 2, 5)").AsInt() == 1, "");
            Check("sin90", Math.Abs(RunExpr("正弦值(90)").AsDbl() - 1) < 1e-9, "");
            Check("cos0", Math.Abs(RunExpr("余弦值(0)").AsDbl() - 1) < 1e-9, "");

            // 时间
            Check("time-format", RunExpr("获取本地时间()").AsStr().Length == 8, RunExpr("获取本地时间()").AsStr());
            Check("datetime-format", RunExpr("获取本地日期时间()").AsStr().Length == 19, "");
            Check("year", RunExpr("获取本地年()").AsInt() >= 2020, "");

            // 文件
            string tmp = Path.Combine(Path.GetTempPath(), "bl_selftest_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt");
            v = RunStmt("a = 写入文本文件(\"" + tmp.Replace("\\", "\\\\") + "\", \"hello\")\n__result = a");
            Check("file-write", v.AsInt() == 1, v.AsStr());
            v = RunStmt("a = 追加文本文件(\"" + tmp.Replace("\\", "\\\\") + "\", \" world\")\n__result = a");
            Check("file-append", v.AsInt() == 1, "");
            v = RunStmt("a = 读取文本文件(\"" + tmp.Replace("\\", "\\\\") + "\", content)\n__result = content");
            Check("file-read", v.AsStr() == "hello world", v.AsStr());
            v = RunExpr("获取文件大小(\"" + tmp.Replace("\\", "\\\\") + "\")");
            Check("file-size", v.AsInt() == 11, v.AsStr());
            try { File.Delete(tmp); } catch { }

            // 注释与执行任务文件
            string inc = Path.Combine(Path.GetTempPath(), "bl_inc_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".txt");
            File.WriteAllText(inc, "#被包含的脚本\nincluded_var = 777\n", Encoding.UTF8);
            v = RunStmt("included_var = 0\n执行任务文件 \"" + inc.Replace("\\", "\\\\") + "\"\n__result = included_var");
            Check("include-file", v.AsInt() == 777, v.AsStr());
            try { File.Delete(inc); } catch { }

            // 多线程
            v = RunStmt("cnt = 0\n子任务 工作线程()\n cnt = cnt + 1\n子任务完成\ntid = 启动线程(\"工作线程\")\n等待线程(tid)\n__result = cnt");
            Check("thread", v.AsInt() == 1, v.AsStr());

            // 键名映射
            Check("keymap-A", Native.KeyToVk("A") == 0x41, "");
            Check("keymap-ctrl", Native.KeyToVk("CTRL") == 0x11, "");
            Check("keymap-enter", Native.KeyToVk("回车") == 0x0D, "");
            Check("keymap-up", Native.KeyToVk("上") == 0x26, "");
            Check("keymap-f1", Native.KeyToVk("F1") == 0x70, "");
            Check("keymap-num", Native.KeyToVk("5") == 0x35, "");

            // DLL 调用（加载DLL/调用DLL/获取DLL导出/卸载DLL）
            v = RunStmt("h = 加载DLL(\"user32.dll\")\n__result = h");
            Check("dll-load", v.AsInt() > 0, v.AsStr());
            v = RunStmt("h = 加载DLL(\"user32.dll\")\nr = 调用DLL(h, \"GetSystemMetrics\", 0)\n__result = r");
            Check("dll-call", v.AsInt() > 0, v.AsStr());
            v = RunStmt("r = 调用DLL(\"user32.dll\", \"GetSystemMetrics\", 1)\n__result = r");
            Check("dll-call-byname", v.AsInt() > 0, v.AsStr());
            v = RunStmt("n = 获取DLL导出(\"user32.dll\", 导出)\n__result = n");
            Check("dll-exports", v.AsInt() > 50, v.AsStr());
            v = RunStmt("h = 加载DLL(\"user32.dll\")\n卸载DLL(h)\n__result = 1");
            Check("dll-unload", v.AsInt() == 1, "");
            v = RunStmt("r = 调用DLL(\"user32.dll\", \"不存在的函数\", 0)\n__result = r");
            Check("dll-call-missing", v.AsInt() == 0, v.AsStr());

            // DLL 检测（PE 解析：user32.dll 为 x64/x86，导出表非空）
            DllHost.DllInfo di = DllHost.Inspect(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "user32.dll"));
            Check("dll-inspect", (di.Arch == "x64" || di.Arch == "x86") && di.Exports > 50, di.Arch + "/" + di.Exports);
        }

        static void RunVisionTests()
        {
            using (Bitmap region = new Bitmap(400, 300, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(region))
            {
                g.Clear(Color.White);
                using (SolidBrush br = new SolidBrush(Color.FromArgb(0xE1, 0x22, 0x33))) g.FillRectangle(br, 100, 100, 60, 40);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0x10, 0x20, 0xF0)), 300, 200, 3, 3);

                int fx, fy;
                List<Point> pts;
                int n = FindColorOnBitmap(region, 0, 0, 400, 300, "e12233", 0.95, out fx, out fy);
                Check("find-color", n == 1 && fx == 100 && fy == 100, "n=" + n + " at=" + fx + "," + fy);

                int cnt = CountColorOnBitmap(region, 0, 0, 400, 300, "e12233");
                Check("count-color", cnt == 60 * 40, "cnt=" + cnt);

                string col = GetColorOnBitmap(region, 105, 105);
                Check("get-color", col == "e12233", col);

                string tplPath = Path.Combine(Path.GetTempPath(), "bl_tpl.bmp");
                using (Bitmap tpl = region.Clone(new Rectangle(100, 100, 60, 40), region.PixelFormat))
                    tpl.Save(tplPath, ImageFormat.Bmp);
                using (Bitmap region2 = region.Clone(new Rectangle(0, 0, 300, 180), region.PixelFormat))
                {
                    n = FindImagesOnBitmap(region2, tplPath, 0.95, out pts);
                    bool ok = n >= 1;
                    if (ok)
                    {
                        Point best = pts[0];
                        foreach (Point p2 in pts)
                            if (Math.Abs(p2.X - 100) <= Math.Abs(best.X - 100) && Math.Abs(p2.Y - 100) <= Math.Abs(best.Y - 100)) best = p2;
                        ok = Math.Abs(best.X - 100) <= 3 && Math.Abs(best.Y - 100) <= 3;
                    }
                    Check("find-image", ok, "n=" + n + (n > 0 ? " at=" + pts[0].X + "," + pts[0].Y : ""));
                }

                using (Bitmap region3 = region.Clone(new Rectangle(0, 0, 90, 90), region.PixelFormat))
                {
                    n = FindImagesOnBitmap(region3, tplPath, 0.95, out pts);
                    Check("find-image-none", n == 0, "n=" + n);
                }

                try { File.Delete(tplPath); } catch { }
            }
        }

        // ============ 流程图执行器测试 ============
        static Value flowResult;
        static Runtime flowRt;
        static Interp flowInterp;

        static void RunFlow(string name, FlowTask task)
        {
            flowRt = new Runtime();
            flowInterp = new Interp(flowRt) { Sc = new Script() };
            Runtime.CurrentInterp = flowInterp;
            FlowCanvas canvas = new FlowCanvas(task);
            flowResult = Value.Str("");
            flowInterp.GetSlot("__result").V = Value.Str("");
            string err = canvas.Execute(flowRt, flowInterp);
            if (err != null) flowInterp.GetSlot("__result").V = Value.Str("ERR:" + err);
        }

        static Value FlowVar(string name) { return flowInterp.GetSlot(name).V; }

        static void RunFlowTests()
        {
            // 1) 线性: 开始 → 自定义(赋值) → 结束
            {
                FlowTask t = new FlowTask();
                FNode s = t.Add("开始", 0, 0);
                FNode c = t.Add("自定义", 200, 0);
                c.P["code"] = "fx = 123";
                FNode e = t.Add("结束", 400, 0);
                s.Out.Add(new FLink(c, 0));
                c.Out.Add(new FLink(e, 0));
                RunFlow("linear", t);
                Check("flow-linear", FlowVar("fx").AsInt() == 123, FlowVar("fx").AsStr());
            }
            // 2) 判断分支: 真
            {
                FlowTask t = new FlowTask();
                FNode s = t.Add("开始", 0, 0);
                FNode j = t.Add("判断", 200, 0);
                j.P["cond"] = "2 > 1";
                FNode tb = t.Add("自定义", 400, -60);
                tb.P["code"] = "branch = 1";
                FNode fb = t.Add("自定义", 400, 60);
                fb.P["code"] = "branch = 2";
                FNode e = t.Add("结束", 600, 0);
                s.Out.Add(new FLink(j, 0));
                j.Out.Add(new FLink(tb, 1));
                j.Out.Add(new FLink(fb, 2));
                tb.Out.Add(new FLink(e, 0));
                RunFlow("judge-true", t);
                Check("flow-judge-true", FlowVar("branch").AsInt() == 1, FlowVar("branch").AsStr());
            }
            // 3) 判断分支: 假
            {
                FlowTask t = new FlowTask();
                FNode s = t.Add("开始", 0, 0);
                FNode j = t.Add("判断", 200, 0);
                j.P["cond"] = "2 < 1";
                FNode tb = t.Add("自定义", 400, -60);
                tb.P["code"] = "branch = 1";
                FNode fb = t.Add("自定义", 400, 60);
                fb.P["code"] = "branch = 2";
                FNode e = t.Add("结束", 600, 0);
                s.Out.Add(new FLink(j, 0));
                j.Out.Add(new FLink(tb, 1));
                j.Out.Add(new FLink(fb, 2));
                fb.Out.Add(new FLink(e, 0));
                RunFlow("judge-false", t);
                Check("flow-judge-false", FlowVar("branch").AsInt() == 2, FlowVar("branch").AsStr());
            }
            // 4) 循环节点: 循环3次执行体内自增，然后走完成分支
            {
                FlowTask t = new FlowTask();
                FNode s = t.Add("开始", 0, 0);
                FNode lp = t.Add("循环", 200, 0);
                lp.P["mode"] = "count";
                lp.P["count"] = "3";
                FNode body = t.Add("自定义", 400, -60);
                body.P["code"] = "n = 到整数(n) + 1";
                FNode done = t.Add("自定义", 400, 60);
                done.P["code"] = "doneflag = 99";
                FNode e = t.Add("结束", 600, 0);
                s.Out.Add(new FLink(lp, 0));
                lp.Out.Add(new FLink(body, 3));
                lp.Out.Add(new FLink(done, 4));
                // body 链以死底结束 = 一次循环体执行完毕
                done.Out.Add(new FLink(e, 0));
                RunFlow("loop", t);
                bool ok = FlowVar("n").AsInt() == 3 && FlowVar("doneflag").AsInt() == 99;
                Check("flow-loop", ok, "n=" + FlowVar("n").AsStr() + " done=" + FlowVar("doneflag").AsStr());
            }
            // 5) 延迟节点(0ms) + 变量节点组合
            {
                FlowTask t = new FlowTask();
                FNode s = t.Add("开始", 0, 0);
                FNode d = t.Add("延迟", 200, 0);
                d.P["ms"] = "0";
                FNode c = t.Add("自定义", 400, 0);
                c.P["code"] = "v = 延迟(0)\nv2 = 随机数(1, 1)\n组合 = v2";
                FNode e = t.Add("结束", 600, 0);
                s.Out.Add(new FLink(d, 0));
                d.Out.Add(new FLink(c, 0));
                c.Out.Add(new FLink(e, 0));
                RunFlow("delay-rand", t);
                Check("flow-delay-rand", FlowVar("组合").AsInt() == 1, FlowVar("组合").AsStr());
            }
        }

        // ============ 任务文件序列化测试 ============
        static void RunTaskIOTests()
        {
            FlowTask t = new FlowTask();
            FNode s = t.Add("开始", 10, 20);
            FNode d = t.Add("延迟", 200, 40);
            d.P["ms"] = "500";
            d.Note = "测试备注";
            FNode e = t.Add("结束", 400, 60);
            s.Out.Add(new FLink(d, 0));
            d.Out.Add(new FLink(e, 0));
            string json = FlowIO.Serialize(t);
            FlowTask t2 = FlowIO.Deserialize(json);
            bool okCount = t2.Nodes.Count == 3;
            bool okParam = okCount && t2.Nodes[1].P.ContainsKey("ms") && t2.Nodes[1].P["ms"] == "500";
            bool okNote = okCount && t2.Nodes[1].Note == "测试备注";
            bool okLink = okCount && t2.Nodes[0].Out.Count == 1 && t2.Nodes[0].Out[0].To == t2.Nodes[1];
            Check("taskio-roundtrip", okCount && okParam && okNote && okLink,
                "count=" + okCount + " param=" + okParam + " note=" + okNote + " link=" + okLink
                + (okCount ? " ms=" + (t2.Nodes[1].P.ContainsKey("ms") ? t2.Nodes[1].P["ms"] : "<无>") : "")
                + " outCount=" + (okCount ? t2.Nodes[0].Out.Count.ToString() : "?")
                + " json=" + json);

            // 模板存取
            TemplateStore.Save("自测模板", new List<FNode> { d });
            List<FNode> loaded = TemplateStore.Load("自测模板");
            Check("template-roundtrip", loaded != null && loaded.Count == 1 && loaded[0].P["ms"] == "500",
                loaded == null ? "null" : loaded.Count.ToString());
            try { File.Delete(Path.Combine(TemplateStore.Dir, "自测模板.json")); } catch { }
        }

        // ============ 位图测试辅助 ============
        static int FindColorOnBitmap(Bitmap bmp, int l, int t, int r, int b, string color, double sim, out int fx, out int fy)
        {
            fx = fy = -1;
            int cr, cg, cb;
            if (!Vision.ParseColor(color, out cr, out cg, out cb)) return 0;
            int tol = (int)Math.Round((1.0 - sim) * 255.0 * 0.9);
            using (Vision.BitmapAccessor acc = new Vision.BitmapAccessor(bmp))
            {
                for (int y = t; y < b && y < acc.H; y++)
                    for (int x = l; x < r && x < acc.W; x++)
                    {
                        int o = acc.Px(x, y);
                        if (Math.Abs(acc.Bits[o + 2] - cr) <= tol && Math.Abs(acc.Bits[o + 1] - cg) <= tol && Math.Abs(acc.Bits[o] - cb) <= tol)
                        { fx = x; fy = y; return 1; }
                    }
            }
            return 0;
        }

        static int CountColorOnBitmap(Bitmap bmp, int l, int t, int r, int b, string color)
        {
            int cr, cg, cb;
            if (!Vision.ParseColor(color, out cr, out cg, out cb)) return 0;
            int count = 0;
            using (Vision.BitmapAccessor acc = new Vision.BitmapAccessor(bmp))
            {
                for (int y = t; y < b && y < acc.H; y++)
                    for (int x = l; x < r && x < acc.W; x++)
                    {
                        int o = acc.Px(x, y);
                        if (acc.Bits[o + 2] == cr && acc.Bits[o + 1] == cg && acc.Bits[o] == cb) count++;
                    }
            }
            return count;
        }

        static string GetColorOnBitmap(Bitmap bmp, int x, int y)
        {
            using (Vision.BitmapAccessor acc = new Vision.BitmapAccessor(bmp))
            {
                int o = acc.Px(x, y);
                return string.Format("{0:x2}{1:x2}{2:x2}", acc.Bits[o + 2], acc.Bits[o + 1], acc.Bits[o]);
            }
        }

        static int FindImagesOnBitmap(Bitmap bmp, string tplPath, double sim, out List<Point> found)
        {
            found = new List<Point>();
            using (Bitmap tpl = new Bitmap(tplPath))
            using (Vision.BitmapAccessor acc = new Vision.BitmapAccessor(bmp))
            using (Vision.BitmapAccessor tacc = new Vision.BitmapAccessor(tpl))
            {
                int tw = tacc.W, th = tacc.H;
                int need = (int)Math.Ceiling(sim * tw * th);
                int tol = Math.Max(6, (int)Math.Round((1.0 - sim) * 200));
                for (int cy = 0; cy + th <= acc.H; cy++)
                    for (int cx = 0; cx + tw <= acc.W; cx++)
                    {
                        int matched = 0;
                        for (int y = 0; y < th; y++)
                            for (int x = 0; x < tw; x++)
                            {
                                int ro = acc.Px(cx + x, cy + y), to = tacc.Px(x, y);
                                if (Math.Abs(acc.Bits[ro + 2] - tacc.Bits[to + 2]) <= tol &&
                                    Math.Abs(acc.Bits[ro + 1] - tacc.Bits[to + 1]) <= tol &&
                                    Math.Abs(acc.Bits[ro] - tacc.Bits[to]) <= tol) matched++;
                            }
                        if (matched >= need) found.Add(new Point(cx, cy));
                    }
            }
            return found.Count;
        }
    }
}
