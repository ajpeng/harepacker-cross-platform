/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HaCreator.MapEditor
{
    /// <summary>
    /// Pure SkiaSharp offscreen renderer for the map editor canvas.
    /// Replaces MonoGame/MapEditorGame — no SDL, OpenGL, or main-thread constraints.
    /// Renders 30 fps on a background thread; frames are copied into a shared byte buffer
    /// for Avalonia to display via WriteableBitmap.
    /// </summary>
    internal sealed class SkiaEditorRenderer
    {
        private readonly MultiBoard _multiBoard;
        private volatile int _width;
        private volatile int _height;
        private volatile bool _needRecreate;

        private SKBitmap? _skBitmap;
        private SKSurface? _surface;

        private readonly object _bufferLock = new();
        private byte[]? _frameBuffer;
        private volatile bool _hasNewFrame;
        private volatile bool _running;
        private Thread? _thread;

        public SkiaEditorRenderer(MultiBoard multiBoard, int width, int height)
        {
            _multiBoard = multiBoard;
            _width  = Math.Max(width,  1);
            _height = Math.Max(height, 1);
        }

        public void Start()
        {
            _running = true;
            // Mark the board as ready immediately so MapEditorControl starts its refresh timer
            _multiBoard.DeviceReady = true;
            _thread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Name = "SkiaEditor-Render",
            };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
        }

        public void Resize(int w, int h)
        {
            if (w < 1 || h < 1) return;
            _width  = w;
            _height = h;
            _needRecreate = true;
            _multiBoard.CurrentDXWindowSize = new System.Drawing.Size(w, h);
        }

        /// <summary>
        /// Copies the latest rendered frame into <paramref name="dest"/> and returns true.
        /// Returns false when no new frame is available since the last call.
        /// </summary>
        public bool TryGetFrame(byte[] dest)
        {
            if (!_hasNewFrame) return false;
            lock (_bufferLock)
            {
                if (!_hasNewFrame || _frameBuffer == null) return false;
                if (_frameBuffer.Length != dest.Length) return false; // sizes diverged during resize — skip frame
                Array.Copy(_frameBuffer, dest, dest.Length);
                _hasNewFrame = false;
                return true;
            }
        }

        // ── Background render loop ─────────────────────────────────────────

        private void RenderLoop()
        {
            var interval = TimeSpan.FromSeconds(1.0 / 30);
            while (_running)
            {
                var sw = Stopwatch.StartNew();
                try { RenderOneFrame(); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SkiaEditorRenderer] frame error: {ex.Message}");
                }
                sw.Stop();
                var wait = interval - sw.Elapsed;
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            }
            _surface?.Dispose();
            _skBitmap?.Dispose();
            _surface   = null;
            _skBitmap  = null;
        }

        private void RenderOneFrame()
        {
            if (_needRecreate)
            {
                _needRecreate = false;
                _surface?.Dispose();
                _skBitmap?.Dispose();
                _surface  = null;
                _skBitmap = null;
            }

            int w = _width, h = _height;

            if (_surface == null || _skBitmap == null)
            {
                _skBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                _surface  = SKSurface.Create(_skBitmap.Info, _skBitmap.GetPixels(), _skBitmap.RowBytes);
                if (_surface == null) return;
                lock (_bufferLock) { _frameBuffer = new byte[w * h * 4]; }
            }

            var canvas = _surface.Canvas;
            canvas.Clear(SKColors.White);

            _multiBoard.RenderFrameSK(canvas, w, h);

            canvas.Flush();

            lock (_bufferLock)
            {
                if (_frameBuffer != null && _frameBuffer.Length == w * h * 4)
                {
                    Marshal.Copy(_skBitmap.GetPixels(), _frameBuffer, 0, _frameBuffer.Length);
                    _hasNewFrame = true;
                }
            }
        }
    }
}
