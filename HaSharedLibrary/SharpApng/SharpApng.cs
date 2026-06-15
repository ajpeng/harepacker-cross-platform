/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using SkiaSharp;
using System;
using System.Collections.Generic;

namespace HaSharedLibrary.SharpApng
{

    public class SharpApng : IDisposable
    {
        private readonly List<SharpApngFrame> m_frames = new List<SharpApngFrame>();

        public SharpApng()
        {
        }

        public void Dispose()
        {
            foreach (SharpApngFrame frame in m_frames)
                frame.Dispose();
            m_frames.Clear();
        }

        public SharpApngFrame this[int index]
        {
            get
            {
                if (index < m_frames.Count) return m_frames[index];
                else return null;
            }
            set
            {
                if (index < m_frames.Count) m_frames[index] = value;
            }
        }

        public void AddFrame(SharpApngFrame frame)
        {
            m_frames.Add(frame);
        }

        public void AddFrame(SKBitmap bmp, int num, int den)
        {
            m_frames.Add(new SharpApngFrame(bmp, num, den));
        }

        private SKBitmap ExtendImage(SKBitmap source, int newWidth, int newHeight)
        {
            SKBitmap result = new SKBitmap(newWidth, newHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using SKCanvas canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, 0, 0);
            return result;
        }

        // TODO: implement cross-platform APNG encoding (SharpApngBasicWrapper requires apng64/apng32.dll).
        public void WriteApng(string path, bool firstFrameHidden, bool disposeAfter)
        {
            throw new PlatformNotSupportedException(
                "APNG export requires the Windows-only apng64/apng32.dll native library. " +
                "Cross-platform APNG encoding is not yet implemented.");
        }
    }
}
