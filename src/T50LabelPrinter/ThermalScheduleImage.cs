using System;
using System.Runtime.Serialization;

namespace T50LabelPrinter
{
    [DataContract]
    public sealed class ThermalScheduleImage
    {
        [DataMember(Order = 1)] public string FileName { get; set; }
        [DataMember(Order = 2)] public string ImageData { get; set; }
        [DataMember(Order = 3)] public decimal X { get; set; }
        [DataMember(Order = 4)] public decimal Y { get; set; }
        [DataMember(Order = 5)] public decimal Width { get; set; }
        [DataMember(Order = 6)] public decimal Height { get; set; }
        [DataMember(Order = 7)] public bool KeepAspect { get; set; }
        [DataMember(Order = 8)] public bool Dither { get; set; }
        [DataMember(Order = 9)] public int Threshold { get; set; }

        public ThermalScheduleImage DeepClone()
        {
            return (ThermalScheduleImage)MemberwiseClone();
        }

        public void Normalize()
        {
            Width = Math.Max(1m, Math.Min(58m, Width));
            Height = Math.Max(1m, Math.Min(990m, Height));
            X = Math.Max(0m, Math.Min(58m - Width, X));
            Y = Math.Max(0m, Math.Min(1000m - Height, Y));
            Threshold = Math.Max(0, Math.Min(255, Threshold));
        }

        public LabelElement ToLabelElement()
        {
            return new LabelElement { Kind = LabelElementKind.Image, ImageData = ImageData,
                ImageKeepAspect = false, ImageThreshold = Threshold, ImageDither = Dither };
        }

        public void ResizeFrom(ThermalScheduleImage start, int handle, decimal dx, decimal dy, bool proportional)
        {
            bool left = handle == 0 || handle == 6 || handle == 7;
            bool right = handle == 2 || handle == 3 || handle == 4;
            bool top = handle == 0 || handle == 1 || handle == 2;
            bool bottom = handle == 4 || handle == 5 || handle == 6;
            decimal width = start.Width + (left ? -dx : right ? dx : 0m);
            decimal height = start.Height + (top ? -dy : bottom ? dy : 0m);
            decimal centerX = start.X + start.Width / 2m, centerY = start.Y + start.Height / 2m;
            decimal maxWidth = left ? start.X + start.Width : right ? 58m - start.X : 2m * Math.Min(centerX, 58m - centerX);
            decimal maxHeight = top ? start.Y + start.Height : bottom ? 1000m - start.Y : 2m * Math.Min(centerY, 1000m - centerY);
            if (proportional)
            {
                decimal factor = !left && !right ? height / start.Height : !top && !bottom ? width / start.Width :
                    Math.Abs(width / start.Width - 1m) >= Math.Abs(height / start.Height - 1m)
                        ? width / start.Width : height / start.Height;
                factor = Math.Max(Math.Max(1m / start.Width, 1m / start.Height),
                    Math.Min(factor, Math.Min(maxWidth / start.Width, maxHeight / start.Height)));
                width = start.Width * factor;
                height = start.Height * factor;
            }
            else
            {
                width = Math.Max(1m, Math.Min(maxWidth, width));
                height = Math.Max(1m, Math.Min(maxHeight, height));
            }
            X = left ? start.X + start.Width - width : right ? start.X : centerX - width / 2m;
            Y = top ? start.Y + start.Height - height : bottom ? start.Y : centerY - height / 2m;
            Width = width;
            Height = height;
        }
    }
}
