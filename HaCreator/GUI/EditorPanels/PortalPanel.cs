/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Avalonia code-only UserControl for the portal placement panel.
    /// Shows a scrollable thumbnail grid of every portal type.
    /// </summary>
    public class PortalPanel : UserControl
    {
        private readonly WrapPanel _grid;

        public PortalPanel()
        {
            _grid = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            Content = new ScrollViewer
            {
                Content = _grid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
        }

        /// <summary>
        /// Populate the portal thumbnail grid and wire click handlers.
        /// </summary>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            foreach (PortalType pt in Program.InfoManager.PortalEditor_TypeById)
            {
                try
                {
                    PortalInfo? pInfo = PortalInfo.GetPortalInfoByType(pt);
                    if (pInfo == null || pInfo.Image == null)
                        continue;

                    Avalonia.Media.Imaging.Bitmap? bmp = SkBitmapToAvalonia(pInfo.Image);
                    string label = PortalTypeExtensions.GetFriendlyName(pt);

                    Button btn = MakeThumb(bmp, label, pInfo);
                    btn.Click += (s, _) =>
                    {
                        lock (hcsm.MultiBoard)
                        {
                            hcsm.EnterEditMode(ItemTypes.Portals);
                            hcsm.MultiBoard.SelectedBoard?.Mouse.SetHeldInfo((PortalInfo)((Button)s!).Tag!);
                            hcsm.MultiBoard.Focus();
                        }
                    };

                    _grid.Children.Add(btn);
                }
                catch (KeyNotFoundException)
                {
                    // Portal type has no registered info — skip silently
                }
                catch (Exception)
                {
                    // Any other per-portal failure — skip silently
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Avalonia.Media.Imaging.Bitmap? SkBitmapToAvalonia(SKBitmap? sk)
        {
            if (sk == null || sk.Width <= 0 || sk.Height <= 0) return null;
            try
            {
                using var data = sk.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Position = 0;
                return new Avalonia.Media.Imaging.Bitmap(ms);
            }
            catch { return null; }
        }

        private static Button MakeThumb(Avalonia.Media.Imaging.Bitmap? bmp, string label, object? tag)
        {
            Control imgCtrl = bmp != null
                ? (Control)new Image { Source = bmp, Width = 64, Height = 64, Stretch = Stretch.Uniform }
                : new Border { Width = 64, Height = 64, Background = Brushes.LightGray };

            var btn = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Width = 72,
                    Children =
                    {
                        imgCtrl,
                        new TextBlock
                        {
                            Text = label,
                            FontSize = 9,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 72,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                },
                Padding = new Thickness(2),
                Margin = new Thickness(2),
                Tag = tag
            };
            return btn;
        }
    }
}
