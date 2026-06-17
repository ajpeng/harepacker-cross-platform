/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using Avalonia.Controls;
using Avalonia.Input;
using HaCreator.Collections;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapEditor
{
    /// <summary>
    /// Stub for the MonoGame+Avalonia map editor surface (WPF MultiBoard.xaml.cs equivalent).
    /// Provides the interface used by Board, Mouse, and drawing code; full implementation pending.
    /// </summary>
    public class MultiBoard
    {
        public static Color InactiveColor;
        public static Color RopeInactiveColor;
        public static Color FootholdInactiveColor;
        public static Color ChairInactiveColor;
        public static Color ToolTipInactiveColor;
        public static Color MiscInactiveColor;
        public static Color VRInactiveColor;
        public static Color MinimapBoundInactiveColor;
        public static float FirstSnapVerification;

        public static Color CreateTransparency(Color orgColor, int alpha)
            => new Color(orgColor.R, orgColor.B, orgColor.G, alpha);

        public static void RecalculateSettings()
        {
            int alpha = UserSettings.NonActiveAlpha;
            FirstSnapVerification = UserSettings.SnapDistance * 20;
            InactiveColor = CreateTransparency(Color.White, alpha);
            RopeInactiveColor = CreateTransparency(UserSettings.RopeColor, alpha);
            FootholdInactiveColor = CreateTransparency(UserSettings.FootholdColor, alpha);
            ChairInactiveColor = CreateTransparency(UserSettings.ChairColor, alpha);
            ToolTipInactiveColor = CreateTransparency(UserSettings.ToolTipColor, alpha);
            MiscInactiveColor = CreateTransparency(UserSettings.MiscColor, alpha);
            VRInactiveColor = CreateTransparency(UserSettings.VRColor, alpha);
            MinimapBoundInactiveColor = CreateTransparency(UserSettings.MinimapBoundColor, alpha);
        }

        public GraphicsDevice? GraphicsDevice { get; set; }
        public SpriteBatch? SpriteBatch { get; set; }
        public Texture2D? Pixel { get; set; }

        // Reference to the Avalonia host control for context-menu anchoring
        public Control? HostControl { get; set; }

        public System.Drawing.Size CurrentDXWindowSize { get; set; } = new System.Drawing.Size(800, 600);

        public FontEngine? FontEngine { get; set; }

        public UserObjectsManager? UserObjects { get; set; }

        public List<Board> Boards { get; } = new List<Board>();

        public Board? SelectedBoard { get; set; }

        public double MaxHScroll { get; set; }
        public double MaxVScroll { get; set; }

        public static int VirtualToPhysical(int location, int center, int scroll, int origin)
            => location + center - scroll - origin;

        public static int VirtualToPhysical(int location, int center, int scroll, int origin, float zoom)
            => (int)((location + center - origin) / zoom - scroll);

        public static int PhysicalToVirtual(int location, int center, int scroll, int origin)
            => location - center + scroll + origin;

        public static int PhysicalToVirtual(int location, int center, int scroll, int origin, float zoom)
            => (int)(location / zoom) - center + scroll + origin;

        /// <summary>Returns true if the selected board has a real layer selected; posts a warning dialog if not.</summary>
        public bool AssertLayerSelected()
        {
            if (SelectedBoard != null && SelectedBoard.SelectedLayerIndex != -1) return true;
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var owner = (Avalonia.Controls.Window?)Program.HaEditorWindow;
                var btn = new Avalonia.Controls.Button { Content = "OK", IsDefault = true,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
                var win = new Avalonia.Controls.Window
                {
                    Title = "No Layer Selected", Width = 320, Height = 130,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                    Content = new Avalonia.Controls.StackPanel
                    {
                        Margin = new Avalonia.Thickness(12), Spacing = 10,
                        Children =
                        {
                            new Avalonia.Controls.TextBlock { Text = "Please select a layer first." },
                            btn
                        }
                    }
                };
                btn.Click += (_, _) => win.Close();
                if (owner != null) await win.ShowDialog(owner);
                else win.Show();
            });
            return false;
        }

        public bool IsItemInRange(int x, int y, int w, int h, int xshift, int yshift)
        {
            if (CurrentDXWindowSize.Width == 0) return true;
            return x + xshift < CurrentDXWindowSize.Width && x + xshift + w > 0
                && y + yshift < CurrentDXWindowSize.Height && y + yshift + h > 0;
        }

        // Physical size of the rendering surface (set by host when resized)
        public double ActualWidth  => CurrentDXWindowSize.Width;
        public double ActualHeight => CurrentDXWindowSize.Height;

        // Tracks whether Ctrl is currently held (updated by the Avalonia host key handler)
        public bool CtrlHeld { get; set; }

        public void SetHScrollbarValue(int value)
        {
            if (SelectedBoard != null)
                SelectedBoard.hScroll = value;
        }

        public void SetVScrollbarValue(int value)
        {
            if (SelectedBoard != null)
                SelectedBoard.vScroll = value;
        }

        /// <summary>Scrolls horizontally by <paramref name="delta"/>. Returns true if the scroll position changed.</summary>
        public bool AddHScrollbarValue(int delta)
        {
            if (SelectedBoard == null) return false;
            int max = Math.Max(0, (SelectedBoard.MapSize.X - CurrentDXWindowSize.Width) / 2);
            int newVal = Math.Clamp(SelectedBoard.hScroll + delta, -max, max);
            if (newVal == SelectedBoard.hScroll) return false;
            SelectedBoard.hScroll = newVal;
            return true;
        }

        /// <summary>Scrolls vertically by <paramref name="delta"/>. Returns true if the scroll position changed.</summary>
        public bool AddVScrollbarValue(int delta)
        {
            if (SelectedBoard == null) return false;
            int max = Math.Max(0, (SelectedBoard.MapSize.Y - CurrentDXWindowSize.Height) / 2);
            int newVal = Math.Clamp(SelectedBoard.vScroll + delta, -max, max);
            if (newVal == SelectedBoard.vScroll) return false;
            SelectedBoard.vScroll = newVal;
            return true;
        }

        public void AdjustScrollBars()
        {
            if (SelectedBoard == null) return;
            MaxHScroll = Math.Max(0, (SelectedBoard.MapSize.X - CurrentDXWindowSize.Width)  / 2);
            MaxVScroll = Math.Max(0, (SelectedBoard.MapSize.Y - CurrentDXWindowSize.Height) / 2);
        }
        public void OnMinimapStateChanged(Board board, bool hasMm) => MinimapStateChanged?.Invoke(this, hasMm);
        public void OnSelectedItemChanged(BoardItem? selectedItem) => SelectedItemChanged?.Invoke(selectedItem);
        public void LayerTSChanged(Layer layer) { OnLayerTSChanged?.Invoke(layer); }

        // Fire helpers for input events
        public void EditInstanceClicked(BoardItem target)    => OnEditInstanceClicked?.Invoke(target);
        public void EditBaseClicked(BoardItem target)        => OnEditBaseClicked?.Invoke(target);
        public void BringToFrontClicked(BoardItem target)    => OnBringToFrontClicked?.Invoke(target);
        public void SendToBackClicked(BoardItem target)      => OnSendToBackClicked?.Invoke(target);
        public void InvokeReturnToSelectionState()           => ReturnToSelectionState?.Invoke();
        public void OnExportRequested()                      => ExportRequested?.Invoke();
        public void OnLoadRequested()                        => LoadRequested?.Invoke();
        public void OnCloseTabRequested()                    => CloseTabRequested?.Invoke();
        public void OnSwitchTabRequested(bool shift)         => SwitchTabRequested?.Invoke(this, shift);
        public void ShowContextMenuAtPointer(Avalonia.Controls.ContextMenu menu)
        {
            if (HostControl != null)
                menu.Open(HostControl);
        }

        public static bool IsItemUnderRectangle(BoardItem item, Microsoft.Xna.Framework.Rectangle rect)
            => rect.Contains(new Microsoft.Xna.Framework.Point(item.X, item.Y));

        public static bool IsPointInsideRectangle(Microsoft.Xna.Framework.Point point, int left, int top, int right, int bottom)
            => point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;

        public void OnBoardRemoved(Board board) => BoardRemoved?.Invoke(board, EventArgs.Empty);
        public void OnImageDropped(Board board, SkiaSharp.SKBitmap bmp, string name,
            Microsoft.Xna.Framework.Point pos) => ImageDropped?.Invoke(board, bmp, name, pos);
        public void UndoListChanged() { OnUndoListChanged?.Invoke(); }
        public void RedoListChanged() { OnRedoListChanged?.Invoke(); }

        public delegate void UndoRedoDelegate();
        public event UndoRedoDelegate? OnUndoListChanged;
        public event UndoRedoDelegate? OnRedoListChanged;

        public delegate void LayerTSChangedDelegate(Layer layer);
        public event LayerTSChangedDelegate? OnLayerTSChanged;

        public void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            if (Pixel == null) return;
            int len = (int)Vector2.Distance(start, end);
            if (len <= 0) return;
            float rotation = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
            sprite.Draw(Pixel,
                new Rectangle((int)start.X, (int)start.Y, len, Math.Max(1, UserSettings.LineWidth)),
                null, color, rotation, Vector2.Zero, SpriteEffects.None, 1f);
        }

        public void DrawRectangle(SpriteBatch sprite, Rectangle rect, Color color)
        {
            var tl = new Vector2(rect.Left,  rect.Top);
            var tr = new Vector2(rect.Right, rect.Top);
            var br = new Vector2(rect.Right, rect.Bottom);
            var bl = new Vector2(rect.Left,  rect.Bottom);
            DrawLine(sprite, tl, tr, color);
            DrawLine(sprite, tr, br, color);
            DrawLine(sprite, br, bl, color);
            DrawLine(sprite, bl, tl, color);
        }

        public void FillRectangle(SpriteBatch sprite, Rectangle rect, Color color)
        {
            if (Pixel == null) return;
            sprite.Draw(Pixel, rect, color);
        }

        public void DrawDot(SpriteBatch sprite, int x, int y, Color color, int dotSize)
        {
            int half = UserSettings.DotWidth * dotSize;
            FillRectangle(sprite, new Rectangle(x - half, y - half, half * 2, half * 2), color);
        }

        /// <summary>The virtual map size of the currently selected board, or Point.Zero if none.</summary>
        public Point MapSize => SelectedBoard?.MapSize ?? Point.Zero;

        // ── Rendering ─────────────────────────────────────────────────────

        /// <summary>
        /// Renders the currently selected board using the provided SpriteBatch and pixel texture.
        /// Called by MapEditorGame.Draw() on the MonoGame game thread.
        /// GraphicsDevice must already have a RenderTarget set before calling.
        /// </summary>
        public void RenderFrame(SpriteBatch sprite, Texture2D pixel)
        {
            // Temporarily set Pixel so Draw* helpers work
            Pixel = pixel;

            float zoom = SelectedBoard?.Zoom ?? 1.0f;

            // Pass 1 — backgrounds (no zoom, stays at fixed screen position)
            if (SelectedBoard != null)
            {
                sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
                lock (this) { SelectedBoard?.RenderBackgrounds(sprite); }
                sprite.End();
            }

            // Pass 2 — main layer with zoom transform
            sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied,
                null, null, null, null, Matrix.CreateScale(zoom));
            if (SelectedBoard != null)
            {
                lock (this)
                {
                    if (SelectedBoard != null)
                    {
                        SelectedBoard.RenderBoard(sprite);
                        var mapSz = SelectedBoard.MapSize;
                        var wnd   = CurrentDXWindowSize;
                        if (mapSz.X < wnd.Width)
                            DrawLine(sprite, new Vector2(mapSz.X, 0), new Vector2(mapSz.X, wnd.Height), Color.Black);
                        if (mapSz.Y < wnd.Height)
                            DrawLine(sprite, new Vector2(0, mapSz.Y), new Vector2(wnd.Width, mapSz.Y), Color.Black);
                    }
                }
            }
            sprite.End();

            // Pass 3 — front backgrounds (no zoom)
            if (SelectedBoard != null)
            {
                sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
                lock (this) { SelectedBoard?.RenderFrontBackgrounds(sprite); }
                sprite.End();
            }

            // Pass 4 — minimap overlay (no zoom)
            if (SelectedBoard != null)
            {
                sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
                lock (this) { SelectedBoard?.RenderMinimap(sprite); }
                sprite.End();
            }
        }

        // ── Input handling (called by MapEditorControl) ────────────────────

        public void HandleMouseMove(int x, int y)
        {
            if (SelectedBoard == null) return;
            lock (this)
            {
                float zoom = SelectedBoard.Zoom;
                int physX = VirtualToPhysical(SelectedBoard.Mouse.X, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom);
                int physY = VirtualToPhysical(SelectedBoard.Mouse.Y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom);
                if (physX == x && physY == y) return;

                var oldPos = new Point(SelectedBoard.Mouse.X, SelectedBoard.Mouse.Y);
                var newPos = new Point(
                    PhysicalToVirtual(x, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom),
                    PhysicalToVirtual(y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom));
                SelectedBoard.Mouse.Move(newPos.X, newPos.Y);
                MouseMoved?.Invoke(SelectedBoard, oldPos, newPos, new Point(x, y));
            }
        }

        public void HandleMouseDown(int x, int y, bool left, bool right)
        {
            if (SelectedBoard == null) return;
            HandleMouseMove(x, y);
            lock (this)
            {
                float zoom = SelectedBoard.Zoom;
                var realPos = new Point(x, y);
                var virtPos = new Point(
                    PhysicalToVirtual(x, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom),
                    PhysicalToVirtual(y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom));
                SelectedBoard.Mouse.IsDown = true;
                if (left)
                {
                    var objs = GetObjectsUnderPoint(realPos, out bool selHigher);
                    LeftMouseDown?.Invoke(SelectedBoard, objs.NonSelectedItem, objs.SelectedItem, realPos, virtPos, selHigher);
                }
                else if (right)
                {
                    RightMouseClick?.Invoke(SelectedBoard, GetObjectUnderPoint(realPos), realPos, virtPos, SelectedBoard.Mouse.State);
                }
            }
        }

        public void HandleMouseUp(int x, int y, bool left, bool right)
        {
            if (SelectedBoard == null) return;
            lock (this)
            {
                float zoom = SelectedBoard.Zoom;
                var realPos = new Point(x, y);
                var virtPos = new Point(
                    PhysicalToVirtual(x, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom),
                    PhysicalToVirtual(y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom));
                SelectedBoard.Mouse.IsDown = false;
                if (left)
                {
                    var objs = GetObjectsUnderPoint(realPos, out bool selHigher);
                    LeftMouseUp?.Invoke(SelectedBoard, objs.NonSelectedItem, objs.SelectedItem, realPos, virtPos, selHigher);
                }
            }
        }

        public void HandleMouseDoubleClick(int x, int y)
        {
            if (SelectedBoard == null) return;
            lock (this)
            {
                float zoom = SelectedBoard.Zoom;
                var realPos = new Point(x, y);
                var virtPos = new Point(
                    PhysicalToVirtual(x, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom),
                    PhysicalToVirtual(y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom));
                MouseDoubleClick?.Invoke(SelectedBoard, GetObjectUnderPoint(realPos), realPos, virtPos);
            }
        }

        public void HandleMouseWheel(int delta)
        {
            if (!AddHScrollbarValue(delta))
                AddVScrollbarValue(delta);
        }

        public void HandleKeyDown(bool ctrl, bool shift, bool alt, Key key)
        {
            if (SelectedBoard == null) return;
            lock (this)
            {
                ShortcutKeyPressed?.Invoke(SelectedBoard, ctrl, shift, alt, key);
            }
        }

        // ── Hit testing ───────────────────────────────────────────────────

        private void GetObjsUnderPointFromList(
            Collections.IMapleList list, Point virtualPos,
            ref BoardItem? item, ref BoardItem? selected, ref bool selHigher)
        {
            if (!list.IsItem || SelectedBoard == null) return;
            var sel = SelectedBoard.GetUserSelectionInfo();

            for (int i = 0; i < list.Count; i++)
            {
                var bi = (BoardItem)list[i];
                if (list.ListType == MapleLib.WzLib.WzStructure.Data.ItemTypes.None)
                {
                    if ((SelectedBoard.EditedTypes & bi.Type) != bi.Type) continue;
                }
                else
                {
                    if ((SelectedBoard.EditedTypes & list.ListType) != list.ListType) continue;
                }
                if (bi is Mouse) continue;
                if (!bi.CheckIfLayerSelected(sel)) continue;
                if (!IsPointInsideRectangle(virtualPos, bi.Left, bi.Top, bi.Right, bi.Bottom)) continue;
                if (bi.IsPixelTransparent(virtualPos.X - bi.Left, virtualPos.Y - bi.Top)) continue;

                if (bi.Selected) { selected = bi; selHigher = true; }
                else             { item     = bi; selHigher = false; }
            }
        }

        internal BoardItemPair GetObjectsUnderPoint(Point physical, out bool selHigher)
        {
            selHigher = false;
            if (SelectedBoard == null) return new BoardItemPair(null, null);
            float zoom = SelectedBoard.Zoom;
            var virtualPos = new Point(
                PhysicalToVirtual(physical.X, SelectedBoard.CenterPoint.X, SelectedBoard.hScroll, 0, zoom),
                PhysicalToVirtual(physical.Y, SelectedBoard.CenterPoint.Y, SelectedBoard.vScroll, 0, zoom));
            BoardItem? item = null, selected = null;
            foreach (var list in SelectedBoard.BoardItems.AllItemLists)
                GetObjsUnderPointFromList(list, virtualPos, ref item, ref selected, ref selHigher);
            return new BoardItemPair(item, selected);
        }

        private BoardItem? GetObjectUnderPoint(Point physical)
        {
            var pair = GetObjectsUnderPoint(physical, out bool selHigher);
            if (pair.SelectedItem == null) return pair.NonSelectedItem;
            if (pair.NonSelectedItem == null) return pair.SelectedItem;
            return selHigher ? pair.SelectedItem : pair.NonSelectedItem;
        }

        // ── Properties required by HaCreatorStateManager ─────────────

        public HaCreator.GUI.HaRibbon? Ribbon { get; set; }

        private HaCreatorStateManager? _stateManager;
        public HaCreatorStateManager? HaCreatorStateManager
        {
            get => _stateManager;
            set => _stateManager = value;
        }

        public bool DeviceReady { get; set; } = false;

        public bool IsVisible { get; set; } = true;

        public void Start() { }

        public void Focus() { }

        public void DxContainer_KeyDown(object? sender, EventArgs e) { }

        // ── Events required by HaCreatorStateManager ──────────────────

        public delegate void BoardItemDelegate(BoardItem item);
        public delegate void BoardItemRefDelegate(BoardItem boardRefItem);
        public delegate void LayerDelegate(Layer layer);
        public delegate void MouseMovedDelegate(Board selectedBoard, Microsoft.Xna.Framework.Point oldPos,
            Microsoft.Xna.Framework.Point newPos, Microsoft.Xna.Framework.Point currPhysicalPos);
        public delegate void ImageDroppedDelegate(Board selectedBoard, SkiaSharp.SKBitmap bmp, string name,
            Microsoft.Xna.Framework.Point pos);
        public delegate void SwitchTabDelegate(object sender, bool reverse);
        public delegate void BoardEventDelegate(object sender, EventArgs e);
        public delegate void MinimapDelegate(object sender, bool hasMm);

        // ── Input event delegates (consumed by InputHandler) ──────────
        public delegate void LeftMouseDownDelegate(Board selectedBoard, BoardItem item, BoardItem selectedItem,
            Microsoft.Xna.Framework.Point realPosition, Microsoft.Xna.Framework.Point virtualPosition, bool selectedItemHigher);
        public delegate void LeftMouseUpDelegate(Board selectedBoard, BoardItem target, BoardItem selectedTarget,
            Microsoft.Xna.Framework.Point realPosition, Microsoft.Xna.Framework.Point virtualPosition, bool selectedItemHigher);
        public delegate void RightMouseClickDelegate(Board selectedBoard, BoardItem rightClickTarget,
            Microsoft.Xna.Framework.Point realPosition, Microsoft.Xna.Framework.Point virtualPosition, MouseState mouseState);
        public delegate void MouseDoubleClickDelegate(Board selectedBoard, BoardItem target,
            Microsoft.Xna.Framework.Point realPosition, Microsoft.Xna.Framework.Point virtualPosition);
        public delegate void ShortcutKeyPressedDelegate(Board selectedBoard, bool ctrl, bool shift, bool alt, Key key);

        public event BoardItemRefDelegate? OnBringToFrontClicked;
        public event BoardItemDelegate? OnEditBaseClicked;
        public event BoardItemDelegate? OnEditInstanceClicked;
        public event BoardItemRefDelegate? OnSendToBackClicked;
        public event Action? ReturnToSelectionState;
        public event BoardItemDelegate? SelectedItemChanged;
        public event MouseMovedDelegate? MouseMoved;
        public event ImageDroppedDelegate? ImageDropped;
        public event Action? ExportRequested;
        public event Action? LoadRequested;
        public event Action? CloseTabRequested;
        public event SwitchTabDelegate? SwitchTabRequested;
        // BackupCheck removed — driven by BackupManager's internal timer
        public event BoardEventDelegate? BoardRemoved;
        public event MinimapDelegate? MinimapStateChanged;
        public event LeftMouseDownDelegate? LeftMouseDown;
        public event LeftMouseUpDelegate? LeftMouseUp;
        public event RightMouseClickDelegate? RightMouseClick;
        public event MouseDoubleClickDelegate? MouseDoubleClick;
        public event ShortcutKeyPressedDelegate? ShortcutKeyPressed;

        public Board CreateBoard(Microsoft.Xna.Framework.Point mapSize, Microsoft.Xna.Framework.Point centerPoint, object? menu, bool bIsNewMapDesign)
        {
            lock (this)
            {
                Board newBoard = new Board(
                    new Microsoft.Xna.Framework.Point(mapSize.X, mapSize.Y),
                    centerPoint, this, bIsNewMapDesign, menu,
                    ApplicationSettings.theoreticalVisibleTypes,
                    ApplicationSettings.theoreticalEditedTypes);
                Boards.Add(newBoard);
                newBoard.CreateMapLayers();
                return newBoard;
            }
        }

        // ── Static board helpers (extracted from InputHandler) ────────

        public static void ClearSelectedItems(Board board)
        {
            lock (board.ParentControl)
            {
                while (board.SelectedItems.Count > 0)
                    board.SelectedItems[0].Selected = false;
            }
        }

        public static void ClearBoundItems(Board board)
        {
            if (board?.Mouse == null) return;
            lock (board.ParentControl)
            {
                var items = new System.Collections.Generic.List<BoardItem>(board.Mouse.BoundItems.Keys);
                foreach (var item in items)
                    board.Mouse.ReleaseItem(item);
            }
        }
    }
}
