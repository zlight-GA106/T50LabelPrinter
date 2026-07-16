using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace T50LabelPrinter
{
    [DataContract]
    public enum LabelElementKind
    {
        [EnumMember]
        Text = 0,
        [EnumMember]
        Pdf417 = 1,
        [EnumMember]
        DataMatrix = 2
    }

    [DataContract]
    public enum CenterGuideMode
    {
        [EnumMember]
        None = 0,
        [EnumMember]
        Vertical = 1,
        [EnumMember]
        Horizontal = 2,
        [EnumMember]
        Cross = 3
    }

    [DataContract]
    public sealed class LabelElement
    {
        [DataMember(Order = 1)]
        public LabelElementKind Kind { get; set; }

        [DataMember(Order = 2)]
        public decimal X { get; set; }

        [DataMember(Order = 3)]
        public decimal Y { get; set; }

        [DataMember(Order = 4)]
        public decimal Width { get; set; }

        [DataMember(Order = 5)]
        public decimal Height { get; set; }

        [DataMember(Order = 6)]
        public string Text { get; set; }

        [DataMember(Order = 7)]
        public string FontFamily { get; set; }

        [DataMember(Order = 8)]
        public decimal FontSizeMm { get; set; }

        [DataMember(Order = 9)]
        public bool Bold { get; set; }

        [DataMember(Order = 10)]
        public int Align { get; set; }

        [DataMember(Order = 11)]
        public string PdfPrefix { get; set; }

        [DataMember(Order = 12)]
        public bool PdfUseTimestamp { get; set; }

        [DataMember(Order = 13)]
        public string PdfPayload { get; set; }

        [DataMember(Order = 14)]
        public bool PrintDigits { get; set; }

        [DataMember(Order = 15)]
        public string DigitsText { get; set; }

        public bool IsBarcode
        {
            get { return Kind == LabelElementKind.Pdf417 || Kind == LabelElementKind.DataMatrix; }
        }

        public string DisplayName
        {
            get
            {
                if (Kind == LabelElementKind.Pdf417)
                {
                    return "PDF417  " + (PdfPrefix ?? "").ToUpperInvariant();
                }
                if (Kind == LabelElementKind.DataMatrix)
                {
                    return "Data Matrix  " + (PdfPrefix ?? "").ToUpperInvariant();
                }

                string value = (Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (value.Length > 14)
                {
                    value = value.Substring(0, 14) + "…";
                }
                return "文字  " + (value.Length == 0 ? "（空）" : value);
            }
        }

        public string GetPdfContent(DateTime timestamp)
        {
            return GetBarcodeContent(timestamp);
        }

        public string GetBarcodeContent(DateTime timestamp)
        {
            string prefix = (PdfPrefix ?? string.Empty).Trim().ToUpperInvariant();
            string payload = PdfUseTimestamp
                ? timestamp.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                : (PdfPayload ?? string.Empty);
            return prefix + payload;
        }

        public string GetDigitsContent(DateTime timestamp)
        {
            string source = string.IsNullOrWhiteSpace(DigitsText)
                ? GetBarcodeContent(timestamp)
                : DigitsText;
            return new string((source ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        public static LabelElement CreateText(decimal labelWidth, decimal labelHeight)
        {
            decimal width = Math.Min(30m, Math.Max(5m, labelWidth - 4m));
            decimal height = Math.Min(8m, Math.Max(3m, labelHeight - 4m));
            return new LabelElement
            {
                Kind = LabelElementKind.Text,
                X = Math.Max(0m, (labelWidth - width) / 2m),
                Y = Math.Max(0m, (labelHeight - height) / 2m),
                Width = width,
                Height = height,
                Text = "标签文字",
                FontFamily = FontCatalog.DefaultSansFamily,
                FontSizeMm = 4m,
                Align = 1,
                PdfPrefix = "ABC",
                PdfUseTimestamp = true,
                PdfPayload = string.Empty,
                PrintDigits = false,
                DigitsText = string.Empty
            };
        }

        public static LabelElement CreatePdf417(decimal labelWidth, decimal labelHeight)
        {
            decimal width = Math.Min(35m, Math.Max(10m, labelWidth - 4m));
            decimal height = Math.Min(12m, Math.Max(5m, labelHeight - 4m));
            return new LabelElement
            {
                Kind = LabelElementKind.Pdf417,
                X = Math.Max(0m, (labelWidth - width) / 2m),
                Y = Math.Max(0m, (labelHeight - height) / 2m),
                Width = width,
                Height = height,
                Text = string.Empty,
                FontFamily = FontCatalog.DefaultSansFamily,
                FontSizeMm = 3m,
                Align = 1,
                PdfPrefix = "ABC",
                PdfUseTimestamp = true,
                PdfPayload = string.Empty,
                PrintDigits = false,
                DigitsText = string.Empty
            };
        }

        public static LabelElement CreateDataMatrix(decimal labelWidth, decimal labelHeight)
        {
            decimal size = Math.Min(18m, Math.Max(8m, Math.Min(labelWidth, labelHeight) - 4m));
            return new LabelElement
            {
                Kind = LabelElementKind.DataMatrix,
                X = Math.Max(0m, (labelWidth - size) / 2m),
                Y = Math.Max(0m, (labelHeight - size) / 2m),
                Width = size,
                Height = size,
                Text = string.Empty,
                FontFamily = FontCatalog.DefaultSansFamily,
                FontSizeMm = 2.2m,
                Align = 1,
                PdfPrefix = "ABC",
                PdfUseTimestamp = true,
                PdfPayload = string.Empty,
                PrintDigits = false,
                DigitsText = string.Empty
            };
        }
    }

    [DataContract]
    public sealed class LabelDocument
    {
        [DataMember(Order = 1)]
        public decimal WidthMm { get; set; }

        [DataMember(Order = 2)]
        public decimal HeightMm { get; set; }

        [DataMember(Order = 3)]
        public int GapMm { get; set; }

        [DataMember(Order = 4)]
        public int PaperType { get; set; }

        [DataMember(Order = 5)]
        public int Direction { get; set; }

        [DataMember(Order = 6)]
        public int Speed { get; set; }

        [DataMember(Order = 7)]
        public int Deepness { get; set; }

        [DataMember(Order = 8)]
        public int Copies { get; set; }

        [DataMember(Order = 9)]
        public bool OneByOne { get; set; }

        [DataMember(Order = 10)]
        public CenterGuideMode GuideMode { get; set; }

        [DataMember(Order = 11)]
        public bool PrintGuide { get; set; }

        [DataMember(Order = 12)]
        public decimal GuideThicknessMm { get; set; }

        [DataMember(Order = 13)]
        public List<LabelElement> Elements { get; set; }

        [DataMember(Order = 14)]
        public bool PrintBarcodes { get; set; }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            PrintBarcodes = true;
        }

        public static LabelDocument CreateDefault()
        {
            LabelDocument document = new LabelDocument
            {
                WidthMm = 50m,
                HeightMm = 30m,
                GapMm = 3,
                PaperType = 1,
                Direction = 0,
                Speed = 40,
                Deepness = 4,
                Copies = 1,
                OneByOne = true,
                GuideMode = CenterGuideMode.None,
                PrintGuide = false,
                GuideThicknessMm = 0.25m,
                PrintBarcodes = true,
                Elements = new List<LabelElement>()
            };

            LabelElement text = LabelElement.CreateText(document.WidthMm, document.HeightMm);
            text.Y = 4m;
            text.Text = "示例标签";
            LabelElement barcode = LabelElement.CreatePdf417(document.WidthMm, document.HeightMm);
            barcode.Y = 13m;
            document.Elements.Add(text);
            document.Elements.Add(barcode);
            return document;
        }

        public void Normalize()
        {
            WidthMm = Math.Max(5m, Math.Min(50m, WidthMm));
            HeightMm = Math.Max(5m, Math.Min(200m, HeightMm));
            GapMm = Math.Max(0, Math.Min(20, GapMm));
            Speed = Math.Max(20, Math.Min(60, Speed));
            Deepness = Math.Max(0, Math.Min(9, Deepness));
            Copies = Math.Max(1, Math.Min(99, Copies));
            GuideThicknessMm = Math.Max(0.1m, Math.Min(2m, GuideThicknessMm));
            if (Elements == null)
            {
                Elements = new List<LabelElement>();
            }

            foreach (LabelElement element in Elements)
            {
                if (string.IsNullOrWhiteSpace(element.FontFamily))
                {
                    element.FontFamily = FontCatalog.DefaultSansFamily;
                }
                if (element.FontSizeMm <= 0m)
                {
                    element.FontSizeMm = 3m;
                }
                if (element.Width <= 0m)
                {
                    element.Width = Math.Min(10m, WidthMm);
                }
                if (element.Height <= 0m)
                {
                    element.Height = Math.Min(5m, HeightMm);
                }
                if (string.IsNullOrWhiteSpace(element.PdfPrefix))
                {
                    element.PdfPrefix = "ABC";
                }
                element.DigitsText = new string((element.DigitsText ?? string.Empty).Where(char.IsDigit).ToArray());
                ClampElement(element);
            }
        }

        public void ClampElement(LabelElement element)
        {
            element.Width = Math.Max(1m, Math.Min(WidthMm, element.Width));
            element.Height = Math.Max(1m, Math.Min(HeightMm, element.Height));
            element.X = Math.Max(0m, Math.Min(WidthMm - element.Width, element.X));
            element.Y = Math.Max(0m, Math.Min(HeightMm - element.Height, element.Y));
        }
    }
}
