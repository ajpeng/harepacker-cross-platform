using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIInfo {
        public SKBitmap Bitmap;
        public HaUIMargin Margins;
        public HaUIMargin Padding;
        public HaUIAlignment HorizontalAlignment = HaUIAlignment.Start;
        public HaUIAlignment VerticalAlignment = HaUIAlignment.Start;
        public int MinHeight;
        public int MinWidth;
    }
}
