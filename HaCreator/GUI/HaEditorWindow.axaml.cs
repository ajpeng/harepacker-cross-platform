/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Interactivity;
using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor;
using System;

namespace HaCreator.GUI
{
    public partial class HaEditorWindow : Window
    {
        private readonly MultiBoard _multiBoard = new MultiBoard();
        private readonly HaRibbon _ribbon = new HaRibbon();
        private HaCreatorStateManager? _stateManager;
        private MapEditorControl? _mapControl;

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

        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            _stateManager = new HaCreatorStateManager(
                _multiBoard, _ribbon, tabMaps, leftPanelScroll,
                txtCursorX, txtCursorY,
                txtCursorX /* real-cursor X (stub: same) */,
                txtCursorY /* real-cursor Y (stub: same) */,
                txtSelectedItem);
            _stateManager.OwnerWindow = this;
            _multiBoard.Ribbon = _ribbon;

            // Embed the MonoGame canvas into the canvas host border
            _mapControl = new MapEditorControl(_multiBoard);
            canvasHost.Child = _mapControl;

            // Initialize left-panel editors and place them in their Expanders
            var tileP = new TilePanel();
            tileP.Initialize(_stateManager);
            expTile.Content = tileP;

            var lifeP = new LifePanel();
            lifeP.Initialize(_stateManager);
            expLife.Content = lifeP;

            var portalP = new PortalPanel();
            portalP.Initialize(_stateManager);
            expPortal.Content = portalP;

            var objP = new ObjPanel();
            objP.Initialize(_stateManager);
            expObj.Content = objP;

            var bgP = new BackgroundPanel();
            bgP.Initialize(_stateManager);
            expBg.Content = bgP;

            var bbP = new BlackBorderPanel();
            bbP.Initialize(_stateManager);
            expBb.Content = bbP;

            // Attempt backup restore after everything is wired up
            bool restored = await _stateManager.backupMan.AttemptRestoreAsync(this);
            if (!restored)
                await _stateManager.ShowFieldSelectorAsync();
        }

        // ── Tab ──────────────────────────────────────────────────────

        private void TabMaps_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (tabMaps.SelectedItem is TabItem ti && ti.Tag is TabItemContainer tic)
            {
                _multiBoard.SelectedBoard = tic.Board;
                _multiBoard.AdjustScrollBars();
            }
            else if (tabMaps.SelectedItem == null)
            {
                _multiBoard.SelectedBoard = null;
            }
        }

        // ── Menu: File ───────────────────────────────────────────────

        private void MenuNew_Click(object? sender, RoutedEventArgs e)    => _ribbon.FireNew();
        private void MenuOpen_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireOpen();
        private void MenuSave_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireSave();
        private void MenuSaveAs_Click(object? sender, RoutedEventArgs e) => _ribbon.FireSave(); // alias

        private async void MenuExit_Click(object? sender, RoutedEventArgs e)
        {
            bool confirmed = false;
            var btnYes = new Button { Content = "Yes", IsDefault = true };
            var btnNo  = new Button { Content = "No",  IsCancel  = true };
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
                            Children = { btnYes, btnNo }
                        }
                    }
                }
            };
            btnYes.Click += (_, _) => { confirmed = true; dlg.Close(); };
            btnNo.Click  += (_, _) => dlg.Close();
            await dlg.ShowDialog(this);
            if (confirmed) Close();
        }

        // ── Menu: Edit ───────────────────────────────────────────────

        private void MenuUndo_Click(object? sender, RoutedEventArgs e)
        {
            if (_multiBoard.SelectedBoard != null)
                lock (_multiBoard) { _multiBoard.SelectedBoard.UndoRedoMan.Undo(); }
        }
        private void MenuRedo_Click(object? sender, RoutedEventArgs e)
        {
            if (_multiBoard.SelectedBoard != null)
                lock (_multiBoard) { _multiBoard.SelectedBoard.UndoRedoMan.Redo(); }
        }

        // ── Menu: View ───────────────────────────────────────────────

        private void MenuMinimap_Click(object? sender, RoutedEventArgs e)
        {
            menuMinimap.IsChecked = !menuMinimap.IsChecked;
            _ribbon.FireShowMinimap(menuMinimap.IsChecked);
        }

        private void MenuParallax_Click(object? sender, RoutedEventArgs e)
        {
            menuParallax.IsChecked = !menuParallax.IsChecked;
            _ribbon.FireParallax(menuParallax.IsChecked);
        }

        private void MenuAllLayers_Click(object? sender, RoutedEventArgs e)
        {
            menuAllLayers.IsChecked = !menuAllLayers.IsChecked;
            ApplicationSettings.lastAllLayers = menuAllLayers.IsChecked;
        }

        private void MenuViewType_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            mi.IsChecked = !mi.IsChecked;
            // true  = visible+editable; false = hidden; null = unchanged for that type
            bool? V(MenuItem m) => m.IsChecked ? true : false;
            _ribbon.FireViewToggled(
                mi == menuVTiles       ? V(menuVTiles)       : (bool?)null,
                mi == menuVObjs        ? V(menuVObjs)        : (bool?)null,
                mi == menuVNpcs        ? V(menuVNpcs)        : (bool?)null,
                mi == menuVMobs        ? V(menuVMobs)        : (bool?)null,
                mi == menuVReactors    ? V(menuVReactors)    : (bool?)null,
                mi == menuVPortals     ? V(menuVPortals)     : (bool?)null,
                mi == menuVFootholds   ? V(menuVFootholds)   : (bool?)null,
                mi == menuVRopes       ? V(menuVRopes)       : (bool?)null,
                mi == menuVChairs      ? V(menuVChairs)      : (bool?)null,
                mi == menuVTooltips    ? V(menuVTooltips)    : (bool?)null,
                mi == menuVBackgrounds ? V(menuVBackgrounds) : (bool?)null,
                mi == menuVMisc        ? V(menuVMisc)        : (bool?)null,
                mi == menuVMirrorField ? V(menuVMirrorField) : (bool?)null);
        }

        // ── Menu: Map ────────────────────────────────────────────────

        private void MenuMapInfo_Click(object? sender, RoutedEventArgs e)
        {
            if (tabMaps.SelectedItem is TabItem ti)
                _ribbon.FireMapInfo(ti);
        }
        private void MenuRegenMinimap_Click(object? sender, RoutedEventArgs e) => _ribbon.FireRegenerateMinimap();
        private void MenuMapPhysics_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireMapPhysics();

        private void MenuMapProperties_Click(object? sender, RoutedEventArgs e) => _ribbon.FireShowMapProperties();

        // ── Menu: Tools ──────────────────────────────────────────────

        private void MenuSettings_Click(object? sender, RoutedEventArgs e)   => _ribbon.FireSettings();
        private void MenuQuestEditor_Click(object? sender, RoutedEventArgs e) => _ribbon.FireShowQuestEditor();
        private void MenuMapSim_Click(object? sender, RoutedEventArgs e)      => _ribbon.FireMapSimulation();
        private void MenuUserObjs_Click(object? sender, RoutedEventArgs e)    => _ribbon.FireUserObjs();
        private void MenuExport_Click(object? sender, RoutedEventArgs e)      => _ribbon.FireExport();
        private void MenuFinalize_Click(object? sender, RoutedEventArgs e)    => _ribbon.FireFinalize();
        private void MenuNewPlatform_Click(object? sender, RoutedEventArgs e) => _ribbon.FireNewPlatform();

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
