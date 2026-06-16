/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using Avalonia.Input;
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

        public void SetHScrollbarValue(int value) { }
        public void SetVScrollbarValue(int value) { }
        public void AddHScrollbarValue(int value) { } // TODO: update hScroll on SelectedBoard
        public void AddVScrollbarValue(int value) { } // TODO: update vScroll on SelectedBoard
        public void AdjustScrollBars() { }
        public void OnMinimapStateChanged(Board board, bool hasMm) { }
        public void OnSelectedItemChanged(BoardItem? selectedItem) { }
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
        public void ShowContextMenuAtPointer(Avalonia.Controls.ContextMenu menu) { } // TODO: implement with host control

        public static bool IsItemUnderRectangle(BoardItem item, Microsoft.Xna.Framework.Rectangle rect)
            => rect.Contains(new Microsoft.Xna.Framework.Point(item.X, item.Y));

        public static bool IsPointInsideRectangle(Microsoft.Xna.Framework.Point point, int left, int top, int right, int bottom)
            => point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;

        public void OnBoardRemoved(Board board) { }
        public void UndoListChanged() { OnUndoListChanged?.Invoke(); }
        public void RedoListChanged() { OnRedoListChanged?.Invoke(); }

        public delegate void UndoRedoDelegate();
        public event UndoRedoDelegate? OnUndoListChanged;
        public event UndoRedoDelegate? OnRedoListChanged;

        public delegate void LayerTSChangedDelegate(Layer layer);
        public event LayerTSChangedDelegate? OnLayerTSChanged;

        public void DrawRectangle(SpriteBatch sprite, Rectangle rect, Color color) { }
        public void FillRectangle(SpriteBatch sprite, Rectangle rect, Color color) { }
        public void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color) { }
        public void DrawDot(SpriteBatch sprite, int x, int y, Color color, int dotSize) { }

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
        public event LayerDelegate? OnLayerTSChanged2;  // separate from the layer-TS delegate above
        public event BoardItemRefDelegate? OnSendToBackClicked;
        public event Action? ReturnToSelectionState;
        public event BoardItemDelegate? SelectedItemChanged;
        public event MouseMovedDelegate? MouseMoved;
        public event ImageDroppedDelegate? ImageDropped;
        public event Action? ExportRequested;
        public event Action? LoadRequested;
        public event Action? CloseTabRequested;
        public event SwitchTabDelegate? SwitchTabRequested;
        public event Action? BackupCheck;
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
