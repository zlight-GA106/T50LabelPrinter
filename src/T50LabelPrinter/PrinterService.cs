using System;
using System.Collections.Generic;
using Supvan.T50PRO.SDK;

namespace T50LabelPrinter
{
    public sealed class PrinterStatusSnapshot
    {
        public DeviceState State { get; set; }
        public string Description { get; set; }
        public string ErrorMessage { get; set; }
        public int PrintedPages { get; set; }
        public int TotalPages { get; set; }

        public string StateText
        {
            get
            {
                switch (State)
                {
                    case DeviceState.Waiting: return "就绪";
                    case DeviceState.CheckDevice: return "检查设备";
                    case DeviceState.Printting: return "打印中";
                    case DeviceState.AbortPrint: return "打印中止";
                    case DeviceState.Completed: return "打印完成";
                    case DeviceState.ResetDevice: return "设备复位";
                    default: return State.ToString();
                }
            }
        }
    }

    public sealed class PrinterService
    {
        public IList<string> GetDevicePaths()
        {
            return T50PROPrintUtil.GetDevicePaths() ?? new List<string>();
        }

        public PrinterStatusSnapshot GetStatus(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
            {
                return null;
            }

            PrintResult result = T50PROPrintUtil.GetPrintResult(devicePath);
            if (result == null)
            {
                return null;
            }
            return new PrinterStatusSnapshot
            {
                State = result.State,
                Description = result.PrintDes ?? string.Empty,
                ErrorMessage = result.ErrorMsg ?? string.Empty,
                PrintedPages = result.DevPrintedPageCount,
                TotalPages = result.PrintPageTotalCount
            };
        }

        public bool Print(LabelDocument document, string bitmapPath, string devicePath)
        {
            SDKSPParamter parameter = new SDKSPParamter
            {
                PrintSet = new SDKPrintSet
                {
                    Copy = document.Copies,
                    Deepness = document.Deepness,
                    DPI = LabelRenderer.PrinterDotsPerMm,
                    Direction = document.Direction,
                    Gap = document.GapMm,
                    Speed = document.Speed,
                    Width = Decimal.ToInt32(Decimal.Round(document.WidthMm, 0)),
                    Height = Decimal.ToInt32(Decimal.Round(document.HeightMm, 0)),
                    PaperType = document.PaperType,
                    MaxDotValue = 384,
                    OffsetH = 0,
                    OffsetV = 0,
                    OneByOne = document.OneByOne
                },
                PrintPages = new List<SDKPrintPage>
                {
                    new SDKPrintPage
                    {
                        Repeat = 1,
                        DrawObjects = new List<SDKPrintPageDrawObject>
                        {
                            new SDKPrintPageDrawObject
                            {
                                AntiColor = false,
                                X = 0m,
                                Y = 0m,
                                Width = document.WidthMm,
                                Height = document.HeightMm,
                                Content = bitmapPath,
                                FontName = FontCatalog.DefaultSansFamily,
                                FontStyle = 0,
                                Align = 0,
                                FontSize = "3",
                                AutoReturn = false,
                                Format = "IMAGE"
                            }
                        }
                    }
                }
            };

            return T50PROPrintUtil.DoPrint(parameter, devicePath);
        }
    }
}
