using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace BulingOffline
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware();

            if (args.Length > 0 && args[0] == "--selftest")
            {
                SelfTest.Run();
                return;
            }

                        if (args.Length > 0 && args[0] == "--selftest-ocr")
            {
                SelfTest.RunOcr();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
                MessageBox.Show("程序错误: " + e.Exception.Message);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.ExceptionObject.ToString()); } catch { }
            };

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString()); } catch { }
                MessageBox.Show("启动错误: " + ex.Message);
            }
        }
    }
}
