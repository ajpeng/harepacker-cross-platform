using SkiaSharp;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator
{
    public class UIFrameHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawUIFrame(SKCanvas canvas, SKColor backgroundColor, int targetImageWidth, int targetImageHeight)
        {
            DrawUIFrame(canvas, backgroundColor,
                null, null, null, null, null, null, null, null, null, 0,
                targetImageWidth, targetImageHeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawUIFrame(SKCanvas canvas,
            SKColor backgroundColor,
            SKBitmap ne, SKBitmap nw, SKBitmap se, SKBitmap sw,
            SKBitmap e, SKBitmap w, SKBitmap n, SKBitmap s,
            SKBitmap c, int startBgYPaint,
            int targetImageWidth, int targetImageHeight)
        {
            float fillLeft   = w != null ? (w.Width / 3f)  : 2f;
            float fillTop    = n != null ? (n.Height / 3f) : 2f;
            float fillWidth  = w != null ? (targetImageWidth  - (w.Width  / 2f) - 1f) : (targetImageWidth  - 1f);
            float fillHeight = n != null ? (targetImageHeight - (n.Height / 2f) - 1f) : (targetImageHeight - 1f);

            using (var bgPaint = new SKPaint { Color = backgroundColor, IsStroke = false })
            {
                canvas.DrawRect(fillLeft, fillTop, fillWidth, fillHeight, bgPaint);
            }

            if (c != null && c.Width > 0 && c.Height > 0)
            {
                int startX = (int)fillLeft;
                int startY = (int)fillTop + startBgYPaint;
                int tileWidth  = (int)fillWidth;
                int tileHeight = (int)fillHeight + 5 - startBgYPaint;

                for (int ty = startY; ty < startY + tileHeight; ty += c.Height)
                    for (int tx = startX; tx < startX + tileWidth; tx += c.Width)
                        canvas.DrawBitmap(c, tx, ty);
            }

            if (n != null && s != null && w != null && e != null && ne != null && sw != null && nw != null && se != null)
            {
                const int MARGIN_HORIZONTAL_BORDER_PX = 1;
                int rightEdgeX   = targetImageWidth  - e.Width  + MARGIN_HORIZONTAL_BORDER_PX;
                int rightCornerX = targetImageWidth  - ne.Width + MARGIN_HORIZONTAL_BORDER_PX;
                int bottomEdgeY  = targetImageHeight - sw.Height;

                for (int i = nw.Width; i <= (targetImageWidth - nw.Width); i += n.Width)
                    canvas.DrawBitmap(n, i, 0);

                for (int i = sw.Width; i <= (targetImageWidth - sw.Width); i += s.Width)
                    canvas.DrawBitmap(s, i, targetImageHeight - s.Height);

                for (int i = bottomEdgeY; i >= nw.Height; i -= w.Height)
                    canvas.DrawBitmap(w, 0, i);

                for (int i = bottomEdgeY; i >= nw.Height; i -= e.Height)
                    canvas.DrawBitmap(e, rightEdgeX, i);

                canvas.DrawBitmap(nw, 0, 0);
                canvas.DrawBitmap(ne, rightCornerX, 0);
                canvas.DrawBitmap(sw, 0, bottomEdgeY);
                canvas.DrawBitmap(se, rightCornerX, bottomEdgeY);
            }
        }
    }
}
