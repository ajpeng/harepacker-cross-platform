using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Footholds;
using MapleLib.WzLib.WzProperties;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HaRepacker.FHMapper
{
    public class DisplayMapWindow : Window
    {
        public List<object> Settings { get; set; } = new();

        private readonly double _scale;
        private readonly List<FootHold.Foothold> _footholds;
        private readonly List<Portals.Portal> _portals;
        private readonly List<SpawnPoint.Spawnpoint> _spawnPoints;
        private int _xOffset;
        private int _yOffset;
        private readonly Avalonia.Controls.Image _imageControl;

        public DisplayMapWindow(
            SKBitmap mapBitmap,
            double scale,
            List<FootHold.Foothold> footholds,
            List<Portals.Portal> portals,
            List<SpawnPoint.Spawnpoint> spawnPoints,
            List<object> settings)
        {
            _scale = scale;
            _footholds = footholds;
            _portals = portals;
            _spawnPoints = spawnPoints;
            Settings = settings;

            Title = "Map Display";
            Width = 900;
            Height = 700;

            _imageControl = new Avalonia.Controls.Image { Stretch = Stretch.None };
            Content = new ScrollViewer { Content = _imageControl };

            _imageControl.PointerMoved += OnPointerMoved;
            _imageControl.PointerPressed += OnPointerPressed;

            // Convert and load bitmap now (mapBitmap ownership transferred here; we dispose it)
            int scaledW = Math.Max(1, (int)(mapBitmap.Width * scale));
            int scaledH = Math.Max(1, (int)(mapBitmap.Height * scale));

            Bitmap? avBitmap = null;
            if (Math.Abs(scale - 1.0) < 0.001)
            {
                avBitmap = SkBitmapToAvalonia(mapBitmap);
            }
            else
            {
                using var scaled = new SKBitmap(scaledW, scaledH);
                using var canvas = new SKCanvas(scaled);
                canvas.DrawBitmap(mapBitmap, SKRect.Create(0, 0, scaledW, scaledH));
                avBitmap = SkBitmapToAvalonia(scaled);
            }
            mapBitmap.Dispose();
            _imageControl.Source = avBitmap;

            // Compute coordinate offsets from first portal
            if (_portals.Count > 0)
            {
                var fp = _portals[0];
                _xOffset = (int)((fp.Shape.X + 20 - ((WzIntProperty)fp.Data["x"]).Value) * -1);
                _yOffset = (int)((fp.Shape.Y + 20 - ((WzIntProperty)fp.Data["y"]).Value) * -1);
            }
        }

        private static Bitmap SkBitmapToAvalonia(SKBitmap bmp)
        {
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            return new Bitmap(ms);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pos = e.GetPosition(_imageControl);
            Title = $"Map X: {(int)(_xOffset + pos.X / _scale)} Y: {(int)(_yOffset + pos.Y / _scale)}";
        }

        private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var pos = e.GetPosition(_imageControl);
            int mx = (int)pos.X;
            int my = (int)pos.Y;
            var click = new System.Drawing.Rectangle(mx, my, 1, 1);

            foreach (var fh in _footholds)
            {
                var r = new System.Drawing.Rectangle(
                    (int)(fh.Shape.X * _scale), (int)(fh.Shape.Y * _scale),
                    (int)(fh.Shape.Width * _scale), (int)(fh.Shape.Height * _scale));
                if (r.IntersectsWith(click))
                {
                    var dlg = new EditFootholdWindow(fh, Settings) { Title = "Edit Foothold: " + fh.Data.Name };
                    await dlg.ShowDialog(this);
                    return;
                }
            }
            foreach (var portal in _portals)
            {
                var r = new System.Drawing.Rectangle(
                    (int)(portal.Shape.X * _scale), (int)(portal.Shape.Y * _scale),
                    (int)(portal.Shape.Width * _scale), (int)(portal.Shape.Height * _scale));
                if (r.IntersectsWith(click))
                {
                    var dlg = new EditPortalsWindow(portal, Settings) { Title = "Edit Portal: " + portal.Data.Name };
                    await dlg.ShowDialog(this);
                    return;
                }
            }
            foreach (var sp in _spawnPoints)
            {
                var r = new System.Drawing.Rectangle(
                    (int)(sp.Shape.X * _scale), (int)(sp.Shape.Y * _scale),
                    (int)(sp.Shape.Width * _scale), (int)(sp.Shape.Height * _scale));
                if (r.IntersectsWith(click))
                {
                    var dlg = new SpawnpointInfoWindow(sp) { Title = "Spawnpoint: " + sp.Data.Name };
                    await dlg.ShowDialog(this);
                    return;
                }
            }
        }
    }
}
