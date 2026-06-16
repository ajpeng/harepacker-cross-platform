/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Threading;
using HaCreator.Exceptions;
using HaCreator.GUI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;
using MapleLib;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor
{
    public class HaCreatorStateManager
    {
        private readonly MultiBoard _multiBoard;
        private readonly HaRibbon _ribbon;
        private readonly TabControl _tabs;
        private readonly ScrollViewer _editorPanel;

        private readonly TextBlock _tbCursorX;
        private readonly TextBlock _tbCursorY;
        private readonly TextBlock _tbRCursorX;
        private readonly TextBlock _tbRCursorY;
        private readonly TextBlock _tbSelectedItem;

        private TilePanel tilePanel;
        private ObjPanel objPanel;
        private BackgroundPanel backgroundPanel;
        private LifePanel lifePanel;
        private BlackBorderPanel blackBorderPanel;
        private ObjectViewerPanel objectViewerPanel;

        public readonly BackupManager backupMan;

        // Set by the host window so dialogs can use it as owner
        public Window? OwnerWindow { get; set; }

        private HotSwapRefreshService _hotSwapService;
        private AssetUsageTracker _assetUsageTracker;

        public HaCreatorStateManager(
            MultiBoard multiBoard,
            HaRibbon ribbon,
            TabControl tabs,
            ScrollViewer editorPanel,
            TextBlock tbCursorX, TextBlock tbCursorY,
            TextBlock tbRCursorX, TextBlock tbRCursorY,
            TextBlock tbSelectedItem)
        {
            _multiBoard = multiBoard;
            _multiBoard.HaCreatorStateManager = this;

            _ribbon = ribbon;
            _tabs = tabs;
            _editorPanel = editorPanel;

            _tbCursorX = tbCursorX;
            _tbCursorY = tbCursorY;
            _tbRCursorX = tbRCursorX;
            _tbRCursorY = tbRCursorY;
            _tbSelectedItem = tbSelectedItem;

            backupMan = new BackupManager(multiBoard, this, tabs);

            _ribbon.NewClicked              += Ribbon_NewClicked;
            _ribbon.OpenClicked             += Ribbon_OpenClicked;
            _ribbon.SaveClicked             += Ribbon_SaveClicked;
            _ribbon.RepackClicked           += Ribbon_RepackClicked;
            _ribbon.AboutClicked            += Ribbon_AboutClicked;
            _ribbon.HelpClicked             += Ribbon_HelpClicked;
            _ribbon.SettingsClicked         += Ribbon_SettingsClicked;
            _ribbon.ExitClicked             += Ribbon_ExitClicked;
            _ribbon.ViewToggled             += Ribbon_ViewToggled;
            _ribbon.ShowMinimapToggled      += Ribbon_ShowMinimapToggled;
            _ribbon.ParallaxToggled         += Ribbon_ParallaxToggled;
            _ribbon.LayerViewChanged        += Ribbon_LayerViewChanged;
            _ribbon.MapSimulationClicked    += Ribbon_MapSimulationClicked;
            _ribbon.RegenerateMinimapClicked+= Ribbon_RegenerateMinimapClicked;
            _ribbon.SnappingToggled         += Ribbon_SnappingToggled;
            _ribbon.RandomTilesToggled      += Ribbon_RandomTilesToggled;
            _ribbon.InfoModeToggled         += Ribbon_InfoModeToggled;
            _ribbon.HaRepackerClicked       += Ribbon_HaRepackerClicked;
            _ribbon.FinalizeClicked         += Ribbon_FinalizeClicked;
            _ribbon.NewPlatformClicked      += Ribbon_NewPlatformClicked;
            _ribbon.UserObjsClicked         += Ribbon_UserObjsClicked;
            _ribbon.ExportClicked           += Ribbon_ExportClicked;
            _ribbon.MapPhysicsClicked       += Ribbon_MapPhysicsClicked;
            _ribbon.ShowQuestEditorWindowClicked += Ribbon_ShowQuestEditorWindowClicked;
            _ribbon.ShowMapPropertiesClicked += Ribbon_ShowMapPropertiesClicked;
            _ribbon.RibbonKeyDown           += (s, e) => { }; // key input forwarding — pending InputHandler port

            _tabs.SelectionChanged += Tabs_SelectionChanged;

            _multiBoard.OnBringToFrontClicked  += MultiBoard_OnBringToFrontClicked;
            _multiBoard.OnEditBaseClicked      += MultiBoard_OnEditBaseClicked;
            _multiBoard.OnEditInstanceClicked  += MultiBoard_OnEditInstanceClicked;
            _multiBoard.OnLayerTSChanged2      += MultiBoard_OnLayerTSChanged;
            _multiBoard.OnSendToBackClicked    += MultiBoard_OnSendToBackClicked;
            _multiBoard.ReturnToSelectionState += MultiBoard_ReturnToSelectionState;
            _multiBoard.SelectedItemChanged    += MultiBoard_SelectedItemChanged;
            _multiBoard.MouseMoved             += MultiBoard_MouseMoved;
            _multiBoard.ImageDropped           += MultiBoard_ImageDropped;
            _multiBoard.ExportRequested        += Ribbon_ExportClicked;
            _multiBoard.LoadRequested          += Ribbon_OpenClicked;
            _multiBoard.CloseTabRequested      += MultiBoard_CloseTabRequested;
            _multiBoard.SwitchTabRequested     += MultiBoard_SwitchTabRequested;
            _multiBoard.BackupCheck            += MultiBoard_BackupCheck;
            _multiBoard.BoardRemoved           += MultiBoard_BoardRemoved;
            _multiBoard.MinimapStateChanged    += MultiBoard_MinimapStateChanged;

            _multiBoard.IsVisible = false;
            _ribbon.SetEnabled(false);
        }

        public static int PositiveMod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        // ── MultiBoard event handlers ─────────────────────────────────

        void MultiBoard_SwitchTabRequested(object sender, bool reverse)
        {
            int idx = _tabs.Items.IndexOf(_tabs.SelectedItem);
            int next = PositiveMod(idx + (reverse ? -1 : 1), _tabs.Items.Count);
            _tabs.SelectedItem = _tabs.Items[next];
        }

        void MultiBoard_CloseTabRequested() => _tabs.Items.Remove(_tabs.SelectedItem);

        void MultiBoard_MinimapStateChanged(object sender, bool hasMm) => _ribbon.SetHasMinimap(hasMm);

        void MultiBoard_BoardRemoved(object sender, EventArgs e)
        {
            if (sender is Board board)
                backupMan.DeleteBackup(board.UniqueID);
        }

        void MultiBoard_BackupCheck()
        {
            try { backupMan.BackupCheck(); }
            catch (Exception e) { Debug.WriteLine($"Backup failed: {e.Message}"); }
        }

        void MultiBoard_ImageDropped(Board selectedBoard, SkiaSharp.SKBitmap bmp, string name,
            Microsoft.Xna.Framework.Point pos)
        {
            // TODO: show WaitWindow (Avalonia progress dialog) then add user object
            if (_multiBoard.UserObjects == null) return;
            try
            {
                ObjectInfo oi = _multiBoard.UserObjects.Add(bmp, name);
                selectedBoard.BoardItems.Add(
                    oi.CreateInstance(selectedBoard.SelectedLayer, selectedBoard, pos.X, pos.Y, 0, false), true);
                objPanel?.OnL1Changed(UserObjectsManager.l1);
            }
            catch (NameAlreadyUsedException)
            {
                Debug.WriteLine($"User object '{name}' already exists.");
            }
        }

        void MultiBoard_MouseMoved(Board selectedBoard,
            Microsoft.Xna.Framework.Point oldPos,
            Microsoft.Xna.Framework.Point newPos,
            Microsoft.Xna.Framework.Point currPhysicalPos)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _tbCursorX.Text = currPhysicalPos.X.ToString();
                _tbCursorY.Text = currPhysicalPos.Y.ToString();
                _tbRCursorX.Text = newPos.X.ToString();
                _tbRCursorY.Text = newPos.Y.ToString();
            });
        }

        void MultiBoard_SelectedItemChanged(BoardItem selectedItem)
        {
            Dispatcher.UIThread.Post(() =>
                _tbSelectedItem.Text = selectedItem != null
                    ? CreateItemDescription(selectedItem).Replace(Environment.NewLine, " - ")
                    : string.Empty);
        }

        void MultiBoard_ReturnToSelectionState()
        {
            if (_multiBoard.SelectedBoard == null) return;
            _multiBoard.SelectedBoard.Mouse.SelectionMode();
            ExitEditMode();
            _multiBoard.Focus();
        }

        void MultiBoard_OnSendToBackClicked(BoardItem boardRefItem)
        {
            lock (_multiBoard)
            {
                foreach (BoardItem item in boardRefItem.Board.SelectedItems)
                {
                    if (item.Z > 0)
                    {
                        item.Board.UndoRedoMan.AddUndoBatch(
                            new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, item.Z, 0) });
                        item.Z = 0;
                    }
                }
                boardRefItem.Board.BoardItems.Sort();
            }
            _multiBoard.Focus();
        }

        void MultiBoard_OnLayerTSChanged(Layer layer) => _ribbon.SetLayer(layer);

        void MultiBoard_OnEditInstanceClicked(BoardItem item)
        {
            MultiBoard.ClearBoundItems(_multiBoard.SelectedBoard);
            try
            {
                // TODO: port instance editor dialogs to Avalonia
                // For now, log and no-op
                Debug.WriteLine($"Edit instance: {item.GetType().Name}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error presenting instance editor for {item.GetType().Name}: {e}");
            }
        }

        void MultiBoard_OnEditBaseClicked(BoardItem item) { /* TODO */ }

        void MultiBoard_OnBringToFrontClicked(BoardItem boardRefItem)
        {
            lock (_multiBoard)
            {
                foreach (BoardItem item in boardRefItem.Board.SelectedItems)
                {
                    int oldZ = item.Z;
                    if (item is BackgroundInstance bg)
                    {
                        IList list = bg.front
                            ? _multiBoard.SelectedBoard.BoardItems.FrontBackgrounds
                            : _multiBoard.SelectedBoard.BoardItems.BackBackgrounds;
                        int highestZ = 0;
                        foreach (BackgroundInstance b in list)
                            if (b.Z > highestZ) highestZ = b.Z;
                        item.Z = highestZ + 1;
                    }
                    else
                    {
                        int highestZ = 0;
                        foreach (LayeredItem li in _multiBoard.SelectedBoard.BoardItems.TileObjs)
                            if (li.Z > highestZ) highestZ = li.Z;
                        item.Z = highestZ + 1;
                    }
                    if (item.Z != oldZ)
                        item.Board.UndoRedoMan.AddUndoBatch(
                            new List<UndoRedoAction> { UndoRedoManager.ItemZChanged(item, oldZ, item.Z) });
                }
            }
            boardRefItem.Board.BoardItems.Sort();
        }

        // ── Tab events ────────────────────────────────────────────────

        private void MapEditInfo(TabItem tabItem)
        {
            if (tabItem?.Tag is not TabItemContainer container) return;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                // TODO: port InfoEditor to Avalonia
                Debug.WriteLine("MapEditInfo stub");
                selectedBoard.ParentControl.AdjustScrollBars();
            }
        }

        private void MapAddVR(TabItem tabItem)
        {
            if (tabItem?.Tag is not TabItemContainer container) return;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                selectedBoard.VRRectangle = new VRRectangle(selectedBoard,
                    new Microsoft.Xna.Framework.Rectangle(
                        -selectedBoard.CenterPoint.X + 100, -selectedBoard.CenterPoint.Y + 100,
                        selectedBoard.MapSize.X - 200, selectedBoard.MapSize.Y - 200));
            }
        }

        private void MapAddMinimap(TabItem tabItem)
        {
            if (tabItem?.Tag is not TabItemContainer container) return;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                selectedBoard.MinimapRectangle = new MinimapRectangle(selectedBoard,
                    new Microsoft.Xna.Framework.Rectangle(
                        -selectedBoard.CenterPoint.X + 100, -selectedBoard.CenterPoint.Y + 100,
                        selectedBoard.MapSize.X - 200, selectedBoard.MapSize.Y - 200));
                selectedBoard.RegenerateMinimap();
            }
        }

        private void CloseMapTab(TabItem tabItem)
        {
            if (_tabs.Items.Count <= 0) return;
            if (tabItem?.Tag is not TabItemContainer container) return;
            Board selectedBoard = container.Board;
            lock (selectedBoard.ParentControl)
            {
                _tabs.SelectedItem = _tabs.Items[0];
                _tabs.Items.Remove(tabItem);
                selectedBoard.Dispose();
            }
            UpdateEditorPanelVisibility();
        }

        public void UpdateEditorPanelVisibility()
        {
            _editorPanel.IsEnabled = _tabs.Items.Count > 0;
            blackBorderPanel?.UpdateBoardData();
        }

        private void Tabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_multiBoard.SelectedBoard == null) return;
            lock (_multiBoard)
            {
                MultiBoard_ReturnToSelectionState();
                if (_tabs.SelectedItem != null)
                {
                    TabItem selectedTab = (TabItem)_tabs.SelectedItem;
                    if (selectedTab.Tag is TabItemContainer container)
                    {
                        _multiBoard.SelectedBoard = container.Board;
                        ApplicationSettings.lastDefaultLayer = _multiBoard.SelectedBoard.SelectedLayerIndex;
                        _ribbon.SetLayers(_multiBoard.SelectedBoard.Layers);
                        _ribbon.SetSelectedLayer(
                            _multiBoard.SelectedBoard.SelectedLayerIndex,
                            _multiBoard.SelectedBoard.SelectedPlatform,
                            _multiBoard.SelectedBoard.SelectedAllLayers,
                            _multiBoard.SelectedBoard.SelectedAllPlatforms);
                        _ribbon.SetHasMinimap(_multiBoard.SelectedBoard.MinimapRectangle != null);
                        blackBorderPanel?.UpdateBoardData();
                        objectViewerPanel?.OnBoardChanged(_multiBoard.SelectedBoard);
                        ParseVisibleEditedTypes();
                    }
                }
                else
                {
                    _multiBoard.SelectedBoard = null;
                }
                _multiBoard.Focus();
            }
        }

        // ── Ribbon event handlers ─────────────────────────────────────

        private void Ribbon_ShowQuestEditorWindowClicked()
        {
            // TODO: port QuestEditor to Avalonia
            Debug.WriteLine("QuestEditor stub");
        }

        private void Ribbon_ShowMapPropertiesClicked()
        {
            if (_multiBoard.SelectedBoard == null) return;
            var unsupported = _multiBoard.SelectedBoard.MapInfo.unsupportedInfoProperties;
            var sb = new StringBuilder();
            int i = 1;
            foreach (var p in unsupported)
            {
                sb.Append(i++).Append(": ").Append(p.Name);
                sb.Append(", val: ").Append(p.WzValue?.ToString() ?? "").AppendLine();
            }
            Debug.WriteLine("Unsupported map properties:\n" + sb);
        }

        private string? _lastSaveLoc;

        public void Ribbon_ExportClicked()
        {
            // TODO: Avalonia SaveFileDialog
            Debug.WriteLine("Export stub");
        }

        private void Ribbon_UserObjsClicked()
        {
            lock (_multiBoard)
            {
                // TODO: port ManageUserObjects to Avalonia
                Debug.WriteLine("ManageUserObjects stub");
                objPanel?.OnL1Changed(UserObjectsManager.l1);
            }
        }

        private void Ribbon_FinalizeClicked()
        {
            // TODO: confirmation dialog
            lock (_multiBoard)
            {
                new MapSaver(_multiBoard.SelectedBoard).ActualizeFootholds();
            }
        }

        private void Ribbon_HaRepackerClicked()
        {
            // TODO: launch HaRepacker in cross-platform mode
            Debug.WriteLine("HaRepacker stub");
        }

        private bool? GetTypes(ItemTypes visibleTypes, ItemTypes editedTypes, ItemTypes type)
        {
            if ((editedTypes & type) == type) return true;
            if ((visibleTypes & type) == type) return null;
            return false;
        }

        private void ParseVisibleEditedTypes()
        {
            ItemTypes visible = ApplicationSettings.theoreticalVisibleTypes = _multiBoard.SelectedBoard.VisibleTypes;
            ItemTypes edited  = ApplicationSettings.theoreticalEditedTypes  = _multiBoard.SelectedBoard.EditedTypes;
            _ribbon.SetVisibilityCheckboxes(
                GetTypes(visible, edited, ItemTypes.Tiles),
                GetTypes(visible, edited, ItemTypes.Objects),
                GetTypes(visible, edited, ItemTypes.NPCs),
                GetTypes(visible, edited, ItemTypes.Mobs),
                GetTypes(visible, edited, ItemTypes.Reactors),
                GetTypes(visible, edited, ItemTypes.Portals),
                GetTypes(visible, edited, ItemTypes.Footholds),
                GetTypes(visible, edited, ItemTypes.Ropes),
                GetTypes(visible, edited, ItemTypes.Chairs),
                GetTypes(visible, edited, ItemTypes.ToolTips),
                GetTypes(visible, edited, ItemTypes.Backgrounds),
                GetTypes(visible, edited, ItemTypes.Misc),
                GetTypes(visible, edited, ItemTypes.MirrorFieldData));
        }

        private void Ribbon_RandomTilesToggled(bool pressed)
        {
            ApplicationSettings.randomTiles = pressed;
            tilePanel?.LoadTileSetList();
        }

        private void Ribbon_SnappingToggled(bool pressed) => UserSettings.useSnapping = pressed;

        private void Ribbon_InfoModeToggled(bool pressed) => ApplicationSettings.InfoMode = pressed;

        private void Ribbon_RegenerateMinimapClicked()
        {
            if (_multiBoard.SelectedBoard.RegenerateMinimap())
                Debug.WriteLine("Minimap regenerated.");
            else
                ErrorLogger.Log(ErrorLevel.Critical,
                    "Error regenerating minimap for map " + _multiBoard.SelectedBoard.MapInfo.id);
        }

        private void Ribbon_MapSimulationClicked()
        {
            // TODO: port MapSimulator to Avalonia + MonoGame.DesktopGL
            Debug.WriteLine("MapSimulator stub");
        }

        private void Ribbon_ParallaxToggled(bool pressed) => UserSettings.emulateParallax = pressed;

        private void Ribbon_ShowMinimapToggled(bool pressed) => UserSettings.useMiniMap = pressed;

        private void SetTypes(ref ItemTypes newVisible, ref ItemTypes newEdited, bool? x, ItemTypes type)
        {
            if (x.HasValue)
            {
                if (x.Value) { newVisible ^= type; newEdited ^= type; }
            }
            else
            {
                newVisible ^= type;
            }
        }

        private void Ribbon_ViewToggled(bool? tiles, bool? objs, bool? npcs, bool? mobs,
            bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs,
            bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField)
        {
            lock (_multiBoard)
            {
                ItemTypes newVisible = 0, newEdited = 0;
                SetTypes(ref newVisible, ref newEdited, tiles, ItemTypes.Tiles);
                SetTypes(ref newVisible, ref newEdited, objs, ItemTypes.Objects);
                SetTypes(ref newVisible, ref newEdited, npcs, ItemTypes.NPCs);
                SetTypes(ref newVisible, ref newEdited, mobs, ItemTypes.Mobs);
                SetTypes(ref newVisible, ref newEdited, reactors, ItemTypes.Reactors);
                SetTypes(ref newVisible, ref newEdited, portals, ItemTypes.Portals);
                SetTypes(ref newVisible, ref newEdited, footholds, ItemTypes.Footholds);
                SetTypes(ref newVisible, ref newEdited, ropes, ItemTypes.Ropes);
                SetTypes(ref newVisible, ref newEdited, chairs, ItemTypes.Chairs);
                SetTypes(ref newVisible, ref newEdited, tooltips, ItemTypes.ToolTips);
                SetTypes(ref newVisible, ref newEdited, backgrounds, ItemTypes.Backgrounds);
                SetTypes(ref newVisible, ref newEdited, misc, ItemTypes.Misc);
                SetTypes(ref newVisible, ref newEdited, mirrorField, ItemTypes.MirrorFieldData);

                ApplicationSettings.theoreticalVisibleTypes = newVisible;
                ApplicationSettings.theoreticalEditedTypes  = newEdited;
                if (_multiBoard.SelectedBoard != null)
                {
                    MultiBoard.ClearSelectedItems(_multiBoard.SelectedBoard);
                    _multiBoard.SelectedBoard.VisibleTypes = newVisible;
                    _multiBoard.SelectedBoard.EditedTypes  = newEdited;
                }
            }
        }

        private void Ribbon_ExitClicked() => CloseRequested?.Invoke();

        private async void Ribbon_SettingsClicked()
        {
            if (OwnerWindow == null)
            {
                Debug.WriteLine("[HaCreatorStateManager] OwnerWindow not set; cannot open Settings dialog");
                return;
            }
            var dlg = new HaCreator.GUI.UserSettingsDialog();
            await dlg.ShowDialog(OwnerWindow);
        }

        private void Ribbon_HelpClicked()
        {
            string helpPath = Path.Combine(AppContext.BaseDirectory, "Help.htm");
            if (File.Exists(helpPath))
                Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
            else
                Debug.WriteLine("Help file not found.");
        }

        private void Ribbon_AboutClicked()
        {
            // TODO: port About dialog to Avalonia
            Debug.WriteLine("About stub");
        }

        private void Ribbon_RepackClicked()
        {
            // TODO: port Repack/PackToWz to Avalonia
            Debug.WriteLine("Repack stub");
        }

        private async void Ribbon_SaveClicked()
        {
            Board? board;
            lock (_multiBoard) { board = _multiBoard.SelectedBoard; }
            if (board == null) return;
            if (OwnerWindow == null)
            {
                Debug.WriteLine("[HaCreatorStateManager] OwnerWindow not set; cannot open Save dialog");
                return;
            }
            var dlg = new HaCreator.GUI.SaveMapDialog(board);
            await dlg.ShowDialog(OwnerWindow);
        }

        public EventHandler[] MakeRightClickHandler()
        {
            return new EventHandler[]
            {
                (s, e) => { if (s is MenuItem mi && mi.Tag is TabItem tab) MapEditInfo(tab); },
                (s, e) => { if (s is MenuItem mi && mi.Tag is TabItem tab) MapAddVR(tab); },
                (s, e) => { if (s is MenuItem mi && mi.Tag is TabItem tab) MapAddMinimap(tab); },
                (s, e) => { if (s is MenuItem mi && mi.Tag is TabItem tab) CloseMapTab(tab); }
            };
        }

        private async void Ribbon_NewClicked()
        {
            if (OwnerWindow == null)
            {
                Debug.WriteLine("[HaCreatorStateManager] OwnerWindow not set; cannot open New dialog");
                return;
            }
            var dlg = new HaCreator.GUI.NewMapDialog(_multiBoard, _tabs, MakeRightClickHandler());
            await dlg.ShowDialog(OwnerWindow);
        }

        private async void Ribbon_OpenClicked()
        {
            if (OwnerWindow == null)
            {
                Debug.WriteLine("[HaCreatorStateManager] OwnerWindow not set; cannot open FieldSelector");
                return;
            }
            var dlg = new HaCreator.GUI.FieldSelectorDialog(_multiBoard, _tabs, MakeRightClickHandler());
            await dlg.ShowDialog(OwnerWindow);
        }

        public void LoadMap(int mapId) => Debug.WriteLine($"LoadMap({mapId}) stub");

        public void LoadMap()
        {
            lock (_multiBoard)
            {
                if (!_multiBoard.DeviceReady)
                {
                    _ribbon.SetEnabled(true);
                    _ribbon.SetOptions(UserSettings.useMiniMap, UserSettings.emulateParallax,
                        UserSettings.useSnapping, ApplicationSettings.randomTiles, ApplicationSettings.InfoMode);
                    _multiBoard.Start();
                    backupMan.Start();
                    FirstMapLoaded?.Invoke();
                }
                _multiBoard.Focus();
            }
        }

        private void Ribbon_NewPlatformClicked()
        {
            lock (_multiBoard)
            {
                // TODO: port NewPlatform dialog
                Debug.WriteLine("NewPlatform stub");
            }
        }

        private void Ribbon_MapPhysicsClicked()
        {
            // TODO: port MapPhysicsEditor to Avalonia
            Debug.WriteLine("MapPhysicsEditor stub");
        }

        // ── Layer ribbon handlers ─────────────────────────────────────

        private void SetLayer(int currentLayer, int currentPlatform, bool allLayers, bool allPlats)
        {
            _multiBoard.SelectedBoard.SelectedLayerIndex = currentLayer;
            _multiBoard.SelectedBoard.SelectedPlatform = currentPlatform;
            _multiBoard.SelectedBoard.SelectedAllLayers = allLayers;
            _multiBoard.SelectedBoard.SelectedAllPlatforms = allPlats;
            ApplicationSettings.lastDefaultLayer = currentLayer;
            ApplicationSettings.lastAllLayers = allLayers;
        }

        private void Ribbon_LayerViewChanged(int layer, int platform, bool allLayers, bool allPlats, string tileSet)
        {
            if (_multiBoard.SelectedBoard == null) return;
            SetLayer(layer, platform, allLayers, allPlats);
            MultiBoard.ClearSelectedItems(_multiBoard.SelectedBoard);
            tilePanel?.SetSelectedTileSet(tileSet);
        }

        // ── Events ────────────────────────────────────────────────────

        public delegate void EmptyDelegate();
        public event EmptyDelegate? CloseRequested;
        public event EmptyDelegate? FirstMapLoaded;

        // ── Item description ──────────────────────────────────────────

        public static string CreateItemDescription(BoardItem item)
        {
            const string sp = " ";
            var sb = new StringBuilder();

            if (item is TileInstance t)
            {
                sb.Append("[Tile] ");
                sb.Append(sp).Append(((TileInfo)t.BaseInfo).tS).Append('\\')
                  .Append(((TileInfo)t.BaseInfo).u).Append('\\')
                  .Append(((TileInfo)t.BaseInfo).no);
            }
            else if (item is ObjectInstance o)
            {
                sb.Append("[Object] ");
                sb.Append(sp).Append(((ObjectInfo)o.BaseInfo).oS).Append('\\')
                  .Append(((ObjectInfo)o.BaseInfo).l0).Append('\\')
                  .Append(((ObjectInfo)o.BaseInfo).l1).Append('\\')
                  .Append(((ObjectInfo)o.BaseInfo).l2);
            }
            else if (item is BackgroundInstance bg)
            {
                sb.Append("[Background] ");
                sb.Append(sp).Append(((BackgroundInfo)bg.BaseInfo).bS).Append('\\')
                  .Append(((BackgroundInfo)bg.BaseInfo).Type).Append('\\')
                  .Append(((BackgroundInfo)bg.BaseInfo).no);
            }
            else if (item is PortalInstance portal)
            {
                sb.Append("[Portal] ");
                sb.Append(sp).Append("Name: ").Append(portal.pn).AppendLine();
                sb.Append(sp).Append("Type: ").Append(PortalTypeExtensions.GetFriendlyName(portal.pt));
            }
            else if (item is MobInstance mob)
            {
                sb.Append("[Mob] ");
                sb.Append(sp).Append("Name: ").Append(((MobInfo)mob.BaseInfo).Name).AppendLine();
                sb.Append(sp).Append("ID: ").Append(((MobInfo)mob.BaseInfo).ID);
            }
            else if (item is NpcInstance npc)
            {
                sb.Append("[Npc] ");
                sb.Append(sp).Append("Name: ").Append(((NpcInfo)npc.BaseInfo).StringName).AppendLine();
                sb.Append(sp).Append("ID: ").Append(((NpcInfo)npc.BaseInfo).ID);
            }
            else if (item is ReactorInstance reactor)
            {
                sb.Append("[Reactor] ");
                sb.Append("ID: ").Append(((ReactorInfo)reactor.BaseInfo).ID);
            }
            else if (item is FootholdAnchor fh)
            {
                sb.Append("[Foothold Anchor] ");
                sb.Append("X: ").Append(fh.X).AppendLine();
                sb.Append("Y: ").Append(fh.Y).AppendLine();
            }
            else if (item is RopeAnchor rope)
            {
                sb.Append(rope.ParentRope.ladder ? "[Ladder] " : "[Rope] ");
                sb.Append("X: ").Append(rope.X).AppendLine();
                sb.Append("Y: ").Append(rope.Y).AppendLine();
            }
            else if (item is Chair chair)
            {
                sb.Append("[Chair] ");
                sb.Append("X: ").Append(chair.X).AppendLine();
                sb.Append("Y: ").Append(chair.Y).AppendLine();
            }
            else if (item is ToolTipChar || item is ToolTipDot || item is ToolTipInstance)
            {
                sb.Append("[Tooltip] ");
            }
            else if (item is INamedMisc misc)
            {
                sb.Append(misc.Name);
            }
            else if (item is MirrorFieldData mfd)
            {
                sb.Append("[MirrorFieldData] Ground reflections for '").Append(mfd.MirrorFieldDataType).Append('\'');
            }
            else if (item is VRDot vrd)
            {
                sb.Append("[VR Dot] ");
                sb.Append("X: ").Append(vrd.X).AppendLine();
                sb.Append("Y: ").Append(vrd.Y).AppendLine();
            }
            else if (item is MinimapDot mmd)
            {
                sb.Append("[Minimap Dot] ");
                sb.Append("X: ").Append(mmd.X).AppendLine();
                sb.Append("Y: ").Append(mmd.Y).AppendLine();
            }
            else
            {
                sb.Append("[Unknown] ").Append(item);
            }

            sb.AppendLine();
            sb.Append("Width: ").Append(item.Width).Append(", Height: ").Append(item.Height);
            return sb.ToString();
        }

        // ── Panel setters ─────────────────────────────────────────────

        public void SetTilePanel(TilePanel tp) => tilePanel = tp;
        public void SetObjPanel(ObjPanel op) => objPanel = op;
        public void SetBlackBorderPanel(BlackBorderPanel op) => blackBorderPanel = op;
        public void SetBackgroundPanel(BackgroundPanel bp) => backgroundPanel = bp;
        public void SetLifePanel(LifePanel lp) => lifePanel = lp;
        public void SetObjectViewerPanel(ObjectViewerPanel ovp) => objectViewerPanel = ovp;

        // ── Edit mode ─────────────────────────────────────────────────

        public void EnterEditMode(ItemTypes type)
        {
            _multiBoard.SelectedBoard.EditedTypes = type;
            _multiBoard.SelectedBoard.VisibleTypes |= type;
            _ribbon.SetEnabled(false);
        }

        public void ExitEditMode()
        {
            _multiBoard.SelectedBoard.EditedTypes = ApplicationSettings.theoreticalEditedTypes;
            _multiBoard.SelectedBoard.VisibleTypes = ApplicationSettings.theoreticalVisibleTypes;
            _ribbon.SetEnabled(true);
        }

        // ── Hot swap ──────────────────────────────────────────────────

        public HotSwapRefreshService HotSwapService => _hotSwapService;
        public AssetUsageTracker AssetUsageTracker => _assetUsageTracker;

        public void InitializeHotSwap()
        {
            if (Program.DataSource is MapleLib.Img.ImgFileSystemDataSource imgDataSource)
            {
                _assetUsageTracker = new AssetUsageTracker();
                _hotSwapService = new HotSwapRefreshService(
                    Program.InfoManager,
                    System.Threading.SynchronizationContext.Current);
                _hotSwapService.SubscribeToDataSource(imgDataSource);
                tilePanel?.SubscribeToHotSwap(_hotSwapService);
                objPanel?.SubscribeToHotSwap(_hotSwapService);
                backgroundPanel?.SubscribeToHotSwap(_hotSwapService);
                lifePanel?.SubscribeToHotSwap(_hotSwapService);
            }
        }

        public void RegisterBoardAssets(Board board) => _assetUsageTracker?.RegisterBoardAssets(board);
        public void UnregisterBoardAssets(Board board) => _assetUsageTracker?.UnregisterBoardAssets(board);

        public void DisposeHotSwap()
        {
            _hotSwapService?.Dispose();
            _hotSwapService = null;
            _assetUsageTracker = null;
        }

        // ── Accessors ─────────────────────────────────────────────────

        public MultiBoard MultiBoard => _multiBoard;
        public HaRibbon Ribbon => _ribbon;
    }
}
