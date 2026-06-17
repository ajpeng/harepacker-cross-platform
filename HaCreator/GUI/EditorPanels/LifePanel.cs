/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Avalonia code-only UserControl that lets the user browse and select
    /// life objects (Mobs, NPCs, Reactors) to place on the map.
    /// Replaces the WinForms LifePanel.
    /// </summary>
    public class LifePanel : UserControl
    {
        // ── Data lists ──────────────────────────────────────────────────

        private readonly List<string> _reactors = new();
        private readonly List<string> _npcs = new();
        private readonly List<string> _mobs = new();

        // ── State ────────────────────────────────────────────────────────

        private HaCreatorStateManager? _hcsm;
        private HotSwapRefreshService? _hotSwapService;

        // ── Controls ────────────────────────────────────────────────────

        private readonly RadioButton _mobRButton;
        private readonly RadioButton _npcRButton;
        private readonly RadioButton _reactorRButton;
        private readonly TextBox _searchBox;
        private readonly ListBox _listBox;

        // ── Construction ─────────────────────────────────────────────────

        public LifePanel()
        {
            // Radio buttons
            _mobRButton      = new RadioButton { Content = "Mob",     GroupName = "LifeMode", IsChecked = true };
            _npcRButton      = new RadioButton { Content = "NPC",     GroupName = "LifeMode" };
            _reactorRButton  = new RadioButton { Content = "Reactor", GroupName = "LifeMode" };

            var radioPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(4)
            };
            radioPanel.Children.Add(_mobRButton);
            radioPanel.Children.Add(_npcRButton);
            radioPanel.Children.Add(_reactorRButton);

            // Search box
            _searchBox = new TextBox
            {
                Watermark = "Search…",
                Margin = new Thickness(4, 2, 4, 2)
            };

            // List box
            _listBox = new ListBox
            {
                Margin = new Thickness(4, 2, 4, 4),
                [Grid.RowProperty] = 2
            };

            // Layout: outer DockPanel → Grid (radio | search | list)
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));

            Grid.SetRow(radioPanel, 0);
            Grid.SetRow(_searchBox,  1);
            Grid.SetRow(_listBox,    2);

            grid.Children.Add(radioPanel);
            grid.Children.Add(_searchBox);
            grid.Children.Add(_listBox);

            Content = grid;

            // Wire events
            _mobRButton.IsCheckedChanged     += OnModeChanged;
            _npcRButton.IsCheckedChanged     += OnModeChanged;
            _reactorRButton.IsCheckedChanged += OnModeChanged;
            _searchBox.TextChanged           += OnSearchTextChanged;
            _listBox.SelectionChanged        += OnListBoxSelectionChanged;
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Populates the data lists from InfoManager and registers this panel
        /// with the state manager.
        /// </summary>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            _hcsm = hcsm;
            hcsm.SetLifePanel(this);

            // Reactors: "id name(name)" format
            foreach (var entry in Program.InfoManager.Reactors)
            {
                string reactorId   = entry.Value.ID;
                string reactorName = entry.Value.Name;
                string combined    = reactorName == string.Empty
                    ? reactorId
                    : $"{reactorId} ({reactorName})";
                _reactors.Add(combined);
            }

            // NPCs: "id - name (desc)" format
            foreach (var entry in Program.InfoManager.NpcNameCache)
            {
                string npcName = entry.Value.Item1;
                string npcDesc = entry.Value.Item2;
                string combined = npcDesc == string.Empty
                    ? $"{entry.Key} - {npcName}"
                    : $"{entry.Key} - {npcName} ({npcDesc})";
                _npcs.Add(combined);
            }

            // Mobs: "id - name" format
            foreach (var entry in Program.InfoManager.MobNameCache)
            {
                _mobs.Add($"{entry.Key} - {entry.Value}");
            }

            ReloadLifeList();
        }

        /// <summary>
        /// Subscribes to hot-swap life-data refresh events.
        /// </summary>
        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (_hotSwapService != null)
                _hotSwapService.LifeDataChanged -= OnLifeDataChanged;

            _hotSwapService = refreshService;

            if (_hotSwapService != null)
                _hotSwapService.LifeDataChanged += OnLifeDataChanged;
        }

        // ── Hot-swap handlers ────────────────────────────────────────────

        private void OnLifeDataChanged(object? sender, LifeDataChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => HandleLifeDataChange(e));
        }

        private void HandleLifeDataChange(LifeDataChangedEventArgs e)
        {
            switch (e.LifeType)
            {
                case LifeType.Mob:      RefreshMobList();      break;
                case LifeType.Npc:      RefreshNpcList();      break;
                case LifeType.Reactor:  RefreshReactorList();  break;
            }
        }

        public void RefreshMobList()
        {
            _mobs.Clear();
            foreach (var entry in Program.InfoManager.MobNameCache.ToList())
                _mobs.Add($"{entry.Key} - {entry.Value}");

            if (_mobRButton.IsChecked == true)
                ReloadLifeList();
        }

        public void RefreshNpcList()
        {
            _npcs.Clear();
            foreach (var entry in Program.InfoManager.NpcNameCache.ToList())
            {
                string npcName = entry.Value.Item1;
                string npcDesc = entry.Value.Item2;
                string combined = npcDesc == string.Empty
                    ? $"{entry.Key} - {npcName}"
                    : $"{entry.Key} - {npcName} ({npcDesc})";
                _npcs.Add(combined);
            }

            if (_npcRButton.IsChecked == true)
                ReloadLifeList();
        }

        public void RefreshReactorList()
        {
            _reactors.Clear();
            foreach (var entry in Program.InfoManager.Reactors.ToList())
            {
                string reactorId   = entry.Value.ID;
                string reactorName = entry.Value.Name;
                string combined    = reactorName == string.Empty
                    ? reactorId
                    : $"{reactorId} ({reactorName})";
                _reactors.Add(combined);
            }

            if (_reactorRButton.IsChecked == true)
                ReloadLifeList();
        }

        // ── Private helpers ──────────────────────────────────────────────

        private static bool ContainsIgnoreCase(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private void ReloadLifeList()
        {
            string searchText = _searchBox.Text ?? string.Empty;
            bool getAll = searchText == string.Empty;

            IEnumerable<string> source;
            if (_reactorRButton.IsChecked == true)
                source = _reactors;
            else if (_npcRButton.IsChecked == true)
                source = _npcs;
            else
                source = _mobs;

            var items = (getAll ? source : source.Where(x => ContainsIgnoreCase(x, searchText)))
                        .OrderBy(x => x)
                        .ToList();

            _listBox.SelectionChanged -= OnListBoxSelectionChanged;
            _listBox.ItemsSource = items;
            _listBox.SelectionChanged += OnListBoxSelectionChanged;
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnModeChanged(object? sender, RoutedEventArgs e)
        {
            // Only fire when a button becomes checked (not when it becomes unchecked)
            if (sender is RadioButton rb && rb.IsChecked == true)
                ReloadLifeList();
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) =>
            ReloadLifeList();

        private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_hcsm == null) return;
            if (_listBox.SelectedItem is not string item) return;

            lock (_hcsm.MultiBoard)
            {
                Board? board = _hcsm.MultiBoard.SelectedBoard;
                if (board == null) return;

                if (_reactorRButton.IsChecked == true)
                {
                    // Extract the numeric ID prefix: e.g. "1002009 (some name)"
                    string number = Regex.Match(item, @"^\d+").Value;
                    if (string.IsNullOrEmpty(number)) return;

                    if (!Program.InfoManager.Reactors.TryGetValue(number, out ReactorInfo? info) || info == null)
                        return;

                    _hcsm.EnterEditMode(ItemTypes.Reactors);
                    board.Mouse.SetHeldInfo(info);
                }
                else if (_npcRButton.IsChecked == true)
                {
                    int sepIdx = item.IndexOf(" - ", StringComparison.Ordinal);
                    if (sepIdx < 0) return;
                    string id = item.Substring(0, sepIdx);

                    NpcInfo? info = NpcInfo.Get(id);
                    if (info == null) return;

                    _hcsm.EnterEditMode(ItemTypes.NPCs);
                    board.Mouse.SetHeldInfo(info);
                }
                else // mob
                {
                    int sepIdx = item.IndexOf(" - ", StringComparison.Ordinal);
                    if (sepIdx < 0) return;
                    string id = item.Substring(0, sepIdx);

                    MobInfo? info = MobInfo.Get(id);
                    if (info == null) return;

                    _hcsm.EnterEditMode(ItemTypes.Mobs);
                    board.Mouse.SetHeldInfo(info);
                }
            }
        }
    }
}
