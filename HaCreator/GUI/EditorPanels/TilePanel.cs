/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Avalonia code-only UserControl for the tile placement panel.
    /// Shows a ComboBox for tile set selection and a scrollable thumbnail grid.
    /// </summary>
    public class TilePanel : UserControl
    {
        private HaCreatorStateManager? _hcsm;
        private HotSwapRefreshService? _hotSwapService;

        private readonly ComboBox _tileSetCombo;
        private readonly WrapPanel _grid;
        private bool _suppressSelectionChanged;

        public TilePanel()
        {
            _tileSetCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(2)
            };

            _grid = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            var scroll = new ScrollViewer
            {
                Content = _grid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };

            // Use a Grid with two rows: Auto (ComboBox) and * (ScrollViewer fills remainder)
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));

            Grid.SetRow(_tileSetCombo, 0);
            Grid.SetRow(scroll, 1);

            rootGrid.Children.Add(_tileSetCombo);
            rootGrid.Children.Add(scroll);

            Content = rootGrid;

            _tileSetCombo.SelectionChanged += (_, _) =>
            {
                if (!_suppressSelectionChanged)
                    LoadTileSetList();
            };
        }

        /// <summary>
        /// Initialize the panel, populate the tile set combo, and register with the state manager.
        /// </summary>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            _hcsm = hcsm;
            hcsm.SetTilePanel(this);

            _suppressSelectionChanged = true;
            try
            {
                var sorted = Program.InfoManager.TileSets.Keys
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
                foreach (string name in sorted)
                    _tileSetCombo.Items.Add(name);
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        /// <summary>
        /// Set the active tile set by name and reload the thumbnail grid.
        /// </summary>
        public void SetSelectedTileSet(string tileSet)
        {
            if (_tileSetCombo.SelectedItem is string current && current == tileSet)
            {
                // Already selected — still reload in case content changed
                LoadTileSetList();
                return;
            }

            _suppressSelectionChanged = true;
            try
            {
                _tileSetCombo.SelectedItem = tileSet;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
            LoadTileSetList();
        }

        /// <summary>
        /// Reload the thumbnail grid from the currently selected tile set.
        /// Called externally by the state manager and on combo selection change.
        /// </summary>
        public void LoadTileSetList()
        {
            if (_hcsm == null) return;

            lock (_hcsm.MultiBoard)
            {
                if (_tileSetCombo.SelectedItem is not string selectedSetName)
                    return;
                if (!Program.InfoManager.TileSets.ContainsKey(selectedSetName))
                    return;

                WzImage? tileSetImage = Program.InfoManager.GetTileSet(selectedSetName);
                if (tileSetImage == null)
                    return;

                int? mag = InfoTool.GetOptionalInt(tileSetImage["info"]?["mag"]);

                // Build button list on background thread, then add to grid on UI thread
                var buttons = new List<Button>();

                foreach (WzSubProperty tCat in tileSetImage.WzProperties)
                {
                    if (tCat.Name == "info")
                        continue;

                    if (ApplicationSettings.randomTiles)
                    {
                        WzCanvasProperty? canvasProp = tCat["0"] as WzCanvasProperty;
                        if (canvasProp == null)
                            continue;

                        SKBitmap? sk = canvasProp.GetLinkedWzCanvasBitmap();
                        Avalonia.Media.Imaging.Bitmap? bmp = SkBitmapToAvalonia(sk);

                        TileInfo[] randomInfos = new TileInfo[tCat.WzProperties.Count];
                        for (int i = 0; i < randomInfos.Length; i++)
                            randomInfos[i] = TileInfo.Get(selectedSetName, tCat.Name, tCat.WzProperties[i].Name, mag);

                        Button btn = MakeThumb(bmp, tCat.Name, randomInfos);
                        btn.Click += (s, _) => TileItem_Click((Button)s!);
                        buttons.Add(btn);
                    }
                    else
                    {
                        foreach (WzCanvasProperty tile in tCat.WzProperties.OfType<WzCanvasProperty>())
                        {
                            SKBitmap? sk = tile.GetLinkedWzCanvasBitmap();
                            Avalonia.Media.Imaging.Bitmap? bmp = SkBitmapToAvalonia(sk);
                            TileInfo info = TileInfo.Get(selectedSetName, tCat.Name, tile.Name, mag);
                            string label = tCat.Name + "/" + tile.Name;

                            Button btn = MakeThumb(bmp, label, info);
                            btn.Click += (s, _) => TileItem_Click((Button)s!);
                            buttons.Add(btn);
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    _grid.Children.Clear();
                    foreach (var b in buttons)
                        _grid.Children.Add(b);
                });
            }
        }

        private void TileItem_Click(Button btn)
        {
            if (_hcsm == null) return;

            MultiBoard multiBoard = _hcsm.MultiBoard;

            lock (multiBoard)
            {
                if (!multiBoard.AssertLayerSelected())
                    return;

                Layer layer = multiBoard.SelectedBoard!.SelectedLayer;

                TileInfo infoToAdd = ApplicationSettings.randomTiles
                    ? ((TileInfo[])btn.Tag!)[0]
                    : (TileInfo)btn.Tag!;

                if (layer.tS != null && infoToAdd.tS != layer.tS)
                {
                    // Must not await while holding the lock — release and show dialog async
                    string oldTS = layer.tS;
                    string newTS = infoToAdd.tS;

                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        bool confirmed = await ShowTSChangeConfirmDialogAsync(oldTS, newTS);
                        if (!confirmed) return;

                        lock (multiBoard)
                        {
                            if (!multiBoard.AssertLayerSelected()) return;
                            Layer l = multiBoard.SelectedBoard!.SelectedLayer;

                            // Re-check in case the user changed layers while the dialog was open
                            if (l.tS != null && l.tS != newTS)
                            {
                                var actions = new List<UndoRedoAction>
                                {
                                    UndoRedoManager.LayerTSChanged(l, l.tS, newTS)
                                };
                                l.ReplaceTS(newTS);
                                multiBoard.SelectedBoard.UndoRedoMan.AddUndoBatch(actions);
                            }

                            CommitTileSelection(multiBoard, btn);
                        }
                    });
                    return;
                }

                CommitTileSelection(multiBoard, btn);
            }
        }

        private void CommitTileSelection(MultiBoard multiBoard, Button btn)
        {
            if (_hcsm == null) return;
            _hcsm.EnterEditMode(ItemTypes.Tiles);
            if (ApplicationSettings.randomTiles)
                multiBoard.SelectedBoard!.Mouse.SetRandomTilesMode((TileInfo[])btn.Tag!);
            else
                multiBoard.SelectedBoard!.Mouse.SetHeldInfo((TileInfo)btn.Tag!);
            multiBoard.Focus();
        }

        private async System.Threading.Tasks.Task<bool> ShowTSChangeConfirmDialogAsync(string oldTS, string newTS)
        {
            Window? owner = _hcsm?.OwnerWindow ??
                (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow : null);

            var dlg = new Window
            {
                Title = "Layer Tile Set Change",
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            bool result = false;

            var yesBtn = new Button { Content = "Yes", Width = 80, Margin = new Thickness(4) };
            var noBtn  = new Button { Content = "No",  Width = 80, Margin = new Thickness(4) };

            yesBtn.Click += (_, _) => { result = true;  dlg.Close(); };
            noBtn.Click  += (_, _) => { result = false; dlg.Close(); };

            dlg.Content = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"This action will change the layer tile set from \"{oldTS}\" to \"{newTS}\". Proceed?",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 4,
                        Children = { yesBtn, noBtn }
                    }
                }
            };

            if (owner != null)
                await dlg.ShowDialog(owner);
            else
                dlg.Show();

            return result;
        }

        /// <summary>
        /// Subscribe to HotSwap tile set change events.
        /// Body intentionally empty — hot-swap support is pending for the Avalonia port.
        /// </summary>
        public void SubscribeToHotSwap(HotSwapRefreshService svc)
        {
            // stub — hot-swap support pending
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
