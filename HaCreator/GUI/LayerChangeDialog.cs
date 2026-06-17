/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    public static class LayerChangeDialog
    {
        // Called while the board lock may be held — dispatch to UI thread
        public static void DispatchAsync(List<BoardItem> items, Board board, Window? owner)
        {
            // Capture state before releasing the lock
            var layers = board.Layers.Select(l => l.ToString()).ToList();
            var zmsPerLayer = board.Layers.Select(l => l.zMList.ToList()).ToList();
            int initLayer  = Math.Max(0, board.SelectedLayerIndex);
            int initZm     = board.SelectedPlatform;

            Dispatcher.UIThread.Post(async () =>
                await ShowAsync(items, board, layers, zmsPerLayer, initLayer, initZm, owner));
        }

        private static async Task ShowAsync(
            List<BoardItem> items, Board board,
            List<string> layers, List<List<int>> zmsPerLayer,
            int initLayerIdx, int initZm, Window? owner)
        {
            var cbxLayer = new ComboBox { ItemsSource = layers, SelectedIndex = initLayerIdx, MinWidth = 180 };
            var cbxZm    = new ComboBox { MinWidth = 100 };

            // Populate zm list for current layer
            void RefreshZms()
            {
                int li = cbxLayer.SelectedIndex >= 0 ? cbxLayer.SelectedIndex : 0;
                var zms = zmsPerLayer.Count > li ? zmsPerLayer[li] : new List<int>();
                cbxZm.ItemsSource  = zms;
                int idx = zms.IndexOf(initZm);
                cbxZm.SelectedIndex = idx >= 0 ? idx : (zms.Count > 0 ? 0 : -1);
            }
            cbxLayer.SelectionChanged += (_, _) => RefreshZms();
            RefreshZms();

            var lblError = new TextBlock { Foreground = Avalonia.Media.Brushes.Red };
            var btnOK    = new Button { Content = "OK",     IsDefault = true };
            var btnCancel= new Button { Content = "Cancel", IsCancel  = true };

            bool confirmed = false;
            var win = new Window
            {
                Title = "Change Layer / Platform",
                Width = 340, Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 8,
                    Children =
                    {
                        LR("Layer:",    cbxLayer),
                        LR("Platform:", cbxZm),
                        lblError,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 6, Children = { btnOK, btnCancel }
                        }
                    }
                }
            };
            btnOK.Click     += (_, _) => { confirmed = true; win.Close(); };
            btnCancel.Click += (_, _) => win.Close();
            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
            if (!confirmed) return;

            int targetLayerIdx = cbxLayer.SelectedIndex >= 0 ? cbxLayer.SelectedIndex : 0;
            if (cbxZm.SelectedItem is not int zm) return;

            lock (board.ParentControl)
            {
                Layer targetLayer = board.Layers[targetLayerIdx];

                // Tile tS check
                if (targetLayer.tS != null)
                {
                    foreach (var item in items)
                    {
                        if (item is TileInstance && ((TileInfo)item.BaseInfo).tS != targetLayer.tS)
                        {
                            Dispatcher.UIThread.Post(() => lblError.Text =
                                "Target layer cannot hold selected tiles (tS mismatch).");
                            return;
                        }
                    }
                }

                var actions      = new List<UndoRedoAction>();
                var touchedLayers= new HashSet<int>();
                foreach (var item in items)
                {
                    if (item is not IContainsLayerInfo li) continue;
                    int oldLayer = li.LayerNumber;
                    int oldZm    = li.PlatformNumber;
                    touchedLayers.Add(oldLayer);
                    li.LayerNumber    = targetLayer.LayerNumber;
                    li.PlatformNumber = zm;
                    actions.Add(UndoRedoManager.ItemLayerPlatChanged(li,
                        new Tuple<int,int>(oldLayer, oldZm),
                        new Tuple<int,int>(li.LayerNumber, li.PlatformNumber)));
                }
                if (actions.Count > 0)
                    board.UndoRedoMan.AddUndoBatch(actions);

                foreach (int l in touchedLayers) board.Layers[l].RecheckTileSet();
                targetLayer.RecheckTileSet();
                InputHandler.ClearSelectedItems(board);
            }
        }

        static StackPanel LR(string label, Control ctrl)
            => new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    new TextBlock { Text = label, Width = 80, VerticalAlignment = VerticalAlignment.Center },
                    ctrl
                }
            };
    }
}
