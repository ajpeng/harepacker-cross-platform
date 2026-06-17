using System;
using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIImage : IHaUIRenderable {
        private HaUIInfo _info;

        public HaUIImage(HaUIInfo bitmapInfo) {
            this._info = bitmapInfo;

            if (bitmapInfo.Bitmap == null) {
                throw new ArgumentException("Bitmap cannot be null for HaUIImage");
            }
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            throw new Exception("Not supported in HaUIImage.");
        }

        public SKBitmap Render() {
            HaUISize size = GetSize();
            int totalWidth = size.Width;
            int totalHeight = size.Height;

            var bitmap = new SKBitmap(totalWidth, totalHeight);
            using (var canvas = new SKCanvas(bitmap)) {
                canvas.Clear(SKColors.Transparent);

                int x = HaUIHelper.CalculateAlignmentOffset(_info.Bitmap.Width, totalWidth, _info.HorizontalAlignment);
                int y = HaUIHelper.CalculateAlignmentOffset(_info.Bitmap.Height, totalHeight, _info.VerticalAlignment);

                canvas.DrawBitmap(_info.Bitmap, new SKRect(x, y, x + _info.Bitmap.Width, y + _info.Bitmap.Height));
            }

            return bitmap;
        }

        public HaUISize GetSize() {
            int width = _info.Bitmap.Width + _info.Margins.Bottom;
            int height = _info.Bitmap.Height + _info.Margins.Top;
            width += _info.Padding.Top;
            height += _info.Padding.Bottom;
            return new HaUISize(
                Math.Max(_info.MinWidth, width),
                Math.Max(_info.MinHeight, height));
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
