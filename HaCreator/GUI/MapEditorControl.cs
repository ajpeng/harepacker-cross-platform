/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaCreator.MapEditor;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Point = Avalonia.Point;

namespace HaCreator.GUI
{
    /// <summary>
    /// Avalonia control that hosts the MonoGame map-editor canvas.
    /// MonoGame renders to an offscreen RenderTarget2D on a background thread;
    /// frames are copied to a WriteableBitmap every ~33 ms for display.
    /// Mouse and keyboard events are forwarded to MultiBoard for editing logic.
    /// </summary>
    public sealed class MapEditorControl : UserControl
    {
        private readonly MultiBoard _multiBoard;
        private readonly Image _image = new() { Stretch = Stretch.Fill };
        private WriteableBitmap? _bitmap;
        private byte[]? _displayBuffer;

        private MapEditorGame? _game;
        private Thread? _gameThread;
        private readonly DispatcherTimer _refreshTimer;

        // Track last pointer position for move deduplication
        private int _lastPx, _lastPy;

        public MapEditorControl(MultiBoard multiBoard)
        {
            _multiBoard = multiBoard;
            multiBoard.HostControl = this;

            Background = Brushes.DarkGray;
            Focusable  = true;
            DragDrop.SetAllowDrop(this, true);

            Content = _image;

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent,     OnDrop);

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromSeconds(1.0 / 30)
            };
            _refreshTimer.Tick += OnRefreshTick;

            SizeChanged += OnSizeChanged;
            DoubleTapped += OnDoubleTapped;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            StartMonoGame();
            _refreshTimer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _refreshTimer.Stop();
            _game?.Exit();
            base.OnDetachedFromVisualTree(e);
        }

        // ── MonoGame startup ───────────────────────────────────────────────

        private void StartMonoGame()
        {
            int w = Math.Max((int)Bounds.Width,  800);
            int h = Math.Max((int)Bounds.Height, 600);
            _multiBoard.CurrentDXWindowSize = new System.Drawing.Size(w, h);

            // On macOS, SDL_VIDEODRIVER=offscreen avoids NSApp main-thread conflicts
            if (OperatingSystem.IsMacOS())
                Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "offscreen");

            _gameThread = new Thread(() =>
            {
                try
                {
                    _game = new MapEditorGame(_multiBoard, w, h);
                    _game.Run();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MapEditorControl] MonoGame failed: {ex}");
                }
            })
            {
                IsBackground = true,
                Name = "MonoGame-Render"
            };
            _gameThread.Start();
        }

        // ── Frame display ──────────────────────────────────────────────────

        private void OnRefreshTick(object? sender, EventArgs e)
        {
            if (_game == null || !_multiBoard.DeviceReady) return;

            int w = _multiBoard.CurrentDXWindowSize.Width;
            int h = _multiBoard.CurrentDXWindowSize.Height;
            if (w <= 0 || h <= 0) return;

            // Recreate bitmap when size changes
            if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(
                    new PixelSize(w, h),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);
                _displayBuffer = new byte[w * h * 4];
                _image.Source  = _bitmap;
            }

            if (_displayBuffer != null && _game.TryGetFrame(_displayBuffer))
            {
                using var fb = _bitmap.Lock();
                Marshal.Copy(_displayBuffer, 0, fb.Address, _displayBuffer.Length);
            }
        }

        // ── Resize ────────────────────────────────────────────────────────

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            int w = (int)e.NewSize.Width;
            int h = (int)e.NewSize.Height;
            if (w > 0 && h > 0)
                _game?.Resize(w, h);
        }

        // ── Pointer events ────────────────────────────────────────────────

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pt = e.GetPosition(this);
            int x = (int)pt.X, y = (int)pt.Y;
            if (x == _lastPx && y == _lastPy) return;
            _lastPx = x; _lastPy = y;
            _multiBoard.HandleMouseMove(x, y);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            var pt    = e.GetPosition(this);
            int x = (int)pt.X, y = (int)pt.Y;
            var props = e.GetCurrentPoint(this).Properties;
            bool left  = props.IsLeftButtonPressed;
            bool right = props.IsRightButtonPressed;
            _multiBoard.HandleMouseDown(x, y, left, right);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            var pt    = e.GetPosition(this);
            int x = (int)pt.X, y = (int)pt.Y;
            var props = e.GetCurrentPoint(this).Properties;
            // Properties reflect state AFTER release: check InitialPressMouseButton
            bool left  = e.InitialPressMouseButton == MouseButton.Left;
            bool right = e.InitialPressMouseButton == MouseButton.Right;
            _multiBoard.HandleMouseUp(x, y, left, right);
        }

        private void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            var pt = e.GetPosition(this);
            _multiBoard.HandleMouseDoubleClick((int)pt.X, (int)pt.Y);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            // Delta.Y > 0 = wheel up; match original WinForms convention (positive = scroll right/up)
            int delta = (int)(e.Delta.Y * UserSettings.ScrollDistance);
            _multiBoard.HandleMouseWheel(delta);
        }

        // ── Drag-drop ─────────────────────────────────────────────────────

        private static readonly string[] _imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.GetFiles() is { } files &&
                files.Any(f => _imageExtensions.Contains(
                    Path.GetExtension(f.Name).ToLowerInvariant())))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            Board? board;
            lock (_multiBoard) { board = _multiBoard.SelectedBoard; }
            if (board == null) return;

            var pt  = e.GetPosition(this);
            var pos = new Microsoft.Xna.Framework.Point((int)pt.X, (int)pt.Y);

            var files = e.Data.GetFiles()?
                .Where(f => _imageExtensions.Contains(
                    Path.GetExtension(f.Name).ToLowerInvariant()))
                .ToList();
            if (files == null || files.Count == 0) return;

            foreach (var file in files)
            {
                string path = file.Path.LocalPath;
                try
                {
                    using var data = SKData.Create(path);
                    var bmp = SKBitmap.Decode(data);
                    if (bmp == null) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    _multiBoard.OnImageDropped(board, bmp, name, pos);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MapEditorControl] Drop failed for '{path}': {ex.Message}");
                }
            }
            e.Handled = true;
        }

        // ── Keyboard events ───────────────────────────────────────────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            bool ctrl  = (e.KeyModifiers & KeyModifiers.Control) != 0;
            bool shift = (e.KeyModifiers & KeyModifiers.Shift)   != 0;
            bool alt   = (e.KeyModifiers & KeyModifiers.Alt)     != 0;
            _multiBoard.CtrlHeld = ctrl;
            _multiBoard.HandleKeyDown(ctrl, shift, alt, e.Key);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            _multiBoard.CtrlHeld = (e.KeyModifiers & KeyModifiers.Control) != 0;
        }
    }
}
