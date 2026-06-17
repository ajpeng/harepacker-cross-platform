/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using HaCreator.MapEditor.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace HaCreator.MapEditor
{
    /// <summary>
    /// MonoGame Game subclass that renders the map canvas to an offscreen RenderTarget2D.
    /// Pixels are read back for Avalonia display via MapEditorControl.
    /// Runs on a background thread; back buffer is 1×1 and positioned off-screen.
    /// On macOS, set SDL_VIDEODRIVER=offscreen before constructing to avoid NSApp conflicts.
    /// </summary>
    internal sealed class MapEditorGame : Game
    {
        private readonly GraphicsDeviceManager _gdm;
        private readonly MultiBoard _multiBoard;
        private SpriteBatch? _sprite;
        private Texture2D? _pixel;
        private RenderTarget2D? _renderTarget;

        // Reused arrays to avoid per-frame GC allocations
        private Color[] _pixelReadback = Array.Empty<Color>();
        private byte[] _frameBuffer = Array.Empty<byte>();
        private readonly object _bufferLock = new();
        private volatile bool _hasNewFrame;

        private int _width;
        private int _height;

        /// <summary>True once LoadContent completes without exception.</summary>
        public bool InitializationSucceeded { get; private set; }

        public MapEditorGame(MultiBoard multiBoard, int width, int height)
        {
            _multiBoard = multiBoard;
            _width  = Math.Max(width,  1);
            _height = Math.Max(height, 1);

            _gdm = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = 1,
                PreferredBackBufferHeight = 1,
                SynchronizeWithVerticalRetrace = false,
            };
            IsMouseVisible    = false;
            IsFixedTimeStep   = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30);
        }

        protected override void Initialize()
        {
            // Place the tiny SDL window off-screen so it is not visible to the user
            try { Window.Position = new Point(-9999, -9999); } catch { }
            try { Window.IsBorderless = true; } catch { }

            CreateRenderTarget(_width, _height);
            _multiBoard.GraphicsDevice = GraphicsDevice;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _sprite = new SpriteBatch(GraphicsDevice);
            _pixel  = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _multiBoard.SpriteBatch = _sprite;
            _multiBoard.Pixel       = _pixel;

            try
            {
                _multiBoard.FontEngine = new FontEngine(
                    UserSettings.FontName, UserSettings.FontStyle,
                    UserSettings.FontSize, GraphicsDevice);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapEditorGame] FontEngine init failed: {ex.Message}");
            }

            _multiBoard.DeviceReady       = true;
            InitializationSucceeded       = true;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_sprite == null || _pixel == null || _renderTarget == null)
                return;
            if (Program.AbortThreads) { Exit(); return; }

            // Render the map to the offscreen RenderTarget2D
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(Color.White);
            _multiBoard.RenderFrame(_sprite, _pixel);

            // Switch back to the 1×1 back buffer before Present()
            GraphicsDevice.SetRenderTarget(null);

            // Read pixels from the render target (safe after SetRenderTarget(null))
            if (_pixelReadback.Length == _width * _height)
            {
                _renderTarget.GetData(_pixelReadback);
                lock (_bufferLock)
                {
                    for (int i = 0; i < _pixelReadback.Length; i++)
                    {
                        int ofs = i * 4;
                        ref Color c = ref _pixelReadback[i];
                        // Avalonia WriteableBitmap Bgra8888 layout: B G R A
                        _frameBuffer[ofs + 0] = c.B;
                        _frameBuffer[ofs + 1] = c.G;
                        _frameBuffer[ofs + 2] = c.R;
                        _frameBuffer[ofs + 3] = c.A;
                    }
                    _hasNewFrame = true;
                }
            }

            base.Draw(gameTime);
        }

        /// <summary>
        /// Copies the latest rendered frame into <paramref name="dest"/> and returns true.
        /// Returns false if no new frame has been rendered since the last call.
        /// </summary>
        public bool TryGetFrame(byte[] dest)
        {
            if (!_hasNewFrame) return false;
            lock (_bufferLock)
            {
                if (!_hasNewFrame) return false;
                Array.Copy(_frameBuffer, dest, Math.Min(_frameBuffer.Length, dest.Length));
                _hasNewFrame = false;
                return true;
            }
        }

        /// <summary>Resize the render target to match the Avalonia canvas size.</summary>
        public void Resize(int newWidth, int newHeight)
        {
            if (newWidth < 1 || newHeight < 1) return;
            if (newWidth == _width && newHeight == _height) return;
            _width  = newWidth;
            _height = newHeight;
            CreateRenderTarget(newWidth, newHeight);
            _multiBoard.CurrentDXWindowSize = new System.Drawing.Size(newWidth, newHeight);
        }

        private void CreateRenderTarget(int w, int h)
        {
            _renderTarget?.Dispose();
            _renderTarget   = new RenderTarget2D(GraphicsDevice, w, h, false, SurfaceFormat.Color, DepthFormat.None);
            _pixelReadback  = new Color[w * h];
            _frameBuffer    = new byte[w * h * 4];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderTarget?.Dispose();
                _pixel?.Dispose();
                _sprite?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
