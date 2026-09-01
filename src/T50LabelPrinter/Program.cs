using System;
using System.IO;
using System.Windows.Forms;

namespace T50LabelPrinter
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] commandLineArgs)
        {
            string startupScheduleFile = ResolveStartupScheduleFile(commandLineArgs);
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
            Application.Run(new MainForm(startupScheduleFile));
        }

        private static string ResolveStartupScheduleFile(string[] args)
        {
            if (args == null)
            {
                return null;
            }
            foreach (string argument in args)
            {
                if (!ThermalScheduleTemplateStore.IsSupportedFile(argument))
                {
                    continue;
                }
                try
                {
                    return Path.GetFullPath(argument);
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
            }
            return null;
        }
    }
}
