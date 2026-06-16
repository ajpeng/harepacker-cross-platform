/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Input;
using XNA = Microsoft.Xna.Framework;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.Exceptions;
using HaCreator.MapEditor.Info;

namespace HaCreator.MapEditor.Input
{
    public class InputHandler
    {
        private MultiBoard parentBoard;
        private XNA.Point lastPhysicalPos = new XNA.Point(0, 0);

        // In-memory clipboard (replaces System.Windows.Forms.Clipboard)
        private static readonly Dictionary<string, object> _clipboard = new();

        public void OnUserInteraction()
        {
            if (parentBoard?.SelectedBoard != null)
                parentBoard.SelectedBoard.Dirty = true;
        }

        // Stubs: no P/Invoke on cross-platform
        public static bool IsKeyPushedDown(Key vKey) => false;
        public static bool IsShiftDown() => false;

        public InputHandler(MultiBoard parentBoard)
        {
            this.parentBoard = parentBoard;
            parentBoard.LeftMouseDown    += parentBoard_LeftMouseDown;
            parentBoard.LeftMouseUp      += parentBoard_LeftMouseUp;
            parentBoard.RightMouseClick  += parentBoard_RightMouseClick;
            parentBoard.MouseDoubleClick += parentBoard_MouseDoubleClick;
            parentBoard.ShortcutKeyPressed += ParentBoard_ShortcutKeyPressed;
            parentBoard.MouseMoved       += parentBoard_MouseMoved;
        }

        public static XNA.Rectangle CreateRectangle(XNA.Point a, XNA.Point b)
        {
            int left, right, top, bottom;
            if (a.X < b.X) { left = a.X; right = b.X; } else { left = b.X; right = a.X; }
            if (a.Y < b.Y) { top = a.Y; bottom = b.Y; } else { top = b.Y; bottom = a.Y; }
            return new XNA.Rectangle(left, top, right - left, bottom - top);
        }

        public static double Distance(double x, double y) => Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

        private UndoRedoAction CreateItemUndoMoveAction(BoardItem item, XNA.Point posChange)
        {
            if (item is BackgroundInstance bi)
                return UndoRedoManager.BackgroundMoved(bi,
                    new XNA.Point(bi.BaseX + posChange.X, bi.BaseY + posChange.Y),
                    new XNA.Point(bi.BaseX, bi.BaseY));
            return UndoRedoManager.ItemMoved(item,
                new XNA.Point(item.X + posChange.X, item.Y + posChange.Y),
                new XNA.Point(item.X, item.Y));
        }

        private void parentBoard_MouseMoved(Board selectedBoard, XNA.Point oldPos, XNA.Point newPos, XNA.Point currPhysicalPos)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (selectedBoard.Mouse.MinimapBrowseOngoing && selectedBoard.Mouse.State == MouseState.Selection)
                {
                    HandleMinimapBrowse(selectedBoard, currPhysicalPos);
                }
                else if (selectedBoard.Mouse.MultiSelectOngoing &&
                    (Math.Abs(selectedBoard.Mouse.X - selectedBoard.Mouse.MultiSelectStart.X) > 1 ||
                     Math.Abs(selectedBoard.Mouse.Y - selectedBoard.Mouse.MultiSelectStart.Y) > 1))
                {
                    XNA.Rectangle oldRect = CreateRectangle(oldPos, selectedBoard.Mouse.MultiSelectStart);
                    XNA.Rectangle newRect = CreateRectangle(newPos, selectedBoard.Mouse.MultiSelectStart);
                    List<BoardItem> toRemove = new List<BoardItem>();
                    SelectionInfo sel = selectedBoard.GetUserSelectionInfo();
                    foreach (BoardItem item in selectedBoard.BoardItems.Items)
                    {
                        if (MultiBoard.IsItemUnderRectangle(item, newRect) && (sel.editedTypes & item.Type) == item.Type && item.CheckIfLayerSelected(sel))
                            item.Selected = true;
                        else if (item.Selected && MultiBoard.IsItemUnderRectangle(item, oldRect))
                            toRemove.Add(item);
                    }
                    foreach (BoardItem item in toRemove) item.Selected = false;
                }
                else if (selectedBoard.Mouse.SingleSelectStarting &&
                    (Distance(newPos.X - selectedBoard.Mouse.SingleSelectStart.X, newPos.Y - selectedBoard.Mouse.SingleSelectStart.Y) > UserSettings.SignificantDistance ||
                     IsKeyPushedDown(Key.LeftAlt)))
                {
                    BindAllSelectedItems(selectedBoard, selectedBoard.Mouse.SingleSelectStart);
                    selectedBoard.Mouse.SingleSelectStarting = false;
                }
                else if (selectedBoard.Mouse.BoundItems.Count > 0)
                {
                    if (UserSettings.useSnapping && !IsKeyPushedDown(Key.LeftAlt))
                    {
                        MouseState state = selectedBoard.Mouse.State;
                        if (state == MouseState.Selection || state == MouseState.StaticObjectAdding ||
                            state == MouseState.RandomTiles || state == MouseState.Ropes ||
                            state == MouseState.Footholds || state == MouseState.Chairs)
                        {
                            foreach (BoardItem item in selectedBoard.Mouse.BoundItems.Keys.ToList())
                                if (item is ISnappable s) s.DoSnap();
                        }
                    }
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    selectedBoard.Mouse.DoSnap();
                }

                int deltaX = currPhysicalPos.X - lastPhysicalPos.X;
                int deltaY = currPhysicalPos.Y - lastPhysicalPos.Y;
                lastPhysicalPos = currPhysicalPos;

                if ((selectedBoard.Mouse.BoundItems.Count > 0 || selectedBoard.Mouse.MultiSelectOngoing) &&
                    selectedBoard.Mouse.State == MouseState.Selection)
                {
                    int boardWidth  = (int)parentBoard.ActualWidth;
                    int boardHeight = (int)parentBoard.ActualHeight;
                    currPhysicalPos = new XNA.Point(
                        Math.Min(Math.Max(currPhysicalPos.X, 0), boardWidth),
                        Math.Min(Math.Max(currPhysicalPos.Y, 0), boardHeight));
                    const int minMovement = 5;
                    if (currPhysicalPos.X - UserSettings.ScrollDistance < 0 && deltaX < -minMovement)
                        selectedBoard.hScroll = (int)Math.Max(0, selectedBoard.hScroll - Math.Pow(UserSettings.ScrollBase, (UserSettings.ScrollDistance - currPhysicalPos.X) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor);
                    else if (currPhysicalPos.X + UserSettings.ScrollDistance > boardWidth && deltaX > minMovement)
                        selectedBoard.hScroll = (int)Math.Min(selectedBoard.hScroll + Math.Pow(UserSettings.ScrollBase, (currPhysicalPos.X - boardWidth + UserSettings.ScrollDistance) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor, parentBoard.MaxHScroll);
                    if (currPhysicalPos.Y - UserSettings.ScrollDistance < 0 && deltaY < -minMovement)
                        selectedBoard.vScroll = (int)Math.Max(0, selectedBoard.vScroll - Math.Pow(UserSettings.ScrollBase, (UserSettings.ScrollDistance - currPhysicalPos.Y) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor);
                    else if (currPhysicalPos.Y + UserSettings.ScrollDistance > boardHeight && deltaY > minMovement)
                        selectedBoard.vScroll = (int)Math.Min(selectedBoard.vScroll + Math.Pow(UserSettings.ScrollBase, (currPhysicalPos.Y - boardHeight + UserSettings.ScrollDistance) * UserSettings.ScrollExponentFactor) * UserSettings.ScrollFactor, parentBoard.MaxVScroll);
                }
            }
        }

        private void ParentBoard_ShortcutKeyPressed(Board selectedBoard, bool ctrl, bool shift, bool alt, Key key)
        {
            lock (parentBoard)
            {
                if (parentBoard == null || parentBoard.SelectedBoard == null) return;
                OnUserInteraction();
                List<UndoRedoAction> actions = new List<UndoRedoAction>();
                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LeftAlt || key == Key.RightAlt)
                    return;
                bool clearRedo = true;
                const int navSpeed = 16;

                switch (key)
                {
                    case Key.Left:
                        if (selectedBoard.SelectedItems.Count > 0)
                        { foreach (BoardItem item in selectedBoard.SelectedItems) if (!item.BoundToSelectedItem(selectedBoard)) { item.X--; actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(1, 0))); } }
                        else selectedBoard.ParentControl.AddHScrollbarValue(-navSpeed);
                        break;
                    case Key.Right:
                        if (selectedBoard.SelectedItems.Count > 0)
                        { foreach (BoardItem item in selectedBoard.SelectedItems) if (!item.BoundToSelectedItem(selectedBoard)) { item.X++; actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(-1, 0))); } }
                        else selectedBoard.ParentControl.AddHScrollbarValue(navSpeed);
                        break;
                    case Key.Up:
                        if (selectedBoard.SelectedItems.Count > 0)
                        { foreach (BoardItem item in selectedBoard.SelectedItems) if (!item.BoundToSelectedItem(selectedBoard)) { item.Y--; actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(0, 1))); } }
                        else selectedBoard.ParentControl.AddVScrollbarValue(-navSpeed);
                        break;
                    case Key.Down:
                        if (selectedBoard.SelectedItems.Count > 0)
                        { foreach (BoardItem item in selectedBoard.SelectedItems) if (!item.BoundToSelectedItem(selectedBoard)) { item.Y++; actions.Add(CreateItemUndoMoveAction(item, new XNA.Point(0, -1))); } }
                        else selectedBoard.ParentControl.AddVScrollbarValue(navSpeed);
                        break;
                    case Key.PageUp:
                        selectedBoard.ParentControl.AddVScrollbarValue(-999);
                        break;
                    case Key.PageDown:
                        selectedBoard.ParentControl.AddVScrollbarValue(999);
                        break;
                    case Key.Delete:
                        switch (selectedBoard.Mouse.State)
                        {
                            case MouseState.Selection:
                                bool askedVr = false, askedMm = false;
                                List<BoardItem> selectedItems = selectedBoard.SelectedItems.ToList();
                                foreach (BoardItem item in selectedItems)
                                {
                                    if (item is ToolTipDot || item is MiscDot) continue;
                                    else if (item is VRDot) { if (!askedVr) { askedVr = true; selectedBoard.VRRectangle.RemoveItem(null); } } // TODO: add confirmation dialog
                                    else if (item is MinimapDot) { if (!askedMm) { askedMm = true; selectedBoard.MinimapRectangle.RemoveItem(null); } } // TODO: add confirmation dialog
                                    else item.RemoveItem(actions);
                                }
                                break;
                            case MouseState.RandomTiles:
                            case MouseState.StaticObjectAdding:
                            case MouseState.Chairs:
                            case MouseState.Ropes:
                                parentBoard.InvokeReturnToSelectionState();
                                break;
                            case MouseState.Footholds:
                                while (selectedBoard.Mouse.connectedLines.Count > 0 && selectedBoard.Mouse.connectedLines[0].FirstDot.connectedLines.Count > 0)
                                    selectedBoard.Mouse.connectedLines[0].FirstDot.connectedLines[0].Remove(false, actions);
                                break;
                        }
                        break;
                    case Key.F:
                        if (ctrl) foreach (BoardItem item in selectedBoard.SelectedItems) if (item is IFlippable f) { f.Flip = !f.Flip; actions.Add(UndoRedoManager.ItemFlipped(f)); }
                        break;
                    case Key.Add:
                        foreach (BoardItem item in selectedBoard.SelectedItems) { item.Z += UserSettings.zShift; actions.Add(UndoRedoManager.ItemZChanged(item, item.Z - UserSettings.zShift, item.Z)); }
                        selectedBoard.BoardItems.Sort();
                        break;
                    case Key.Subtract:
                        foreach (BoardItem item in selectedBoard.SelectedItems) { item.Z -= UserSettings.zShift; actions.Add(UndoRedoManager.ItemZChanged(item, item.Z + UserSettings.zShift, item.Z)); }
                        selectedBoard.BoardItems.Sort();
                        break;
                    case Key.A:
                        if (ctrl)
                        {
                            foreach (BoardItem item in selectedBoard.BoardItems.Items)
                                if ((selectedBoard.EditedTypes & item.Type) == item.Type)
                                {
                                    if (item is LayeredItem li) { if (li.CheckIfLayerSelected(selectedBoard.GetUserSelectionInfo())) item.Selected = true; }
                                    else item.Selected = true;
                                }
                        }
                        clearRedo = false;
                        break;
                    case Key.X:
                        if (ctrl && selectedBoard.Mouse.State == MouseState.Selection)
                        {
                            _clipboard[SerializationManager.HaClipboardData] = selectedBoard.SerializationManager.SerializeList(selectedBoard.SelectedItems.Cast<ISerializableSelector>());
                            int idx = 0;
                            while (selectedBoard.SelectedItems.Count > idx)
                            {
                                BoardItem item = selectedBoard.SelectedItems[idx];
                                if (item is ToolTipDot || item is MiscDot || item is VRDot || item is MinimapDot) idx++;
                                else item.RemoveItem(actions);
                            }
                        }
                        break;
                    case Key.C:
                        if (ctrl)
                            _clipboard[SerializationManager.HaClipboardData] = selectedBoard.SerializationManager.SerializeList(selectedBoard.SelectedItems.Cast<ISerializableSelector>());
                        break;
                    case Key.V:
                        if (ctrl && _clipboard.ContainsKey(SerializationManager.HaClipboardData))
                        {
                            List<ISerializable> items;
                            try { items = selectedBoard.SerializationManager.DeserializeList((string)_clipboard[SerializationManager.HaClipboardData]); }
                            catch (SerializationException de) { Debug.WriteLine("[InputHandler] Paste: " + de.Message); return; }
                            catch (Exception e) { Debug.WriteLine("[InputHandler] Paste: " + e); return; }

                            string tS = null;
                            bool needsLayer = false;
                            foreach (ISerializable item in items)
                            {
                                if (item is TileInstance tile)
                                {
                                    string currtS = ((TileInfo)tile.BaseInfo).tS;
                                    if (currtS != tS) { if (tS == null) tS = currtS; else { Debug.WriteLine("[InputHandler] Paste: mixed tile sets"); return; } }
                                }
                                if (item is IContainsLayerInfo) needsLayer = true;
                            }
                            if (needsLayer && (selectedBoard.SelectedLayerIndex < 0 || selectedBoard.SelectedPlatform < 0)) { Debug.WriteLine("[InputHandler] Paste: no layer selected"); return; }
                            if (tS != null && selectedBoard.SelectedLayer.tS != null && tS != selectedBoard.SelectedLayer.tS) { Debug.WriteLine("[InputHandler] Paste: tile set mismatch"); return; }

                            XNA.Point minPos = new XNA.Point(int.MaxValue, int.MaxValue);
                            XNA.Point maxPos = new XNA.Point(int.MinValue, int.MinValue);
                            foreach (ISerializable item in items)
                            {
                                if (item is BoardItem bi) { if (bi.Left < minPos.X) minPos.X = bi.Left; if (bi.Top < minPos.Y) minPos.Y = bi.Top; if (bi.Right > maxPos.X) maxPos.X = bi.Right; if (bi.Bottom > maxPos.Y) maxPos.Y = bi.Bottom; }
                                else if (item is Rope r) { int rx = r.FirstAnchor.X; int minY = Math.Min(r.FirstAnchor.Y, r.SecondAnchor.Y); int maxY = Math.Max(r.FirstAnchor.Y, r.SecondAnchor.Y); if (rx < minPos.X) minPos.X = rx; if (rx > maxPos.X) maxPos.X = rx; if (minY < minPos.Y) minPos.Y = minY; if (maxY > maxPos.Y) maxPos.Y = maxY; }
                            }
                            XNA.Point center = new XNA.Point((maxPos.X + minPos.X) / 2, (maxPos.Y + minPos.Y) / 2);
                            XNA.Point offset = new XNA.Point(selectedBoard.Mouse.X - center.X, selectedBoard.Mouse.Y - center.Y);

                            ClearSelectedItems(selectedBoard);
                            List<UndoRedoAction> undoPipe = new List<UndoRedoAction>();
                            foreach (ISerializable item in items) { item.AddToBoard(undoPipe); item.PostDeserializationActions(true, offset); }
                            selectedBoard.BoardItems.Sort();
                            selectedBoard.UndoRedoMan.AddUndoBatch(undoPipe);
                        }
                        break;
                    case Key.Z:
                        if (ctrl && selectedBoard.UndoRedoMan.UndoList.Count > 0) selectedBoard.UndoRedoMan.Undo();
                        clearRedo = false;
                        break;
                    case Key.Y:
                        if (ctrl && selectedBoard.UndoRedoMan.RedoList.Count > 0) selectedBoard.UndoRedoMan.Redo();
                        clearRedo = false;
                        break;
                    case Key.S:
                        if (ctrl) parentBoard.OnExportRequested();
                        break;
                    case Key.O:
                        if (ctrl) parentBoard.OnLoadRequested();
                        break;
                    case Key.Escape:
                        if (selectedBoard.Mouse.State == MouseState.Selection) { ClearBoundItems(selectedBoard); ClearSelectedItems(selectedBoard); clearRedo = false; }
                        else if (selectedBoard.Mouse.State == MouseState.Footholds) selectedBoard.Mouse.Clear();
                        else parentBoard.InvokeReturnToSelectionState();
                        break;
                    case Key.W:
                        if (ctrl) parentBoard.OnCloseTabRequested();
                        break;
                    case Key.Tab:
                        if (ctrl) parentBoard.OnSwitchTabRequested(shift);
                        break;
                    default:
                        clearRedo = false;
                        break;
                }
                if (actions.Count > 0) selectedBoard.UndoRedoMan.AddUndoBatch(actions);
                if (clearRedo) selectedBoard.UndoRedoMan.RedoList.Clear();
            }
        }

        private bool ClickOnMinimap(Board selectedBoard, XNA.Point position)
        {
            if (selectedBoard.MiniMap == null || !UserSettings.useMiniMap) return false;
            return position.X > 0 && position.X < selectedBoard.MinimapArea.Width && position.Y > 0 && position.Y < selectedBoard.MinimapArea.Height;
        }

        private void parentBoard_MouseDoubleClick(Board selectedBoard, BoardItem target, XNA.Point realPosition, XNA.Point virtualPosition)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (ClickOnMinimap(selectedBoard, realPosition)) return;
                if (target != null)
                {
                    ClearSelectedItems(selectedBoard);
                    target.Selected = true;
                    parentBoard.EditInstanceClicked(target);
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    selectedBoard.Mouse.CreateFhAnchor();
                }
            }
        }

        private void parentBoard_RightMouseClick(Board selectedBoard, BoardItem rightClickTarget, XNA.Point realPosition, XNA.Point virtualPosition, MouseState mouseState)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (mouseState == MouseState.Selection)
                {
                    ClearBoundItems(selectedBoard);
                    if (ClickOnMinimap(selectedBoard, realPosition)) return;
                    if (rightClickTarget == null) return;
                    if (!rightClickTarget.Selected) ClearSelectedItems(selectedBoard);
                    rightClickTarget.Selected = true;
                    BoardItemContextMenu bicm = new BoardItemContextMenu(parentBoard, selectedBoard, rightClickTarget);
                    parentBoard.ShowContextMenuAtPointer(bicm.Menu);
                }
                else parentBoard.InvokeReturnToSelectionState();
            }
        }

        private void parentBoard_LeftMouseUp(Board selectedBoard, BoardItem target, BoardItem selectedTarget, XNA.Point realPosition, XNA.Point virtualPosition, bool selectedItemHigher)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                if (selectedBoard.Mouse.State == MouseState.Selection)
                {
                    ClearBoundItems(selectedBoard);
                }
                else if (selectedBoard.Mouse.State == MouseState.StaticObjectAdding ||
                    selectedBoard.Mouse.State == MouseState.RandomTiles ||
                    selectedBoard.Mouse.State == MouseState.Chairs ||
                    selectedBoard.Mouse.State == MouseState.Ropes ||
                    selectedBoard.Mouse.State == MouseState.Tooltip ||
                    selectedBoard.Mouse.State == MouseState.Clock)
                {
                    selectedBoard.Mouse.PlaceObject();
                }
                else if (selectedBoard.Mouse.State == MouseState.Footholds)
                {
                    selectedBoard.Mouse.TryConnectFoothold();
                }
            }
        }

        private void HandleMinimapBrowse(Board selectedBoard, XNA.Point realPosition)
        {
            int h = realPosition.X * selectedBoard.mag - (int)parentBoard.ActualWidth  / 2;
            int v = realPosition.Y * selectedBoard.mag - (int)parentBoard.ActualHeight / 2;
            selectedBoard.hScroll = h < 0 ? 0 : (h > parentBoard.MaxHScroll ? (int)parentBoard.MaxHScroll : h);
            selectedBoard.vScroll = v < 0 ? 0 : (v > parentBoard.MaxVScroll ? (int)parentBoard.MaxVScroll : v);
        }

        private void parentBoard_LeftMouseDown(Board selectedBoard, BoardItem item, BoardItem selectedItem, XNA.Point realPosition, XNA.Point virtualPosition, bool selectedItemHigher)
        {
            lock (parentBoard)
            {
                OnUserInteraction();
                lastPhysicalPos = realPosition;
                if (ClickOnMinimap(selectedBoard, realPosition) && selectedBoard.Mouse.State == MouseState.Selection)
                {
                    selectedBoard.Mouse.MinimapBrowseOngoing = true;
                    HandleMinimapBrowse(selectedBoard, realPosition);
                }
                else if (selectedBoard.Mouse.State == MouseState.Selection)
                {
                    bool ctrlDown = parentBoard.CtrlHeld;
                    if (item == null && selectedItem == null)
                    {
                        if (!ctrlDown) ClearSelectedItems(selectedBoard);
                        selectedBoard.Mouse.MultiSelectOngoing = true;
                        selectedBoard.Mouse.MultiSelectStart = virtualPosition;
                    }
                    else
                    {
                        BoardItem itemToSelect;
                        bool itemAlreadySelected = false;
                        if (item == null) { itemToSelect = selectedItem; itemAlreadySelected = true; }
                        else if (selectedItem == null) { itemToSelect = item; }
                        else if (!selectedItemHigher) { itemToSelect = item; }
                        else { itemToSelect = selectedItem; itemAlreadySelected = true; }

                        if (!itemAlreadySelected && !ctrlDown) ClearSelectedItems(selectedBoard);
                        if (ctrlDown) { itemToSelect.Selected = !itemToSelect.Selected; }
                        else { itemToSelect.Selected = true; selectedBoard.Mouse.SingleSelectStarting = true; selectedBoard.Mouse.SingleSelectStart = virtualPosition; }
                    }
                }
            }
        }

        private void BindAllSelectedItems(Board selectedBoard) =>
            BindAllSelectedItems(selectedBoard, new XNA.Point(selectedBoard.Mouse.X, selectedBoard.Mouse.Y));

        private void BindAllSelectedItems(Board selectedBoard, XNA.Point mousePosition)
        {
            foreach (BoardItem itemToSelect in selectedBoard.SelectedItems)
            {
                selectedBoard.Mouse.BindItem(itemToSelect, new XNA.Point(itemToSelect.X - mousePosition.X, itemToSelect.Y - mousePosition.Y));
                itemToSelect.moveStartPos = (itemToSelect is BackgroundInstance bi2)
                    ? new XNA.Point(bi2.BaseX, bi2.BaseY)
                    : new XNA.Point(itemToSelect.X, itemToSelect.Y);
            }
        }

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
            lock (board.ParentControl)
            {
                List<UndoRedoAction> undoActions = new List<UndoRedoAction>();
                bool addUndo;
                List<BoardItem> items = board.Mouse.BoundItems.Keys.ToList();
                foreach (BoardItem item in items)
                {
                    addUndo = item.tempParent == null || !(item.tempParent.Parent is Mouse);
                    board.Mouse.ReleaseItem(item);
                    if (addUndo)
                    {
                        if (item is BackgroundInstance bi && (bi.BaseX != item.moveStartPos.X || bi.BaseY != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.BackgroundMoved(bi, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(bi.BaseX, bi.BaseY)));
                        else if (!(item is BackgroundInstance) && (item.X != item.moveStartPos.X || item.Y != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.ItemMoved(item, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(item.X, item.Y)));
                    }
                }
                if (undoActions.Count > 0) board.UndoRedoMan.AddUndoBatch(undoActions);
            }
        }
    }
}
