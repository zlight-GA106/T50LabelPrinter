using System;
using System.Collections.Generic;

namespace T50LabelPrinter
{
    public sealed class ThermalScheduleItem
    {
        public bool Completed { get; set; }
        public string Time { get; set; }
        public string Content { get; set; }

        public ThermalScheduleItem DeepClone()
        {
            return new ThermalScheduleItem
            {
                Completed = Completed,
                Time = Time ?? string.Empty,
                Content = Content ?? string.Empty
            };
        }
    }

    public sealed class ThermalScheduleDocument
    {
        public const decimal PaperWidthMm = 58m;

        public string Title { get; set; }
        public DateTime Date { get; set; }
        public bool ShowDate { get; set; }
        public bool ShowCheckboxes { get; set; }
        public string FontFamily { get; set; }
        public decimal TitleFontSizeMm { get; set; }
        public decimal BodyFontSizeMm { get; set; }
        public decimal MarginMm { get; set; }
        public decimal RowSpacingMm { get; set; }
        public int Copies { get; set; }
        public List<ThermalScheduleItem> Items { get; set; }

        public static ThermalScheduleDocument CreateDefault()
        {
            return new ThermalScheduleDocument
            {
                Title = "今日日程",
                Date = DateTime.Today,
                ShowDate = true,
                ShowCheckboxes = true,
                FontFamily = FontCatalog.DefaultSansFamily,
                TitleFontSizeMm = 5m,
                BodyFontSizeMm = 3.2m,
                MarginMm = 3m,
                RowSpacingMm = 1.2m,
                Copies = 1,
                Items = new List<ThermalScheduleItem>
                {
                    new ThermalScheduleItem { Time = "09:00", Content = "填写日程内容" },
                    new ThermalScheduleItem { Time = "14:00", Content = "按时间安排任务" }
                }
            };
        }

        public void Normalize()
        {
            Title = (Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(FontFamily))
            {
                FontFamily = FontCatalog.DefaultSansFamily;
            }
            TitleFontSizeMm = Math.Max(2.5m, Math.Min(10m, TitleFontSizeMm));
            BodyFontSizeMm = Math.Max(1.8m, Math.Min(8m, BodyFontSizeMm));
            MarginMm = Math.Max(1m, Math.Min(10m, MarginMm));
            RowSpacingMm = Math.Max(0.4m, Math.Min(6m, RowSpacingMm));
            Copies = Math.Max(1, Math.Min(99, Copies));
            if (Items == null)
            {
                Items = new List<ThermalScheduleItem>();
            }
            if (Items.Count > 200)
            {
                Items.RemoveRange(200, Items.Count - 200);
            }
            foreach (ThermalScheduleItem item in Items)
            {
                item.Time = (item.Time ?? string.Empty).Trim();
                item.Content = (item.Content ?? string.Empty).Trim();
            }
        }

        public ThermalScheduleDocument DeepClone()
        {
            ThermalScheduleDocument clone = new ThermalScheduleDocument
            {
                Title = Title,
                Date = Date,
                ShowDate = ShowDate,
                ShowCheckboxes = ShowCheckboxes,
                FontFamily = FontFamily,
                TitleFontSizeMm = TitleFontSizeMm,
                BodyFontSizeMm = BodyFontSizeMm,
                MarginMm = MarginMm,
                RowSpacingMm = RowSpacingMm,
                Copies = Copies,
                Items = new List<ThermalScheduleItem>()
            };
            if (Items != null)
            {
                foreach (ThermalScheduleItem item in Items)
                {
                    clone.Items.Add(item.DeepClone());
                }
            }
            clone.Normalize();
            return clone;
        }
    }
}
