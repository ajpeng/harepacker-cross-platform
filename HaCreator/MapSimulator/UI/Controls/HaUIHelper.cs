using SkiaSharp;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUIHelper {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateAlignmentOffset(int total, int child, HaUIAlignment alignment) {
            switch (alignment) {
                case HaUIAlignment.Center: return (total - child) / 2;
                case HaUIAlignment.End:   return total - child;
                default:                  return 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SKBitmap RenderAndMergeMinimapUIFrame(HaUIStackPanel toDrawUI, SKColor color_bgFill,
            SKBitmap ne, SKBitmap nw, SKBitmap se, SKBitmap sw,
            SKBitmap e, SKBitmap w, SKBitmap n, SKBitmap s,
            SKBitmap c, int startBgYPaint) {
            HaUISize size = toDrawUI.GetSize();

            var finalBitmap = new SKBitmap(size.Width, size.Height);
            using (var canvas = new SKCanvas(finalBitmap)) {
                UIFrameHelper.DrawUIFrame(canvas, color_bgFill, ne, nw, se, sw, e, w, n, s, c, startBgYPaint, size.Width, size.Height);
                using var contentBmp = toDrawUI.Render();
                canvas.DrawBitmap(contentBmp, 0, 0);
            }
            return finalBitmap;
        }
    }
}
