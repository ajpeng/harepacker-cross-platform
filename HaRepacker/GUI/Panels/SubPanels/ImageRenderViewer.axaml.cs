using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HaSharedLibrary.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class ImageRenderViewer : UserControl
    {
        private SKBitmap? _originalBitmap;
        private SKBitmap? _filteredBitmap;
        private readonly ScaleTransform _imageScale = new ScaleTransform(3, 3);

        public ImageRenderViewer()
        {
            InitializeComponent();
            imageBorder.RenderTransform = _imageScale;
        }

        public void SetImage(SKBitmap? bitmap)
        {
            _originalBitmap = bitmap;
            _filteredBitmap = bitmap;
            RenderImage(bitmap);
            ResetFilter();

            if (bitmap != null)
            {
                propertyList.ItemsSource = new[]
                {
                    new KeyValuePair<string, string>("Width", bitmap.Width.ToString()),
                    new KeyValuePair<string, string>("Height", bitmap.Height.ToString()),
                    new KeyValuePair<string, string>("ColorType", bitmap.ColorType.ToString()),
                };
            }
        }

        private void RenderImage(SKBitmap? bitmap)
        {
            if (bitmap == null)
            {
                canvasPropBox.Source = null;
                return;
            }
            using SKImage img = SKImage.FromBitmap(bitmap);
            using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
            using MemoryStream ms = new MemoryStream(data.ToArray());
            ms.Seek(0, SeekOrigin.Begin);
            canvasPropBox.Source = new Bitmap(ms);
        }

        private void ZoomSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            _imageScale.ScaleX = e.NewValue;
            _imageScale.ScaleY = e.NewValue;
            if (zoomLabel != null)
                zoomLabel.Text = $"Zoom: {e.NewValue:F1}x";
        }

        private void button_filter_apply_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;
            byte r = (byte)(sliderR.Value);
            byte g = (byte)(sliderG.Value);
            byte b = (byte)(sliderB.Value);
            byte a = (byte)(sliderA.Value);
            var skColor = new SKColor(r, g, b, a);
            _filteredBitmap = BitmapHelper.ApplyColorFilter(_originalBitmap, skColor);
            RenderImage(_filteredBitmap);
        }

        private void button_filter_reset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _filteredBitmap = _originalBitmap;
            RenderImage(_originalBitmap);
            ResetFilter();
        }

        private void ResetFilter()
        {
            sliderR.Value = 255; numR.Value = 255;
            sliderG.Value = 255; numG.Value = 255;
            sliderB.Value = 255; numB.Value = 255;
            sliderA.Value = 255; numA.Value = 255;
        }
    }
}
