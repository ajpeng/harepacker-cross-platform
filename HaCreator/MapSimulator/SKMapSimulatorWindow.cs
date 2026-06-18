/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using MapleLib.WzLib.WzStructure.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Point = Avalonia.Point;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Cross-platform map simulator built on Avalonia + SkiaSharp.
    /// Renders the map using the board's existing SK pipeline, plus a player character
    /// with foothold physics (gravity, landing, movement, jumping).
    /// Controls: A/D or ←/→ Move, W/↑/Space Jump, MMB drag / Wheel Camera, ESC Close.
    /// </summary>
    public sealed class SKMapSimulatorWindow : Window
    {
        private readonly MultiBoard _editorBoard;
        private readonly Board      _board;

        // ── Display ──────────────────────────────────────────────────────────────
        // Stretch.Fill: bitmap always fills the full window regardless of exact pixel match.
        private readonly Image _image = new()
        {
            Stretch             = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
        };
        private WriteableBitmap? _bitmap;
        private byte[]?          _displayBuffer;

        // ── Render thread ─────────────────────────────────────────────────────────
        private volatile bool    _running;
        private Thread?          _renderThread;
        private readonly object  _bufferLock = new();
        private byte[]?          _frameBuffer;
        private volatile bool    _hasNewFrame;
        private volatile int     _renderW = 800;
        private volatile int     _renderH = 600;

        private readonly DispatcherTimer _refreshTimer;

        // ── Camera state (volatile: written from render thread AND UI thread) ─────
        private volatile int _hScroll;
        private volatile int _vScroll;
        private volatile int _maxHScroll;
        private volatile int _maxVScroll;

        // ── Middle-click pan ──────────────────────────────────────────────────────
        private bool  _isPanning;
        private Point _panLast;

        // ── Player state (render-thread only) ────────────────────────────────────
        private float _playerX;
        private float _playerY;
        private float _playerVY;
        private bool  _playerOnGround;
        private int   _playerFacing = 1; // 1 = right, -1 = left
        private float _spawnX;
        private float _spawnY;

        private const int   PlayerW      = 28;
        private const int   PlayerH      = 48;
        private const float Gravity      = 1800f; // px/s²
        private const float MaxFallSpeed = 700f;  // px/s
        private const float MoveSpeed    = 200f;  // px/s
        private const float JumpVelocity = -520f; // px/s (up)
        private const float PhysDt       = 1f / 30f;

        // ── Input (UI thread → render thread via volatile) ────────────────────────
        private volatile bool _keyLeft;
        private volatile bool _keyRight;
        private volatile bool _keyJumpPending;

        // ── Foothold cache (built once at init, read-only after) ─────────────────
        private (float x1, float y1, float x2, float y2)[] _footholds
            = Array.Empty<(float, float, float, float)>();

        // ── Error recovery ────────────────────────────────────────────────────────
        private string? _lastError;

        // ────────────────────────────────────────────────────────────────────────────
        //  Construction
        // ────────────────────────────────────────────────────────────────────────────

        public SKMapSimulatorWindow(MultiBoard editorBoard, Board board, string title)
        {
            _editorBoard = editorBoard;
            _board       = board;

            CacheFootholds();
            InitPlayer();

            Title  = $"Map Simulation — {title}";
            Width  = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Focusable = true;

            _image.IsHitTestVisible = false;
            Content = _image;

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
            };
            _refreshTimer.Tick += OnRefreshTick;

            // SizeChanged fires before Opened — use it to capture the actual content size
            // and pre-allocate at the right dimensions.
            SizeChanged += OnSizeChanged;

            // Opened fires after first layout — _renderW/_renderH are already set correctly.
            Opened  += (_, _) => { Focus(); StartRender(); };
            Closing += (_, _) => StopRender();
        }

        // ── Initialization ────────────────────────────────────────────────────────

        private void CacheFootholds()
        {
            var list = new List<(float, float, float, float)>();
            lock (_editorBoard)
            {
                foreach (FootholdLine fh in _board.BoardItems.FootholdLines)
                {
                    if (fh.SecondDot == null) continue;
                    if (fh.IsWall) continue; // vertical walls are not walkable surfaces
                    list.Add((fh.FirstDot.X, fh.FirstDot.Y, fh.SecondDot.X, fh.SecondDot.Y));
                }
            }
            Console.Error.WriteLine($"[SKSim] Cached {list.Count} walkable footholds");
            _footholds = list.ToArray();
        }

        private void InitPlayer()
        {
            PortalInstance? spawn = null;
            lock (_editorBoard)
            {
                spawn = _board.BoardItems.Portals
                    .FirstOrDefault(p => p.pt == PortalType.StartPoint);
            }

            if (spawn != null)
            {
                _spawnX = spawn.X;
                _spawnY = spawn.Y - PlayerH;
            }
            else
            {
                _spawnX = 0;
                _spawnY = 0;
            }
            RespawnPlayer();
        }

        private void RespawnPlayer()
        {
            _playerX        = _spawnX;
            _playerY        = _spawnY;
            _playerVY       = 0f;
            _playerOnGround = false;
        }

        // ── Camera helpers ────────────────────────────────────────────────────────

        // VirtualToPhysical: screenX = worldX + centerPoint.X - hScroll
        // To centre player on screen: hScroll = playerX + cpX - viewW/2
        private void RecenterCamera(int viewW, int viewH)
        {
            int cpX = _board.CenterPoint.X;
            int cpY = _board.CenterPoint.Y;
            _maxHScroll = Math.Max(0, _board.MapSize.X - viewW);
            _maxVScroll = Math.Max(0, _board.MapSize.Y - viewH);
            _hScroll = Math.Clamp((int)(_playerX + cpX - viewW * 0.5f),    0, _maxHScroll);
            _vScroll = Math.Clamp((int)(_playerY + cpY - viewH * 0.65f),   0, _maxVScroll);
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void StartRender()
        {
            // Use actual window content size (_renderW/_renderH set by SizeChanged already).
            int w = _renderW, h = _renderH;
            RecenterCamera(w, h);
            AllocateBitmap(w, h);

            _running      = true;
            _renderThread = new Thread(RenderLoop) { IsBackground = true, Name = "SKSim-Render" };
            _renderThread.Start();
            _refreshTimer.Start();
        }

        private void StopRender()
        {
            _refreshTimer.Stop();
            _running = false;
        }

        private void AllocateBitmap(int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(w, h),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            _displayBuffer = new byte[w * h * 4];
            _image.Source  = _bitmap;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            int w = Math.Max(1, (int)e.NewSize.Width);
            int h = Math.Max(1, (int)e.NewSize.Height);
            _renderW = w;
            _renderH = h;

            // Re-centre camera on player for new view dimensions
            RecenterCamera(w, h);

            // If the renderer is already running, pre-allocate so the UI thread
            // has the right bitmap ready; the render thread sees _renderW/_renderH change
            // and will recreate its SKBitmap on the next frame.
            if (_running)
                AllocateBitmap(w, h);
        }

        // ── Display refresh (UI thread, ~30 fps) ──────────────────────────────────

        private int _tickCount; // diagnostic

        private void OnRefreshTick(object? sender, EventArgs e)
        {
            int w = _renderW, h = _renderH;
            if (w <= 0 || h <= 0) return;

            // Reallocate only when the bitmap dimensions changed (should be rare after init).
            if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
                AllocateBitmap(w, h);

            bool wrote = false;
            lock (_bufferLock)
            {
                if (!_hasNewFrame || _frameBuffer == null || _displayBuffer == null)
                {
                    int tc = ++_tickCount;
                    if (tc <= 5 || tc % 60 == 0)
                        Console.Error.WriteLine($"[SKSim] Tick {tc}: no new frame (hasNew={_hasNewFrame} fb={_frameBuffer?.Length} db={_displayBuffer?.Length})");
                    return;
                }
                if (_frameBuffer.Length != _displayBuffer.Length)
                {
                    Console.Error.WriteLine($"[SKSim] Tick size mismatch fb={_frameBuffer.Length} db={_displayBuffer.Length}");
                    return;
                }
                Array.Copy(_frameBuffer, _displayBuffer, _frameBuffer.Length);
                _hasNewFrame = false;
                wrote = true;
            }

            if (wrote && _bitmap != null && _displayBuffer != null)
            {
                using var fb = _bitmap.Lock();
                Marshal.Copy(_displayBuffer, 0, fb.Address, _displayBuffer.Length);
                _image.InvalidateVisual();
            }
        }

        // ── Physics (render thread, ~30 fps) ──────────────────────────────────────

        private void UpdatePhysics(int viewW, int viewH)
        {
            float vx = 0f;
            if (_keyLeft)  { vx = -MoveSpeed; _playerFacing = -1; }
            if (_keyRight) { vx =  MoveSpeed; _playerFacing =  1; }

            if (!_playerOnGround)
                _playerVY = Math.Min(_playerVY + Gravity * PhysDt, MaxFallSpeed);

            if (_keyJumpPending)
            {
                _keyJumpPending = false;
                if (_playerOnGround)
                {
                    _playerVY       = JumpVelocity;
                    _playerOnGround = false;
                }
            }

            float prevX = _playerX;
            float prevY = _playerY;
            float newX  = prevX + vx      * PhysDt;
            float newY  = prevY + _playerVY * PhysDt;

            bool  landed  = false;
            float bestFhY = float.MaxValue;

            foreach (var (x1, y1, x2, y2) in _footholds)
            {
                float xMin = Math.Min(x1, x2);
                float xMax = Math.Max(x1, x2);
                if (newX < xMin || newX > xMax) continue;

                float fhY = Math.Abs(x2 - x1) < 0.1f
                    ? Math.Min(y1, y2)
                    : y1 + (y2 - y1) * (newX - x1) / (x2 - x1);

                bool crossedDown = _playerVY >= 0 && prevY <= fhY + 2f && newY >= fhY;
                bool stayingOn   = _playerOnGround && prevY <= fhY + 4f && prevY >= fhY - 32f;

                if ((crossedDown || stayingOn) && fhY < bestFhY)
                {
                    bestFhY = fhY;
                    landed  = true;
                }
            }

            if (landed)
            {
                newY            = bestFhY;
                _playerVY       = 0f;
                _playerOnGround = true;
            }
            else if (_playerOnGround)
            {
                _playerOnGround = false;
            }

            // Clamp X to map bounds
            newX = Math.Clamp(newX, -_board.CenterPoint.X, _board.MapSize.X - _board.CenterPoint.X);

            // Respawn if player fell past the map bottom (death boundary)
            float mapBottom = _board.MapSize.Y - _board.CenterPoint.Y;
            if (newY > mapBottom + 200f)
            {
                RespawnPlayer();
                return;
            }

            // Clamp ceiling
            float mapTop = -_board.CenterPoint.Y;
            if (newY < mapTop) { newY = mapTop; _playerVY = 0f; }

            _playerX = newX;
            _playerY = newY;

            // Camera follows player
            int cpX = _board.CenterPoint.X, cpY = _board.CenterPoint.Y;
            _hScroll = Math.Clamp((int)(_playerX + cpX - viewW * 0.5f),    0, _maxHScroll);
            _vScroll = Math.Clamp((int)(_playerY + cpY - viewH * 0.65f),   0, _maxVScroll);
        }

        // ── Render loop (background thread) ───────────────────────────────────────

        private int _frameCount; // diagnostic

        private void RenderLoop()
        {
            Console.Error.WriteLine("[SKSim] RenderLoop starting");
            var interval = TimeSpan.FromMilliseconds(33);
            SKBitmap? skBmp  = null;
            SKSurface? surf  = null;
            byte[]? localBuf = null;
            int curW = 0, curH = 0;

            while (_running)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    int w = _renderW, h = _renderH;
                    if (w != curW || h != curH || skBmp == null)
                    {
                        Console.Error.WriteLine($"[SKSim] Allocating {w}x{h} bitmap");
                        surf?.Dispose();
                        skBmp?.Dispose();
                        skBmp    = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                        surf     = SKSurface.Create(skBmp.Info, skBmp.GetPixels(), skBmp.RowBytes);
                        localBuf = new byte[w * h * 4];
                        lock (_bufferLock) { _frameBuffer = null; }
                        curW = w; curH = h;

                        // Push a "loading" frame immediately — dark navy — to verify display pipeline.
                        surf.Canvas.Clear(new SKColor(10, 20, 60));
                        PushFrame(surf.Canvas, skBmp, localBuf, w, h);
                        Console.Error.WriteLine($"[SKSim] Loading frame pushed ({w}x{h})");
                    }

                    UpdatePhysics(curW, curH);

                    if (surf != null && localBuf != null)
                    {
                        RenderOneFrame(surf.Canvas, skBmp, localBuf, curW, curH);
                        int fc = ++_frameCount;
                        if (fc <= 3 || fc % 60 == 0)
                            Console.Error.WriteLine($"[SKSim] Frame {fc} ok");
                    }
                }
                catch (Exception ex)
                {
                    _lastError = $"{ex.GetType().Name}: {ex.Message}";
                    Console.Error.WriteLine($"[SKSim] RenderOneFrame error (frame {_frameCount}): {_lastError}");
                    Console.Error.WriteLine(ex.StackTrace);
                    bool pushed = false;
                    try
                    {
                        if (surf != null && skBmp != null && localBuf != null)
                        {
                            PushErrorFrame(surf.Canvas, skBmp, localBuf, curW, curH);
                            pushed = true;
                        }
                    }
                    catch (Exception ex2)
                    {
                        Console.Error.WriteLine($"[SKSim] PushErrorFrame also failed: {ex2.GetType().Name}: {ex2.Message}");
                        Console.Error.WriteLine(ex2.StackTrace);
                    }
                    if (!pushed && localBuf != null)
                    {
                        // Raw-pixel fallback — magenta fill — bypasses all canvas operations.
                        for (int i = 0; i < localBuf.Length; i += 4)
                        { localBuf[i] = 255; localBuf[i + 1] = 0; localBuf[i + 2] = 255; localBuf[i + 3] = 255; }
                        lock (_bufferLock)
                        {
                            if (_frameBuffer == null || _frameBuffer.Length != localBuf.Length)
                                _frameBuffer = new byte[localBuf.Length];
                            Array.Copy(localBuf, _frameBuffer, localBuf.Length);
                            _hasNewFrame = true;
                        }
                        Console.Error.WriteLine("[SKSim] Raw magenta fallback frame pushed");
                    }
                }

                sw.Stop();
                var wait = interval - sw.Elapsed;
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            }

            Console.Error.WriteLine($"[SKSim] RenderLoop exiting after {_frameCount} frames");
            surf?.Dispose();
            skBmp?.Dispose();
        }

        private void RenderOneFrame(SKCanvas canvas, SKBitmap skBmp, byte[] localBuf, int w, int h)
        {
            canvas.Clear(SKColors.Black);

            int snapH = _hScroll;
            int snapV = _vScroll;

            lock (_editorBoard)
            {
                int savedH  = _board.hScroll;
                int savedV  = _board.vScroll;
                var savedSz = _editorBoard.CurrentDXWindowSize;
                _board.hScroll = snapH;
                _board.vScroll = snapV;
                _editorBoard.CurrentDXWindowSize = new System.Drawing.Size(w, h);
                try
                {
                    _board.RenderBackgroundsSK(canvas);
                    canvas.Save();
                    try   { _board.RenderBoardSK(canvas); }
                    finally { canvas.Restore(); }
                    _board.RenderFrontBackgroundsSK(canvas);
                    _board.RenderMinimapSK(canvas);
                }
                finally
                {
                    _board.hScroll = savedH;
                    _board.vScroll = savedV;
                    _editorBoard.CurrentDXWindowSize = savedSz;
                }
            }

            DrawPlayer(canvas, snapH, snapV);
            DrawHUD(canvas, w, h);

            PushFrame(canvas, skBmp, localBuf, w, h);
        }

        private void PushFrame(SKCanvas canvas, SKBitmap skBmp, byte[] localBuf, int w, int h)
        {
            canvas.Flush();
            int copyLen = Math.Min(localBuf.Length, skBmp.ByteCount);
            Marshal.Copy(skBmp.GetPixels(), localBuf, 0, copyLen);
            lock (_bufferLock)
            {
                if (_frameBuffer == null || _frameBuffer.Length != localBuf.Length)
                    _frameBuffer = new byte[localBuf.Length];
                Array.Copy(localBuf, _frameBuffer, localBuf.Length);
                _hasNewFrame = true;
            }
        }

        private void PushErrorFrame(SKCanvas canvas, SKBitmap skBmp, byte[] localBuf, int w, int h)
        {
            canvas.Clear(new SKColor(30, 0, 0));
            using var p = new SKPaint { Color = SKColors.Red, TextSize = 14f, IsAntialias = true };
            canvas.DrawText("Map Simulator error:", 10f, 30f, p);
            p.Color    = SKColors.OrangeRed;
            p.TextSize = 11f;
            string err = _lastError ?? "unknown";
            for (int i = 0; i * 90 < err.Length; i++)
                canvas.DrawText(err.Substring(i * 90, Math.Min(90, err.Length - i * 90)), 10f, 55f + i * 16f, p);
            PushFrame(canvas, skBmp, localBuf, w, h);
        }

        // ── Player rendering ──────────────────────────────────────────────────────

        private void DrawPlayer(SKCanvas canvas, int hScroll, int vScroll)
        {
            int cpX = _board.CenterPoint.X, cpY = _board.CenterPoint.Y;
            // screenX = worldX + cpX - hScroll
            float sx = _playerX + cpX - hScroll;
            float sy = _playerY + cpY - vScroll;

            // Body
            using var body = new SKPaint { Color = new SKColor(50, 170, 50, 230) };
            canvas.DrawRect(SKRect.Create(sx - PlayerW * 0.5f, sy - PlayerH, PlayerW, PlayerH * 0.72f), body);

            // Head
            using var head = new SKPaint { Color = new SKColor(255, 215, 130, 230), IsAntialias = true };
            float headCY = sy - PlayerH - 11f;
            canvas.DrawCircle(sx, headCY, 12f, head);

            // Eye (facing indicator)
            using var eye = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawCircle(sx + _playerFacing * 5f, headCY - 2f, 3f, eye);

            // Feet line
            using var feet = new SKPaint { Color = new SKColor(180, 80, 20, 200), StrokeWidth = 2f, IsStroke = true };
            canvas.DrawLine(sx - 9f, sy, sx + 9f, sy, feet);
        }

        private static void DrawHUD(SKCanvas canvas, int w, int h)
        {
            using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 160) };
            canvas.DrawRect(SKRect.Create(4f, h - 20f, 360f, 16f), bg);
            using var txt = new SKPaint { Color = SKColors.White, TextSize = 11f, IsAntialias = true };
            canvas.DrawText("PLAY MODE  |  A/D ←/→ Move   W/↑/Space Jump   MMB/Wheel Camera   ESC Close",
                            8f, h - 7f, txt);
        }

        // ── Keyboard ──────────────────────────────────────────────────────────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Left:
                case Key.A:     _keyLeft        = true;  e.Handled = true; break;
                case Key.Right:
                case Key.D:     _keyRight       = true;  e.Handled = true; break;
                case Key.Up:
                case Key.W:
                case Key.Space: _keyJumpPending = true;  e.Handled = true; break;
                case Key.Escape: Close();                e.Handled = true; break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            switch (e.Key)
            {
                case Key.Left:  case Key.A: _keyLeft  = false; e.Handled = true; break;
                case Key.Right: case Key.D: _keyRight = false; e.Handled = true; break;
                case Key.Up: case Key.W: case Key.Space:        e.Handled = true; break;
            }
        }

        // ── Mouse wheel (manual camera override) ──────────────────────────────────

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            e.Handled = true;
            const int Speed = 60;
            int dy = (int)Math.Round(e.Delta.Y * Speed);
            int dx = (int)Math.Round(e.Delta.X * Speed);
            if (dy != 0) _vScroll = Math.Clamp(_vScroll - dy, 0, _maxVScroll);
            if (dx != 0) _hScroll = Math.Clamp(_hScroll + dx, 0, _maxHScroll);
        }

        // ── Middle-click drag (manual camera override) ────────────────────────────

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _panLast   = e.GetPosition(this);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_isPanning) return;
            var pt = e.GetPosition(this);
            _hScroll = Math.Clamp(_hScroll - (int)Math.Round(pt.X - _panLast.X), 0, _maxHScroll);
            _vScroll = Math.Clamp(_vScroll - (int)Math.Round(pt.Y - _panLast.Y), 0, _maxVScroll);
            _panLast  = pt;
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_isPanning && e.InitialPressMouseButton == MouseButton.Middle)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }
    }
}
