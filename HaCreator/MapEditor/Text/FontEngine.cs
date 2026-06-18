/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
using HaSharedLibrary.Util;
using System;
using System.Collections.Generic;

namespace HaCreator.MapEditor.Text
{
    /// <summary>
    /// Cross-platform font rendering engine using SkiaSharp (replaces System.Drawing.Font).
    /// Pre-rasterizes the ASCII character set into Texture2D for MonoGame rendering.
    /// </summary>
    public class FontEngine
    {
        private readonly GraphicsDevice device;
        private readonly SKTypeface typeface;
        private readonly float size;
        private readonly CharTexture[] characters = new CharTexture[0x100];
        private readonly SKPaint measurePaint;

        public FontEngine(string fontName, SKFontStyle fontStyle, float size, GraphicsDevice device)
        {
            this.device = device;
            this.size = size;
            typeface = SKTypeface.FromFamilyName(fontName, fontStyle) ?? SKTypeface.Default;
            measurePaint = new SKPaint
            {
                Typeface = typeface,
                TextSize = size,
                IsAntialias = true,
            };

            for (char ch = (char)0; ch < 0x100; ch++)
                characters[ch] = RasterizeCharacter(ch);
        }

        private CharTexture RasterizeCharacter(char ch)
        {
            string text = ch.ToString();
            float w = measurePaint.MeasureText(text);
            if (w < 1) w = size * 0.5f;
            int iw = Math.Max(1, (int)Math.Ceiling(w));
            int ih = Math.Max(1, (int)Math.Ceiling(size));

            using SKBitmap bmp = new SKBitmap(iw, ih, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Transparent);
                using var paint = new SKPaint
                {
                    Typeface = typeface,
                    TextSize = size,
                    IsAntialias = true,
                    Color = SKColors.White,
                };
                canvas.DrawText(text, 0, ih - 2, paint);
            }

            return new CharTexture(bmp.ToTexture2D(device), iw, ih);
        }

        public void DrawString(SpriteBatch sprite, System.Drawing.Point position, Color color, string str, int maxWidth)
        {
            if (string.IsNullOrEmpty(str)) return;
            if (UserSettings.ClipText)
            {
                float totalW = measurePaint.MeasureText(str);
                if (totalW > maxWidth)
                {
                    float dotsW = measurePaint.MeasureText("...");
                    while (str.Length > 0 && measurePaint.MeasureText(str) + dotsW > maxWidth)
                        str = str[..^1];
                    str += "...";
                }
            }

            int xOffs = 0;
            foreach (char c in str)
            {
                if (c >= 0x100) return;
                CharTexture ct = characters[c];
                if (ct?.texture != null)
                {
                    sprite.Draw(ct.texture, new Rectangle(position.X + xOffs, position.Y, ct.w, ct.h), color);
                    xOffs += ct.w;
                }
            }
        }

        public void DrawStringSK(SKCanvas canvas, System.Drawing.Point position, SKColor color, string str, int maxWidth)
        {
            if (string.IsNullOrEmpty(str)) return;
            if (UserSettings.ClipText)
            {
                float totalW = measurePaint.MeasureText(str);
                if (totalW > maxWidth)
                {
                    float dotsW = measurePaint.MeasureText("...");
                    while (str.Length > 0 && measurePaint.MeasureText(str) + dotsW > maxWidth)
                        str = str[..^1];
                    str += "...";
                }
            }
            using var paint = new SKPaint
            {
                Typeface = typeface,
                TextSize = size,
                IsAntialias = true,
                Color = color,
            };
            canvas.DrawText(str, position.X, position.Y + (int)Math.Ceiling(size), paint);
        }

        public System.Drawing.SizeF MeasureString(string s)
        {
            if (string.IsNullOrEmpty(s)) return System.Drawing.SizeF.Empty;
            float w = measurePaint.MeasureText(s);
            return new System.Drawing.SizeF(w, size);
        }
    }
}
