using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HaSharedLibrary.Util
{
    public static class BitmapHelper
    {
        /// <summary>
        /// Converts an SKBitmap to a MonoGame Texture2D by encoding as PNG and letting MonoGame decode it.
        /// </summary>
        public static Texture2D ToTexture2D(this SKBitmap bitmap, GraphicsDevice device)
        {
            if (bitmap == null)
                return null;

            using SKImage img = SKImage.FromBitmap(bitmap);
            using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
            using MemoryStream ms = new MemoryStream(data.ToArray());
            ms.Seek(0, SeekOrigin.Begin);
            return Texture2D.FromStream(device, ms);
        }

        /// <summary>
        /// Applies a per-channel color filter to an SKBitmap (multiplicative tint).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SKBitmap ApplyColorFilter(SKBitmap originalBitmap, SKColor filterColor)
        {
            SKBitmap result = new SKBitmap(originalBitmap.Width, originalBitmap.Height,
                SKColorType.Bgra8888, SKAlphaType.Unpremul);

            float rScale = filterColor.Red   / 255f;
            float gScale = filterColor.Green / 255f;
            float bScale = filterColor.Blue  / 255f;
            float aScale = filterColor.Alpha / 255f;

            using SKCanvas canvas = new SKCanvas(result);
            using SKPaint paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    rScale, 0,      0,      0, 0,
                    0,      gScale, 0,      0, 0,
                    0,      0,      bScale, 0, 0,
                    0,      0,      0,      aScale, 0,
                })
            };
            canvas.DrawBitmap(originalBitmap, 0, 0, paint);
            return result;
        }

        /// <summary>
        /// Returns the size of an SKBitmap encoded as PNG, in kilobytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetImageSizeInKB(SKBitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            using SKImage img = SKImage.FromBitmap(bitmap);
            using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.Size / 1024.0;
        }
    }
}
