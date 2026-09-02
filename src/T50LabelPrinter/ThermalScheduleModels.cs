using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace T50LabelPrinter
{
    [DataContract]
    public enum ThermalScheduleItemKind
    {
        [EnumMember]
        Schedule = 0,

        [EnumMember]
        Countdown = 1
    }

    [DataContract]
    public sealed class ThermalScheduleItem
    {
        [DataMember(Order = 1)]
        public bool Completed { get; set; }
        [DataMember(Order = 2)]
        public string Time { get; set; }
        [DataMember(Order = 3)]
        public string Content { get; set; }
        [DataMember(Order = 4)]
        public string FontFamily { get; set; }
        [DataMember(Order = 5)]
        public decimal FontSizeMm { get; set; }
        [DataMember(Order = 6)]
        public bool Bold { get; set; }
        [DataMember(Order = 7)]
        public bool Italic { get; set; }
        [DataMember(Order = 8)]
        public ThermalScheduleItemKind Kind { get; set; }
        [DataMember(Order = 9, EmitDefaultValue = false)]
        public DateTime TargetDate { get; set; }

        public string GetCountdownText(DateTime baseDate)
        {
            string name = string.IsNullOrWhiteSpace(Content) ? "目标日" : Content.Trim();
            int days = (TargetDate.Date - baseDate.Date).Days;
            if (days > 0)
            {
                return "距离" + name + "还有 " + days + " 天";
            }
            if (days == 0)
            {
                return "今天是" + name;
            }
            return name + "已过去 " + Math.Abs((long)days) + " 天";
        }

        public ThermalScheduleItem DeepClone()
        {
            return new ThermalScheduleItem
            {
                Completed = Completed,
                Time = Time ?? string.Empty,
                Content = Content ?? string.Empty,
                FontFamily = FontFamily ?? string.Empty,
                FontSizeMm = FontSizeMm,
                Bold = Bold,
                Italic = Italic,
                Kind = Kind,
                TargetDate = TargetDate
            };
        }
    }

    [DataContract]
    public sealed class ThermalScheduleDocument
    {
        public const decimal PaperWidthMm = 58m;

        [DataMember(Order = 1)]
        public string Title { get; set; }
        [DataMember(Order = 2)]
        public DateTime Date { get; set; }
        [DataMember(Order = 3)]
        public bool AutoDate { get; set; }
        [DataMember(Order = 4)]
        public bool ShowDate { get; set; }
        [DataMember(Order = 5)]
        public bool ShowCheckboxes { get; set; }
        [DataMember(Order = 6)]
        public bool ShowTime { get; set; }
        [DataMember(Order = 7)]
        public bool ShowContent { get; set; }
        [DataMember(Order = 8)]
        public string FontFamily { get; set; }
        [DataMember(Order = 9)]
        public string TitleFontFamily { get; set; }
        [DataMember(Order = 10)]
        public decimal TitleFontSizeMm { get; set; }
        [DataMember(Order = 11)]
        public bool TitleBold { get; set; }
        [DataMember(Order = 12)]
        public bool TitleItalic { get; set; }
        [DataMember(Order = 13)]
        public decimal BodyFontSizeMm { get; set; }
        [DataMember(Order = 14)]
        public decimal MarginMm { get; set; }
        [DataMember(Order = 15)]
        public decimal RowSpacingMm { get; set; }
        [DataMember(Order = 16)]
        public int Copies { get; set; }
        [DataMember(Order = 17)]
        public List<ThermalScheduleItem> Items { get; set; }

        // v1.5.0 及更早版本使用的单一倒数日字段。读取后会迁移成 Countdown 行。
        [DataMember(Order = 18, EmitDefaultValue = false)]
        public bool ShowCountdown { get; set; }

        [DataMember(Order = 19, EmitDefaultValue = false)]
        public string CountdownName { get; set; }

        [DataMember(Order = 20, EmitDefaultValue = false)]
        public DateTime CountdownDate { get; set; }

        public static ThermalScheduleDocument CreateDefault()
        {
            return new ThermalScheduleDocument
            {
                Title = "今日日程",
                Date = DateTime.Today,
                AutoDate = true,
                ShowDate = true,
                ShowCheckboxes = true,
                ShowTime = true,
                ShowContent = true,
                FontFamily = FontCatalog.DefaultSansFamily,
                TitleFontFamily = string.Empty,
                TitleFontSizeMm = 5m,
                TitleBold = true,
                TitleItalic = false,
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
            Title = Limit((Title ?? string.Empty).Trim(), 80);
            if (AutoDate)
            {
                Date = DateTime.Today;
            }
            else if (Date.Year < 1753 || Date.Year > 9998)
            {
                Date = DateTime.Today;
            }
            if (string.IsNullOrWhiteSpace(FontFamily))
            {
                FontFamily = FontCatalog.DefaultSansFamily;
            }
            FontFamily = Limit(FontFamily.Trim(), 128);
            TitleFontFamily = Limit((TitleFontFamily ?? string.Empty).Trim(), 128);
            TitleFontSizeMm = Math.Max(2.5m, Math.Min(10m, TitleFontSizeMm));
            BodyFontSizeMm = Math.Max(1.8m, Math.Min(8m, BodyFontSizeMm));
            MarginMm = Math.Max(1m, Math.Min(10m, MarginMm));
            RowSpacingMm = Math.Max(0.4m, Math.Min(6m, RowSpacingMm));
            Copies = Math.Max(1, Math.Min(99, Copies));
            if (Items == null)
            {
                Items = new List<ThermalScheduleItem>();
            }
            Items.RemoveAll(item => item == null);

            if (ShowCountdown)
            {
                string legacyName = Limit((CountdownName ?? string.Empty).Trim(), 80);
                if (string.IsNullOrWhiteSpace(legacyName))
                {
                    legacyName = "目标日";
                }
                DateTime legacyDate = IsValidDate(CountdownDate)
                    ? CountdownDate.Date
                    : Date.AddDays(7).Date;
                bool alreadyMigrated = Items.Any(item =>
                    item.Kind == ThermalScheduleItemKind.Countdown &&
                    item.TargetDate.Date == legacyDate &&
                    string.Equals((item.Content ?? string.Empty).Trim(), legacyName,
                        StringComparison.Ordinal));
                if (!alreadyMigrated)
                {
                    Items.Add(new ThermalScheduleItem
                    {
                        Kind = ThermalScheduleItemKind.Countdown,
                        Content = legacyName,
                        TargetDate = legacyDate,
                        Bold = true
                    });
                }
            }
            ShowCountdown = false;
            CountdownName = null;
            CountdownDate = default(DateTime);

            if (Items.Count > 200)
            {
                Items.RemoveRange(200, Items.Count - 200);
            }
            foreach (ThermalScheduleItem item in Items)
            {
                item.Time = Limit((item.Time ?? string.Empty).Trim(), 20);
                item.Content = Limit((item.Content ?? string.Empty).Trim(), 500);
                item.FontFamily = Limit((item.FontFamily ?? string.Empty).Trim(), 128);
                if (item.FontSizeMm > 0m)
                {
                    item.FontSizeMm = Math.Max(1.8m, Math.Min(8m, item.FontSizeMm));
                }
                if (item.Kind != ThermalScheduleItemKind.Schedule &&
                    item.Kind != ThermalScheduleItemKind.Countdown)
                {
                    item.Kind = ThermalScheduleItemKind.Schedule;
                }
                if (item.Kind == ThermalScheduleItemKind.Countdown)
                {
                    if (string.IsNullOrWhiteSpace(item.Content))
                    {
                        item.Content = "目标日";
                    }
                    if (!IsValidDate(item.TargetDate))
                    {
                        item.TargetDate = Date.AddDays(7).Date;
                    }
                }
                else
                {
                    // 普通日程没有目标日期。保持默认值并在 JSON 中省略该字段，
                    // 避免 DateTime.MinValue 在本地时区转 UTC 时发生下溢。
                    item.TargetDate = default(DateTime);
                }
            }
        }

        [Obsolete("请使用 ThermalScheduleItem.GetCountdownText(DateTime)。")]
        public string GetCountdownText()
        {
            return new ThermalScheduleItem
            {
                Content = string.IsNullOrWhiteSpace(CountdownName) ? "目标日" : CountdownName,
                TargetDate = IsValidDate(CountdownDate) ? CountdownDate : Date
            }.GetCountdownText(Date);
        }

        private static bool IsValidDate(DateTime value)
        {
            return value.Year >= 1753 && value.Year <= 9998;
        }

        private static string Limit(string value, int maximumLength)
        {
            return value.Length <= maximumLength ? value : value.Substring(0, maximumLength);
        }

        public ThermalScheduleDocument DeepClone()
        {
            ThermalScheduleDocument clone = new ThermalScheduleDocument
            {
                Title = Title,
                Date = Date,
                AutoDate = AutoDate,
                ShowDate = ShowDate,
                ShowCheckboxes = ShowCheckboxes,
                ShowTime = ShowTime,
                ShowContent = ShowContent,
                FontFamily = FontFamily,
                TitleFontFamily = TitleFontFamily,
                TitleFontSizeMm = TitleFontSizeMm,
                TitleBold = TitleBold,
                TitleItalic = TitleItalic,
                BodyFontSizeMm = BodyFontSizeMm,
                MarginMm = MarginMm,
                RowSpacingMm = RowSpacingMm,
                Copies = Copies,
                ShowCountdown = ShowCountdown,
                CountdownName = CountdownName,
                CountdownDate = CountdownDate,
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
