using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace T50LabelPrinter
{
    public sealed class ThermalPrinterStatus
    {
        public bool IsValid { get; set; }
        public bool HasError { get; set; }
        public bool IsDefault { get; set; }
        public string StateText { get; set; }
        public string Description { get; set; }
    }

    public sealed class ThermalPrinterService
    {
        public IList<string> GetInstalledPrinters()
        {
            List<string> printers = new List<string>();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                printers.Add(printer);
            }
            printers.Sort(StringComparer.CurrentCultureIgnoreCase);
            return printers;
        }

        public string GetDefaultPrinterName()
        {
            PrinterSettings settings = new PrinterSettings();
            return settings.PrinterName ?? string.Empty;
        }

        public bool IsLikelyThermalPrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return false;
            }
            string name = printerName.ToUpperInvariant();
            string[] markers = { "58", "POS", "THERMAL", "RECEIPT", "TICKET", "小票", "热敏", "AY-D" };
            foreach (string marker in markers)
            {
                if (name.Contains(marker.ToUpperInvariant()))
                {
                    return true;
                }
            }
            return false;
        }

        public ThermalPrinterStatus GetStatus(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return new ThermalPrinterStatus
                {
                    IsValid = false,
                    HasError = true,
                    StateText = "未选择",
                    Description = "尚未选择 Windows 打印机。"
                };
            }

            PrinterSettings settings = new PrinterSettings { PrinterName = printerName };
            PrinterSettings defaultSettings = new PrinterSettings();
            bool valid = settings.IsValid;
            uint queueStatus = 0;
            uint jobs = 0;
            bool hasQueueStatus = valid && TryGetQueueStatus(printerName, out queueStatus, out jobs);
            bool hasError = !valid || (hasQueueStatus && HasErrorStatus(queueStatus));
            string stateText = !valid
                ? "无效"
                : hasQueueStatus ? GetStateText(queueStatus) : "可用";
            string description = !valid
                ? "打印机队列无效，请检查驱动、USB 连接和打印机电源。"
                : hasQueueStatus
                    ? "Windows 队列：" + stateText + "；待处理任务 " + jobs + "；纸宽固定为 58 mm。"
                    : "Windows 打印队列可用；纸宽固定为 58 mm。";
            return new ThermalPrinterStatus
            {
                IsValid = valid,
                HasError = hasError,
                IsDefault = string.Equals(printerName, defaultSettings.PrinterName, StringComparison.OrdinalIgnoreCase),
                StateText = stateText,
                Description = description
            };
        }

        public void Print(string printerName, ThermalScheduleDocument schedule)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException("schedule");
            }
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new InvalidOperationException("未选择 58mm 热敏打印机。");
            }

            ThermalScheduleDocument document = schedule.DeepClone();
            using (Bitmap receipt = ThermalScheduleRenderer.Render(document))
            using (PrintDocument printDocument = new PrintDocument())
            {
                printDocument.DocumentName = string.IsNullOrWhiteSpace(document.Title)
                    ? "58mm 日程表"
                    : document.Title;
                printDocument.PrinterSettings.PrinterName = printerName;
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException("所选 Windows 打印机无效，请重新刷新设备。");
                }

                int paperWidth = MillimetersToHundredthsOfInch(ThermalScheduleDocument.PaperWidthMm);
                int paperHeight = MillimetersToHundredthsOfInch(ThermalScheduleRenderer.GetHeightMm(receipt));
                printDocument.DefaultPageSettings.PaperSize = new PaperSize("58mm 日程", paperWidth, paperHeight);
                printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                printDocument.OriginAtMargins = false;
                int maximumCopies = printDocument.PrinterSettings.MaximumCopies;
                int copies = Math.Max(1, document.Copies);
                if (maximumCopies > 0)
                {
                    copies = Math.Min(copies, maximumCopies);
                }
                printDocument.PrinterSettings.Copies = (short)Math.Min(short.MaxValue, copies);
                printDocument.PrintController = new StandardPrintController();
                printDocument.PrintPage += (sender, args) => DrawReceiptPage(args, receipt);
                printDocument.Print();
            }
        }

        private static void DrawReceiptPage(PrintPageEventArgs args, Bitmap receipt)
        {
            GraphicsState state = args.Graphics.Save();
            try
            {
                args.Graphics.TranslateTransform(-args.PageSettings.HardMarginX, -args.PageSettings.HardMarginY);
                float width = MillimetersToHundredthsOfInch(ThermalScheduleDocument.PaperWidthMm);
                float height = MillimetersToHundredthsOfInch(ThermalScheduleRenderer.GetHeightMm(receipt));
                args.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                args.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                args.Graphics.DrawImage(receipt, new RectangleF(0f, 0f, width, height));
            }
            finally
            {
                args.Graphics.Restore(state);
            }
            args.HasMorePages = false;
        }

        private static int MillimetersToHundredthsOfInch(decimal millimeters)
        {
            return Math.Max(1, (int)Math.Round((double)(millimeters / 25.4m * 100m)));
        }

        private static bool TryGetQueueStatus(string printerName, out uint status, out uint jobs)
        {
            status = 0;
            jobs = 0;
            IntPtr printer;
            if (!OpenPrinter(printerName, out printer, IntPtr.Zero))
            {
                return false;
            }

            try
            {
                uint required;
                GetPrinter(printer, 2, IntPtr.Zero, 0, out required);
                if (required == 0)
                {
                    return false;
                }
                IntPtr buffer = Marshal.AllocHGlobal((int)required);
                try
                {
                    if (!GetPrinter(printer, 2, buffer, required, out required))
                    {
                        return false;
                    }
                    PrinterInfo2 info = (PrinterInfo2)Marshal.PtrToStructure(buffer, typeof(PrinterInfo2));
                    status = info.Status;
                    jobs = info.JobCount;
                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                ClosePrinter(printer);
            }
        }

        private static bool HasErrorStatus(uint status)
        {
            const uint errorMask = PrinterStatusPaused | PrinterStatusError | PrinterStatusPendingDeletion |
                PrinterStatusPaperJam | PrinterStatusPaperOut | PrinterStatusPaperProblem |
                PrinterStatusOffline | PrinterStatusOutputBinFull | PrinterStatusNotAvailable |
                PrinterStatusNoToner | PrinterStatusUserIntervention | PrinterStatusOutOfMemory |
                PrinterStatusDoorOpen | PrinterStatusServerUnknown;
            return (status & errorMask) != 0;
        }

        private static string GetStateText(uint status)
        {
            if ((status & PrinterStatusOffline) != 0) return "离线";
            if ((status & PrinterStatusPaperOut) != 0) return "缺纸";
            if ((status & PrinterStatusPaperJam) != 0) return "卡纸";
            if ((status & PrinterStatusDoorOpen) != 0) return "机盖打开";
            if ((status & PrinterStatusPaused) != 0) return "已暂停";
            if ((status & PrinterStatusUserIntervention) != 0) return "需要人工处理";
            if ((status & PrinterStatusError) != 0) return "错误";
            if ((status & PrinterStatusPrinting) != 0) return "打印中";
            if ((status & PrinterStatusBusy) != 0) return "忙碌";
            if ((status & PrinterStatusInitializing) != 0) return "初始化";
            if ((status & PrinterStatusWarmingUp) != 0) return "预热";
            if ((status & PrinterStatusPowerSave) != 0) return "节能";
            return "就绪";
        }

        private const uint PrinterStatusPaused = 0x00000001;
        private const uint PrinterStatusError = 0x00000002;
        private const uint PrinterStatusPendingDeletion = 0x00000004;
        private const uint PrinterStatusPaperJam = 0x00000008;
        private const uint PrinterStatusPaperOut = 0x00000010;
        private const uint PrinterStatusPaperProblem = 0x00000040;
        private const uint PrinterStatusOffline = 0x00000080;
        private const uint PrinterStatusBusy = 0x00000200;
        private const uint PrinterStatusPrinting = 0x00000400;
        private const uint PrinterStatusOutputBinFull = 0x00000800;
        private const uint PrinterStatusNotAvailable = 0x00001000;
        private const uint PrinterStatusInitializing = 0x00008000;
        private const uint PrinterStatusWarmingUp = 0x00010000;
        private const uint PrinterStatusNoToner = 0x00040000;
        private const uint PrinterStatusUserIntervention = 0x00100000;
        private const uint PrinterStatusOutOfMemory = 0x00200000;
        private const uint PrinterStatusDoorOpen = 0x00400000;
        private const uint PrinterStatusServerUnknown = 0x00800000;
        private const uint PrinterStatusPowerSave = 0x01000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PrinterInfo2
        {
            public IntPtr ServerName;
            public IntPtr PrinterName;
            public IntPtr ShareName;
            public IntPtr PortName;
            public IntPtr DriverName;
            public IntPtr Comment;
            public IntPtr Location;
            public IntPtr DevMode;
            public IntPtr SeparatorFile;
            public IntPtr PrintProcessor;
            public IntPtr DataType;
            public IntPtr Parameters;
            public IntPtr SecurityDescriptor;
            public uint Attributes;
            public uint Priority;
            public uint DefaultPriority;
            public uint StartTime;
            public uint UntilTime;
            public uint Status;
            public uint JobCount;
            public uint AveragePagesPerMinute;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetPrinter(
            IntPtr printer,
            uint level,
            IntPtr printerInfo,
            uint bufferSize,
            out uint required);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr printer);
    }
}
