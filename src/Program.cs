using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("micopy")]
[assembly: AssemblyDescription("ワンクリック差分バックアップ")]
[assembly: AssemblyProduct("micopy")]
[assembly: AssemblyCopyright("Copyright (C) 2026 onboard-dev  License: GPL-3.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace Micopy
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "micopy_single_instance_7F3A", out created))
            {
                if (!created)
                {
                    MessageBox.Show("micopy はすでに起動しています。", "micopy",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    ReportError(e.Exception, false);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    ReportError(e.ExceptionObject as Exception, true);
                };
                Application.Run(new MainForm());
            }
        }

        /// <summary>予期しないエラーをログに残して知らせる(ここで再びエラーになっても落ちないようにする)</summary>
        private static void ReportError(Exception ex, bool fatal)
        {
            string text = ex != null ? ex.ToString() : "(不明なエラー)";
            string logPath = "";
            try
            {
                System.IO.Directory.CreateDirectory(AppSettings.AppDataDir);
                logPath = System.IO.Path.Combine(AppSettings.AppDataDir, "error.log");
                System.IO.File.AppendAllText(logPath,
                    "==== " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " ====\r\n" + text + "\r\n\r\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }

            string msg = (fatal ? "予期しないエラーが発生したため、micopy を終了します。" : "予期しないエラーが発生しました。") +
                         "\n\n" + (ex != null ? ex.Message : "") +
                         (logPath.Length > 0 ? "\n\n詳細: " + logPath : "");
            try
            {
                EmojiBox.Show(null, msg, "cross", EmojiBox.Buttons.OK);
            }
            catch
            {
                try { MessageBox.Show(msg, "micopy", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }
    }
}
