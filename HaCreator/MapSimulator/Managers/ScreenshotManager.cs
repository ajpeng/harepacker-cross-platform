using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Managers
{
    public class ScreenshotManager
    {
        private bool _saveScreenshot = false;
        private bool _saveScreenshotComplete = true;

        public bool TakeScreenshot
        {
            get => _saveScreenshot;
            set => _saveScreenshot = value;
        }

        public bool IsComplete => _saveScreenshotComplete;

        public void RequestScreenshot() => _saveScreenshot = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessScreenshot(GraphicsDevice graphicsDevice)
        {
            if (!_saveScreenshotComplete || !_saveScreenshot) return;
            _saveScreenshot = false;

            int w = graphicsDevice.PresentationParameters.BackBufferWidth;
            int h = graphicsDevice.PresentationParameters.BackBufferHeight;
            var pixels = new byte[w * h * 4];
            graphicsDevice.GetBackBufferData(pixels);

            DateTime now = DateTime.Now;
            string fileName = string.Format("Maple_{0}{1}{2}_{3}{4}{5}.png",
                now.Day.ToString("D2"), now.Month.ToString("D2"), (now.Year - 2000).ToString("D2"),
                now.Hour.ToString("D2"), now.Minute.ToString("D2"), now.Second.ToString("D2"));

            using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmp.GetPixels(), pixels.Length);
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = File.OpenWrite(fileName);
            data.SaveTo(fs);

            _saveScreenshotComplete = true;
        }
    }
}
