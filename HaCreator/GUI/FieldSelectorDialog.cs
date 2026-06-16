/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    /// <summary>
    /// Simplified cross-platform map browser for loading maps from WZ data.
    /// Replaces the WinForms FieldSelector / MapBrowser combo.
    /// </summary>
    public class FieldSelectorDialog : Window
    {
        private readonly MultiBoard _multiBoard;
        private readonly TabControl _tabs;
        private readonly EventHandler[] _rightClickHandlers;
        private readonly bool _autoCloseOnSelect;

        private readonly TextBox _searchBox;
        private readonly ListBox _mapList;
        private readonly TextBlock _statusLabel;
        private readonly Button _btnLoad;

        // Full list of (displayString, mapId) pairs, built once on open
        private List<(string display, string mapId)> _allMaps = new();

        public FieldSelectorDialog(MultiBoard multiBoard, TabControl tabs, EventHandler[] rightClickHandlers,
            bool autoCloseOnSelect = false, string? defaultFilter = null)
        {
            _multiBoard = multiBoard;
            _tabs = tabs;
            _rightClickHandlers = rightClickHandlers;
            _autoCloseOnSelect = autoCloseOnSelect;

            Title = "Open Map";
            Width = 500;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Padding = new Thickness(12);

            _searchBox = new TextBox { Watermark = "Search by name or map ID...", Margin = new Thickness(0, 0, 0, 6) };
            _searchBox.TextChanged += (_, _) => FilterList(_searchBox.Text ?? string.Empty);

            _mapList = new ListBox { Height = 340 };
            _mapList.DoubleTapped += (_, _) => LoadSelected();

            _statusLabel = new TextBlock
            {
                Text = "Loading map list...",
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 0)
            };

            _btnLoad = new Button { Content = "Load", IsEnabled = false };
            _btnLoad.Click += (_, _) => LoadSelected();

            var btnCancel = new Button { Content = "Cancel" };
            btnCancel.Click += (_, _) => Close();

            _mapList.SelectionChanged += (_, _) => _btnLoad.IsEnabled = _mapList.SelectedItem != null;

            Content = new DockPanel
            {
                Children =
                {
                    D(new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { btnCancel, _btnLoad }
                    }, Dock.Bottom),
                    D(_statusLabel, Dock.Bottom),
                    _searchBox,
                    new ScrollViewer { Content = _mapList }
                }
            };

            Opened += async (_, _) =>
            {
                await Task.Run(BuildMapList);
                if (!string.IsNullOrEmpty(defaultFilter))
                    _searchBox.Text = defaultFilter;
                else
                    FilterList(string.Empty);
            };
        }

        private static Control D(Control c, Dock dock) { DockPanel.SetDock(c, dock); return c; }

        private void BuildMapList()
        {
            var list = new List<(string display, string mapId)>();

            var nameCache = Program.InfoManager?.MapsNameCache;
            if (nameCache != null)
            {
                foreach (var kvp in nameCache)
                {
                    string mapId   = kvp.Key;
                    string street  = kvp.Value.Item2 ?? string.Empty;
                    string name    = kvp.Value.Item1 ?? string.Empty;
                    string display = $"[{mapId}] {street}: {name}";
                    list.Add((display, mapId));
                }
            }

            // Sort by map ID numerically
            list.Sort((a, b) =>
            {
                if (int.TryParse(a.mapId, out int ia) && int.TryParse(b.mapId, out int ib))
                    return ia.CompareTo(ib);
                return string.Compare(a.mapId, b.mapId, StringComparison.Ordinal);
            });

            _allMaps = list;

            Dispatcher.UIThread.Post(() =>
            {
                FilterList(_searchBox.Text ?? string.Empty);
                _statusLabel.Text = $"{list.Count} maps available";
            });
        }

        private void FilterList(string query)
        {
            query = query.Trim();
            IEnumerable<string> items = string.IsNullOrEmpty(query)
                ? _allMaps.Select(m => m.display)
                : _allMaps
                    .Where(m => m.display.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.display);

            _mapList.ItemsSource = items.ToList();
            _btnLoad.IsEnabled = false;
        }

        private void LoadSelected()
        {
            string? selected = _mapList.SelectedItem as string;
            if (selected == null) return;

            // Extract the map ID from "[000000000] ..." format
            if (!selected.StartsWith("[") || selected.Length < 11)
            {
                Debug.WriteLine("[FieldSelectorDialog] Cannot parse map ID from selection");
                return;
            }

            string mapIdStr = selected.Substring(1, 9); // "000000000"
            if (!int.TryParse(mapIdStr, out int mapId))
            {
                Debug.WriteLine($"[FieldSelectorDialog] Non-numeric map ID: {mapIdStr}");
                return;
            }

            _statusLabel.Text = $"Loading map {mapIdStr}...";
            _statusLabel.Foreground = Brushes.Gray;
            _btnLoad.IsEnabled = false;

            // Resolve names from cache
            string mapName = "NO NAME", streetName = "NO NAME", categoryName = "NO NAME";
            if (Program.InfoManager.MapsNameCache.TryGetValue(mapIdStr, out var names))
            {
                mapName      = names.Item1 ?? mapName;
                streetName   = names.Item2 ?? streetName;
                categoryName = names.Item3 ?? categoryName;
            }

            // Try MapsCache first (has pre-loaded WzImage)
            WzImage? mapImage = null;
            MapInfo? info = null;

            if (Program.InfoManager.MapsCache.TryGetValue(mapIdStr, out var cached))
            {
                mapImage     = cached.Item1;
                mapName      = cached.Item2 ?? mapName;
                streetName   = cached.Item3 ?? streetName;
                categoryName = cached.Item4 ?? categoryName;
                info         = cached.Item5;
            }

            // If WzImage not cached, find on demand
            if (mapImage == null)
                mapImage = WzInfoTools.FindMapImage(mapIdStr, Program.WzManager);

            if (mapImage == null)
            {
                _statusLabel.Text = $"Map image not found for ID {mapIdStr}";
                _statusLabel.Foreground = Brushes.DarkRed;
                _btnLoad.IsEnabled = true;
                return;
            }

            info ??= new MapInfo(mapImage, mapName, streetName, categoryName);

            try
            {
                MapLoader.CreateMapFromImage(mapId, mapImage.DeepClone(), info,
                    mapName, streetName, categoryName,
                    _tabs, _multiBoard, _rightClickHandlers);

                _statusLabel.Text = $"Loaded: [{mapIdStr}] {streetName}: {mapName}";
                _statusLabel.Foreground = Brushes.Green;

                if (_autoCloseOnSelect)
                    Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error loading map: {ex.Message}";
                _statusLabel.Foreground = Brushes.DarkRed;
                Debug.WriteLine($"[FieldSelectorDialog] Load error: {ex}");
            }
            finally
            {
                _btnLoad.IsEnabled = true;
            }
        }
    }
}
