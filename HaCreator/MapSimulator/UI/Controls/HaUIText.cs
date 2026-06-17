using System;
using SkiaSharp;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIText : IHaUIRenderable {
        private string _text;
        private readonly SKPaint _paint;

        private HaUIInfo _info;

        public HaUIText(string text, SKColor color, string fontFamily, float fontSize, float userScreenScaleFactor) {
            this._text = text;
            this._paint = new SKPaint {
                TextSize = fontSize / userScreenScaleFactor,
                Color = color,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(fontFamily) ?? SKTypeface.Default,
            };
            this._info = new HaUIInfo();
        }

        public void AddRenderable(IHaUIRenderable renderable) {
            throw new Exception("Not supported in HaUIText.");
        }

        public SKBitmap Render() {
            HaUISize size = GetSize();

            var textBitmap = new SKBitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
            using var canvas = new SKCanvas(textBitmap);
            canvas.Clear(SKColors.Transparent);

            _paint.GetFontMetrics(out SKFontMetrics metrics);
            float lineHeight = -metrics.Ascent + metrics.Descent;
            float y = _info.Margins.Top - metrics.Ascent;
            foreach (var line in _text.Split('\n')) {
                canvas.DrawText(line, _info.Margins.Left, y, _paint);
                y += lineHeight;
            }

            return textBitmap;
        }

        public HaUISize GetSize() {
            HaUISize size = GetTextSizeWithoutMargins();
            return new HaUISize(size.Width + _info.Margins.Left + _info.Margins.Right, size.Height + _info.Margins.Top + _info.Margins.Bottom);
        }

        private HaUISize GetTextSizeWithoutMargins() {
            _paint.GetFontMetrics(out SKFontMetrics metrics);
            float lineHeight = -metrics.Ascent + metrics.Descent;
            var lines = _text.Split('\n');
            float maxWidth = 0;
            foreach (var line in lines) {
                float w = _paint.MeasureText(line);
                if (w > maxWidth) maxWidth = w;
            }
            float totalHeight = lineHeight * lines.Length;
            return new HaUISize((int)Math.Ceiling(maxWidth), (int)Math.Ceiling(totalHeight));
        }

        public HaUIInfo GetInfo() {
            return _info;
        }
    }
}
