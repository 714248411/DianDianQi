using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BulingOffline
{
    // 图色识别 + 离线OCR（Umi-OCR HTTP）
    public static class Vision
    {
        // ---------- 颜色解析 ----------
        public static bool ParseColor(string s, out int r, out int g, out int b)
        {
            r = g = b = 0;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length < 6) return false;
            try
            {
                r = Convert.ToInt32(s.Substring(0, 2), 16);
                g = Convert.ToInt32(s.Substring(2, 2), 16);
                b = Convert.ToInt32(s.Substring(4, 2), 16);
                return true;
            }
            catch { return false; }
        }

        // 非0时后台图色：从绑定窗口客户区截图（坐标为窗口客户区坐标）
        public static IntPtr CaptureSource = IntPtr.Zero;

        static Bitmap Capture(int l, int t, int r, int b)
        {
            int w = r - l, h = b - t;
            if (w <= 0 || h <= 0) return null;
            if (CaptureSource != IntPtr.Zero)
            {
                try
                {
                    Native.RECT cr;
                    Native.GetClientRect(CaptureSource, out cr);
                    int cw = cr.Right - cr.Left, ch = cr.Bottom - cr.Top;
                    if (cw <= 0 || ch <= 0) return null;
                    Bitmap win = new Bitmap(cw, ch, PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(win))
                    {
                        IntPtr hdc = g.GetHdc();
                        Native.PrintWindow(CaptureSource, hdc, 1); // PW_CLIENTONLY
                        g.ReleaseHdc(hdc);
                    }
                    int cl = Math.Max(0, Math.Min(l, cw - 1));
                    int ct = Math.Max(0, Math.Min(t, ch - 1));
                    int crr = Math.Min(r, cw);
                    int cbb = Math.Min(b, ch);
                    if (crr - cl <= 0 || cbb - ct <= 0) { win.Dispose(); return null; }
                    Bitmap crop = win.Clone(new Rectangle(cl, ct, crr - cl, cbb - ct), win.PixelFormat);
                    win.Dispose();
                    return crop;
                }
                catch { return null; }
            }
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(l, t, 0, 0, new Size(w, h));
            }
            return bmp;
        }

        public static int CaptureRect(int l, int t, int r, int b, string path)
        {
            try
            {
                using (Bitmap bmp = Capture(l, t, r, b))
                {
                    if (bmp == null) return 0;
                    bmp.Save(path, ImageFormat.Bmp);
                }
                return 1;
            }
            catch { return 0; }
        }

        // ---------- 获取颜色 ----------
        public static string GetColor(int x, int y)
        {
            try
            {
                Color c = Color.Empty;
                Thread t = new Thread(delegate() { c = GetPixelSafe(x, y); });
                t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(3000);
                return string.Format("{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B);
            }
            catch { return ""; }
        }
        static Color GetPixelSafe(int x, int y)
        {
            using (Bitmap bmp = Capture(x, y, x + 1, y + 1))
            {
                if (bmp == null) return Color.Black;
                return bmp.GetPixel(0, 0);
            }
        }

        // ---------- 找颜色 ----------
        public class BitmapAccessor : IDisposable
        {
            public byte[] Bits; public int W, H;
            Bitmap src;
            public BitmapAccessor(Bitmap bmp)
            {
                src = bmp;
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                W = bmp.Width; H = bmp.Height;
                Bits = new byte[d.Stride * H];
                System.Runtime.InteropServices.Marshal.Copy(d.Scan0, Bits, 0, Bits.Length);
                bmp.UnlockBits(d);
            }
            public int Px(int x, int y) { return (y * W + x) * 3; }
            public void Dispose() { }
        }
        static BitmapAccessor Lock(Bitmap bmp)
        {
            return new BitmapAccessor(bmp);
        }

        public static int CountColor(int l, int t, int r, int b, string color)
        {
            int cr, cg, cb;
            if (!ParseColor(color, out cr, out cg, out cb)) return 0;
            int count = 0;
            try
            {
                using (Bitmap bmp = Capture(l, t, r, b))
                {
                    if (bmp == null) return 0;
                    BitmapAccessor id = Lock(bmp);
                    for (int y = 0; y < id.H; y++)
                        for (int x = 0; x < id.W; x++)
                        {
                            int o = id.Px(x, y);
                            if (id.Bits[o + 2] == cr && id.Bits[o + 1] == cg && id.Bits[o] == cb) count++;
                        }
                }
            }
            catch { }
            return count;
        }

        public static int FindColor(int l, int t, int r, int b, string color, double sim, out int fx, out int fy)
        {
            fx = fy = -1;
            int cr, cg, cb;
            if (!ParseColor(color, out cr, out cg, out cb)) return 0;
            int tol = TolFromSim(sim);
            try
            {
                using (Bitmap bmp = Capture(l, t, r, b))
                {
                    if (bmp == null) return 0;
                    BitmapAccessor id = Lock(bmp);
                    for (int y = 0; y < id.H; y++)
                        for (int x = 0; x < id.W; x++)
                        {
                            int o = id.Px(x, y);
                            if (Math.Abs(id.Bits[o + 2] - cr) <= tol && Math.Abs(id.Bits[o + 1] - cg) <= tol && Math.Abs(id.Bits[o] - cb) <= tol)
                            {
                                fx = l + x; fy = t + y;
                                return 1;
                            }
                        }
                }
            }
            catch { }
            return 0;
        }

        static int TolFromSim(double sim)
        {
            if (sim > 1) sim = 1;
            if (sim < 0.3) sim = 0.3;
            return (int)Math.Round((1.0 - sim) * 255.0 * 0.9);
        }

        // 多点找色: 主色 + "x|y|颜色,x|y|颜色"
        public static int MultiFindColor(int l, int t, int r, int b, string color, string offsets, double sim, out int fx, out int fy)
        {
            fx = fy = -1;
            int cr, cg, cb;
            if (!ParseColor(color, out cr, out cg, out cb)) return 0;
            List<Point> offs = new List<Point>();
            List<int[]> cols = new List<int[]>();
            if (!string.IsNullOrEmpty(offsets))
            {
                foreach (string part in offsets.Split(','))
                {
                    string[] seg = part.Trim().Split('|');
                    if (seg.Length != 3) continue;
                    string[] xy = seg[0].Trim().Split(' '); // 不支持空格形式
                    int ox, oy;
                    string[] xy2 = seg[0].Trim().Split(new char[] { '|' });
                    // 格式为 x|y|color 已被上一行拆开: seg[0]=x seg[1]=y seg[2]=color
                    if (!int.TryParse(seg[0].Trim(), out ox)) continue;
                    if (!int.TryParse(seg[1].Trim(), out oy)) continue;
                    int or, og, ob;
                    if (!ParseColor(seg[2], out or, out og, out ob)) continue;
                    offs.Add(new Point(ox, oy));
                    cols.Add(new int[] { or, og, ob });
                }
            }
            int tol = TolFromSim(sim);
            try
            {
                using (Bitmap bmp = Capture(l, t, r, b))
                {
                    if (bmp == null) return 0;
                    BitmapAccessor id = Lock(bmp);
                    int margin = 0;
                    foreach (Point p in offs) margin = Math.Max(margin, Math.Max(p.X, p.Y));
                    for (int y = 0; y + margin < id.H; y++)
                        for (int x = 0; x + margin < id.W; x++)
                        {
                            int o = id.Px(x, y);
                            if (Math.Abs(id.Bits[o + 2] - cr) <= tol && Math.Abs(id.Bits[o + 1] - cg) <= tol && Math.Abs(id.Bits[o] - cb) <= tol)
                            {
                                bool all = true;
                                for (int i = 0; i < offs.Count; i++)
                                {
                                    int px = x + offs[i].X, py = y + offs[i].Y;
                                    if (px < 0 || py < 0 || px >= id.W || py >= id.H) { all = false; break; }
                                    int po = id.Px(px, py);
                                    int diffR = Math.Abs(id.Bits[po + 2] - cols[i][0]);
                                    int diffG = Math.Abs(id.Bits[po + 1] - cols[i][1]);
                                    int diffB = Math.Abs(id.Bits[po] - cols[i][2]);
                                    if (diffR > tol || diffG > tol || diffB > tol) { all = false; break; }
                                }
                                if (all)
                                {
                                    fx = l + x; fy = t + y;
                                    return 1;
                                }
                            }
                        }
                }
            }
            catch { }
            return 0;
        }

        // ---------- 找图片 ----------
        public static string TemplateDir = "";   // 模板图片目录
        static string ResolveImage(string name)
        {
            if (File.Exists(name)) return name;
            if (TemplateDir != "")
            {
                string p2 = Path.Combine(TemplateDir, name);
                if (File.Exists(p2)) return p2;
                if (File.Exists(p2 + ".bmp")) return p2 + ".bmp";
                if (File.Exists(p2 + ".png")) return p2 + ".png";
                if (File.Exists(p2 + ".jpg")) return p2 + ".jpg";
            }
            return null;
        }

        public static int FindImage(int l, int t, int r, int b, string name, string devi, double sim, out int fx, out int fy)
        {
            List<Point> one;
            int n = FindImages(l, t, r, b, name, devi, sim, out one);
            if (n > 0) { fx = one[0].X; fy = one[0].Y; return 1; }
            fx = fy = -1;
            return 0;
        }

        public static int FindImages(int l, int t, int r, int b, string name, string devi, double sim, out List<Point> found)
        {
            found = new List<Point>();
            string path = ResolveImage(name);
            if (path == null) return 0;
            if (sim > 1) sim = 1;
            if (sim < 0.3) sim = 0.3;
            try
            {
                Bitmap tpl;
                using (Bitmap src = new Bitmap(path))
                {
                    tpl = new Bitmap(src, src.Width, src.Height); // 解码副本
                }
                using (tpl)
                using (Bitmap region = Capture(l, t, r, b))
                {
                    if (region == null) return 0;
                    BitmapAccessor tid = Lock(tpl), rid = Lock(region);
                    int tw = tid.W, th = tid.H;
                    if (tw > rid.W || th > rid.H) return 0;
                    int need = (int)Math.Ceiling(sim * tw * th);
                    int tol = Math.Max(6, (int)Math.Round((1.0 - sim) * 200));
                    int step = Math.Max(1, Math.Min(4, Math.Min(tw, th) / 8));
                    int reportEvery = Math.Max(1, (tw * th) / 100);
                    for (int cy = 0; cy + th <= rid.H; cy++)
                        for (int cx = 0; cx + tw <= rid.W; cx++)
                        {
                            int matched = 0;
                            // 粗扫（步长step）先快速排除
                            int coarseNeed = 0, coarseBad = 0;
                            int coarseTotal = 0;
                            bool fail = false;
                            for (int y = 0; y < th && !fail; y += step)
                                for (int x = 0; x < tw && !fail; x += step)
                                {
                                    coarseTotal++;
                                    int ro = rid.Px(cx + x, cy + y), to = tid.Px(x, y);
                                    if (Math.Abs(rid.Bits[ro + 2] - tid.Bits[to + 2]) <= tol &&
                                        Math.Abs(rid.Bits[ro + 1] - tid.Bits[to + 1]) <= tol &&
                                        Math.Abs(rid.Bits[ro] - tid.Bits[to]) <= tol) matched++;
                                    else coarseBad++;
                                    if (coarseTotal >= 24 && coarseBad > 8) { fail = true; break; }
                                }
                            if (fail) continue;
                            // 细扫全像素
                            matched = 0;
                            int checked2 = 0;
                            for (int y = 0; y < th; y++)
                                for (int x = 0; x < tw; x++)
                                {
                                    checked2++;
                                    int ro = rid.Px(cx + x, cy + y), to = tid.Px(x, y);
                                    if (Math.Abs(rid.Bits[ro + 2] - tid.Bits[to + 2]) <= tol &&
                                        Math.Abs(rid.Bits[ro + 1] - tid.Bits[to + 1]) <= tol &&
                                        Math.Abs(rid.Bits[ro] - tid.Bits[to]) <= tol) matched++;
                                    if (checked2 % reportEvery == 0 && matched + (tw * th - checked2) < need) { fail = true; break; }
                                }
                            if (!fail && matched >= need)
                            {
                                // 非极大值抑制
                                bool dup = false;
                                foreach (Point p in found)
                                    if (Math.Abs(p.X - (l + cx)) < tw / 2 && Math.Abs(p.Y - (t + cy)) < th / 2) { dup = true; break; }
                                if (!dup) found.Add(new Point(l + cx, t + cy));
                            }
                        }
                }
            }
            catch (Exception) { }
            return found.Count;
        }

        // ---------- OCR (Umi-OCR HTTP) ----------
        public class OcrItem { public int Left, Top, Right, Bottom; public string Text; public double Score; }
        static Process ocrProc;
        static bool ocrFailed;
        public static string LastOcrRaw = "";

        static bool OcrServerAlive()
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult r = c.BeginConnect("127.0.0.1", 1224, null, null);
                    bool ok = r.AsyncWaitHandle.WaitOne(400);
                    if (ok) { try { c.EndConnect(r); } catch { return false; } }
                    c.Close();
                    return ok;
                }
            }
            catch { return false; }
        }

        static string FindUmiExe()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] cands = new string[]
            {
                Path.Combine(baseDir, "util\\umi-ocr\\Umi-OCR.exe"),
                Path.Combine(baseDir, "..\\布灵点点-高级版\\util\\umi-ocr\\Umi-OCR.exe"),
                "C:\\Users\\Administrator\\Desktop\\布灵点点-高级版\\util\\umi-ocr\\Umi-OCR.exe"
            };
            foreach (string c in cands) if (File.Exists(c)) return c;
            return null;
        }

        static void EnsureOcrServer()
        {
            if (OcrServerAlive()) return;
            if (ocrFailed) return;
            string exe = FindUmiExe();
            if (exe == null) { ocrFailed = true; return; }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, "--hide --server --port 1224");
                psi.UseShellExecute = true;
                ocrProc = Process.Start(psi);
            }
            catch { ocrFailed = true; return; }
            for (int i = 0; i < 100; i++)
            {
                if (OcrServerAlive()) return;
                Thread.Sleep(200);
            }
            ocrFailed = true;
        }

        public static int OcrRegion(int l, int t, int r, int b, double conf, out List<OcrItem> items, string outImg)
        {
            items = new List<OcrItem>();
            EnsureOcrServer();
            if (!OcrServerAlive()) return 0;
            try
            {
                using (Bitmap bmp = Capture(l, t, r, b))
                {
                    if (bmp == null) return 0;
                    if (!string.IsNullOrEmpty(outImg))
                    {
                        try { bmp.Save(outImg, ImageFormat.Bmp); } catch { }
                    }
                    string b64;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        b64 = Convert.ToBase64String(ms.ToArray());
                    }
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var req = new Dictionary<string, object>();
                    req["base64"] = b64;
                    var opts = new Dictionary<string, object>();
                    req["options"] = opts;
                    string body = js.Serialize(req);
                    string resp = HttpPost("http://127.0.0.1:1224/api/ocr", body, 30000);
                    if (string.IsNullOrEmpty(resp)) return 0;
                    Dictionary<string, object> root = js.Deserialize<Dictionary<string, object>>(resp);
                    if (root == null || !root.ContainsKey("data")) return 0;
                    LastOcrRaw = resp;
                    if (root["data"] is string) return 0;   // 纯文本格式无坐标
                    List<object> dataList = new List<object>();
                    if (root["data"] is System.Collections.IEnumerable && !(root["data"] is Dictionary<string, object>))
                    {
                        foreach (object o in (System.Collections.IEnumerable)root["data"]) dataList.Add(o);
                    }
                    object[] data = dataList.ToArray();
                    if (data == null || data.Length == 0) return 0;
                    foreach (object o in data)
                    {
                        Dictionary<string, object> item = o as Dictionary<string, object>;
                        if (item == null) continue;
                        object[] box = item.ContainsKey("box") ? AsArr(item["box"]) : null;
                        string text = item.ContainsKey("text") ? (item["text"] ?? "").ToString() : "";
                        double score = item.ContainsKey("score") ? Convert.ToDouble(item["score"]) : 1.0;
                        if (box == null || box.Length < 2) continue;
                        int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
                        foreach (object bp in box)
                        {
                            object[] pt = AsArr(bp);
                            if (pt.Length < 2) continue;
                            int px = Convert.ToInt32(pt[0]), py = Convert.ToInt32(pt[1]);
                            if (px < minx) minx = px;
                            if (py < miny) miny = py;
                            if (px > maxx) maxx = px;
                            if (py > maxy) maxy = py;
                        }
                        if (score < conf) continue;
                        OcrItem it = new OcrItem();
                        it.Left = l + minx; it.Top = t + miny; it.Right = l + maxx; it.Bottom = t + maxy;
                        it.Text = text; it.Score = score;
                        items.Add(it);
                    }
                    return items.Count;
                }
            }
            catch { return 0; }
        }

        static object[] AsArr(object o)
        {
            if (o is object[]) return (object[])o;
            System.Collections.IEnumerable en = o as System.Collections.IEnumerable;
            if (en == null) return new object[0];
            List<object> l = new List<object>();
            foreach (object x in en) l.Add(x);
            return l.ToArray();
        }

        static string HttpPost(string url, string body, int timeoutMs)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                using (Stream s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch { return null; }
        }
    }
}
