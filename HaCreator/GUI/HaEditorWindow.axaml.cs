/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Interactivity;
using HaCreator.MapEditor;
using System;

namespace HaCreator.GUI
{
    public partial class HaEditorWindow : Window
    {
        private readonly MultiBoard _multiBoard = new MultiBoard();
        private readonly HaRibbon _ribbon = new HaRibbon();
        private HaCreatorStateManager? _stateManager;

        public HaEditorWindow()
        {
            InitializeComponent();
            Program.HaEditorWindow = this;

            Closed += (_, _) => Program.AbortThreads = true;

            menuMinimap.IsChecked   = UserSettings.useMiniMap;
            menuParallax.IsChecked  = UserSettings.emulateParallax;
            menuAllLayers.IsChecked = ApplicationSettings.lastAllLayers;

            Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            _stateManager = new HaCreatorStateManager(
                _multiBoard, _ribbon, tabMaps, leftPanelScroll,
                txtCursorX, txtCursorY,
                txtCursorX /* real-cursor X (stub: same) */,
                txtCursorY /* real-cursor Y (stub: same) */,
                txtSelectedItem);
            _stateManager.OwnerWindow = this;
            _multiBoard.Ribbon = _ribbon;
        }

        // ── Tab ──────────────────────────────────────────────────────

        private void TabMaps_SelectionChanged(object? sender, SelectionChangedEventArgs e) { }

        // ── Menu: File ───────────────────────────────────────────────

        private void MenuNew_Click(object? sender, RoutedEventArgs e)    => _ribbon.FireNew();
        private void MenuOpen_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireOpen();
        private void MenuSave_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireSave();
        private void MenuSaveAs_Click(object? sender, RoutedEventArgs e) => _ribbon.FireSave(); // alias

        private async void MenuExit_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new Window
            {
                Title = "Quit",
                Width = 300, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16), Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "Are you sure you want to quit?" },
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children =
                            {
                                new Button { Content = "Yes", Tag = true  },
                                new Button { Content = "No",  Tag = false }
                            }
                        }
                    }
                }
            };
            // Simple close — wire Yes button properly when HaCreatorStateManager is ported
            Close();
        }

        // ── Menu: Edit ───────────────────────────────────────────────

        private void MenuUndo_Click(object? sender, RoutedEventArgs e) { }
        private void MenuRedo_Click(object? sender, RoutedEventArgs e) { }

        // ── Menu: View ───────────────────────────────────────────────

        private void MenuMinimap_Click(object? sender, RoutedEventArgs e)
        {
            menuMinimap.IsChecked = !menuMinimap.IsChecked;
            UserSettings.useMiniMap = menuMinimap.IsChecked;
        }

        private void MenuParallax_Click(object? sender, RoutedEventArgs e)
        {
            menuParallax.IsChecked = !menuParallax.IsChecked;
            UserSettings.emulateParallax = menuParallax.IsChecked;
        }

        private void MenuAllLayers_Click(object? sender, RoutedEventArgs e)
        {
            menuAllLayers.IsChecked = !menuAllLayers.IsChecked;
            ApplicationSettings.lastAllLayers = menuAllLayers.IsChecked;
        }

        // ── Menu: Map ────────────────────────────────────────────────

        private void MenuMapInfo_Click(object? sender, RoutedEventArgs e) { }
        private void MenuRegenMinimap_Click(object? sender, RoutedEventArgs e) { }
        private void MenuMapPhysics_Click(object? sender, RoutedEventArgs e) { }

        // ── Menu: Tools ──────────────────────────────────────────────

        private void MenuSettings_Click(object? sender, RoutedEventArgs e) => _ribbon.FireSettings();

        // ── Menu: Help ───────────────────────────────────────────────

        private async void MenuAbout_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new Window
            {
                Title = "About HaCreator",
                Width = 360, Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16), Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "HaCreator — MapleStory Map Editor", FontWeight = Avalonia.Media.FontWeight.Bold },
                        new TextBlock { Text = "Cross-platform port by ajpeng", FontSize = 12 },
                        new TextBlock { Text = "github.com/ajpeng/harepacker-cross-platform", FontSize = 11, Foreground = Avalonia.Media.Brushes.Blue },
                        new Button   { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                    }
                }
            };
            await dlg.ShowDialog(this);
        }

        // ── Public helpers called by future HaCreatorStateManager ────

        public void UpdateCursorPosition(int x, int y)
        {
            txtCursorX.Text = x.ToString();
            txtCursorY.Text = y.ToString();
        }

        public void UpdateZoom(float zoom)
        {
            txtZoom.Text = $"{(int)(zoom * 100)}%";
        }

        public void UpdateSelectedItem(string desc)
        {
            txtSelectedItem.Text = desc;
        }
    }
}
