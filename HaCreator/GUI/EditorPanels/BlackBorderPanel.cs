/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using HaCreator.MapEditor;
using System;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Avalonia code-only UserControl for configuring per-board black border
    /// (LBTop / LBBottom / LBSide) values.
    /// Replaces the WinForms BlackBorderPanel.
    /// </summary>
    public class BlackBorderPanel : UserControl
    {
        // ── State ────────────────────────────────────────────────────────

        private HaCreatorStateManager? _hcsm;

        // ── Controls ─────────────────────────────────────────────────────

        private readonly CheckBox        _checkTop;
        private readonly NumericUpDown   _nudTop;
        private readonly CheckBox        _checkBottom;
        private readonly NumericUpDown   _nudBottom;
        private readonly CheckBox        _checkSide;
        private readonly NumericUpDown   _nudSide;

        // Suppress re-entrant updates while we push board data into the controls
        private bool _updatingControls;

        // ── Construction ──────────────────────────────────────────────────

        public BlackBorderPanel()
        {
            // ── Top row ──
            _checkTop = new CheckBox { Content = "Top",    Margin = new Thickness(4, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            _nudTop   = new NumericUpDown
            {
                Minimum         = 0,
                Maximum         = 9999,
                Value           = 0,
                IsEnabled       = false,
                Width           = 80,
                Margin          = new Thickness(0, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // ── Bottom row ──
            _checkBottom = new CheckBox { Content = "Bottom", Margin = new Thickness(4, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            _nudBottom   = new NumericUpDown
            {
                Minimum         = 0,
                Maximum         = 9999,
                Value           = 0,
                IsEnabled       = false,
                Width           = 80,
                Margin          = new Thickness(0, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // ── Side row ──
            _checkSide = new CheckBox { Content = "Side", Margin = new Thickness(4, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            _nudSide   = new NumericUpDown
            {
                Minimum         = 0,
                Maximum         = 9999,
                Value           = 0,
                IsEnabled       = false,
                Width           = 80,
                Margin          = new Thickness(0, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // ── Layout ──

            // 3 rows × 2 cols: [CheckBox | NumericUpDown]
            var grid = new Grid { Margin = new Thickness(4) };
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            // Top
            Grid.SetRow(_checkTop, 0); Grid.SetColumn(_checkTop, 0);
            Grid.SetRow(_nudTop,   0); Grid.SetColumn(_nudTop,   1);
            // Bottom
            Grid.SetRow(_checkBottom, 1); Grid.SetColumn(_checkBottom, 0);
            Grid.SetRow(_nudBottom,   1); Grid.SetColumn(_nudBottom,   1);
            // Side
            Grid.SetRow(_checkSide, 2); Grid.SetColumn(_checkSide, 0);
            Grid.SetRow(_nudSide,   2); Grid.SetColumn(_nudSide,   1);

            grid.Children.Add(_checkTop);    grid.Children.Add(_nudTop);
            grid.Children.Add(_checkBottom); grid.Children.Add(_nudBottom);
            grid.Children.Add(_checkSide);   grid.Children.Add(_nudSide);

            Content = grid;

            // ── Wire events ──
            _checkTop.IsCheckedChanged    += OnTopCheckedChanged;
            _checkBottom.IsCheckedChanged += OnBottomCheckedChanged;
            _checkSide.IsCheckedChanged   += OnSideCheckedChanged;

            _nudTop.ValueChanged    += OnTopValueChanged;
            _nudBottom.ValueChanged += OnBottomValueChanged;
            _nudSide.ValueChanged   += OnSideValueChanged;
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Stores the state manager reference and registers this panel with it.
        /// </summary>
        public void Initialize(HaCreatorStateManager hcsm)
        {
            _hcsm = hcsm;
            hcsm.SetBlackBorderPanel(this);
        }

        /// <summary>
        /// Reads LBTop / LBBottom / LBSide from the currently selected board and
        /// updates the controls accordingly. Called whenever the active board changes.
        /// </summary>
        public void UpdateBoardData()
        {
            if (_hcsm == null) return;
            var board = _hcsm.MultiBoard.SelectedBoard;
            if (board == null) return;

            _updatingControls = true;
            try
            {
                // Top
                int? top = board.MapInfo.LBTop;
                _checkTop.IsChecked = top.HasValue && top.Value != 0;
                _nudTop.Value       = top.HasValue ? top.Value : 0;
                _nudTop.IsEnabled   = _checkTop.IsChecked == true;

                // Bottom
                int? bottom = board.MapInfo.LBBottom;
                _checkBottom.IsChecked = bottom.HasValue && bottom.Value != 0;
                _nudBottom.Value       = bottom.HasValue ? bottom.Value : 0;
                _nudBottom.IsEnabled   = _checkBottom.IsChecked == true;

                // Side
                int? side = board.MapInfo.LBSide;
                _checkSide.IsChecked = side.HasValue && side.Value != 0;
                _nudSide.Value       = side.HasValue ? side.Value : 0;
                _nudSide.IsEnabled   = _checkSide.IsChecked == true;
            }
            finally
            {
                _updatingControls = false;
            }
        }

        // ── Checkbox handlers ─────────────────────────────────────────────

        private void OnTopCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_updatingControls) return;
            bool isChecked = _checkTop.IsChecked == true;
            _nudTop.IsEnabled = isChecked;

            if (!isChecked)
            {
                _nudTop.Value = 0;
                var board = _hcsm?.MultiBoard.SelectedBoard;
                if (board != null) board.MapInfo.LBTop = null;
            }
        }

        private void OnBottomCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_updatingControls) return;
            bool isChecked = _checkBottom.IsChecked == true;
            _nudBottom.IsEnabled = isChecked;

            if (!isChecked)
            {
                _nudBottom.Value = 0;
                var board = _hcsm?.MultiBoard.SelectedBoard;
                if (board != null) board.MapInfo.LBBottom = null;
            }
        }

        private void OnSideCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_updatingControls) return;
            bool isChecked = _checkSide.IsChecked == true;
            _nudSide.IsEnabled = isChecked;

            if (!isChecked)
            {
                _nudSide.Value = 0;
                var board = _hcsm?.MultiBoard.SelectedBoard;
                if (board != null) board.MapInfo.LBSide = null;
            }
        }

        // ── NumericUpDown value-change handlers ───────────────────────────

        private void OnTopValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_updatingControls) return;
            if (_checkTop.IsChecked != true) return;
            var board = _hcsm?.MultiBoard.SelectedBoard;
            if (board != null)
                board.MapInfo.LBTop = _nudTop.Value.HasValue ? (int?)((int)_nudTop.Value.Value) : null;
        }

        private void OnBottomValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_updatingControls) return;
            if (_checkBottom.IsChecked != true) return;
            var board = _hcsm?.MultiBoard.SelectedBoard;
            if (board != null)
                board.MapInfo.LBBottom = _nudBottom.Value.HasValue ? (int?)((int)_nudBottom.Value.Value) : null;
        }

        private void OnSideValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_updatingControls) return;
            if (_checkSide.IsChecked != true) return;
            var board = _hcsm?.MultiBoard.SelectedBoard;
            if (board != null)
                board.MapInfo.LBSide = _nudSide.Value.HasValue ? (int?)((int)_nudSide.Value.Value) : null;
        }
    }
}
