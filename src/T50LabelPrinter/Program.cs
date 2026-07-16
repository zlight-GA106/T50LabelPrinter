using System;
using System.IO;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                string workingDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "T50LabelPrinter");
                Directory.CreateDirectory(workingDirectory);
                Environment.CurrentDirectory = workingDirectory;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, args) =>
                MessageBox.Show(args.Exception.Message, "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                MessageBox.Show(Convert.ToString(args.ExceptionObject), "程序错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
        }
    }
}
