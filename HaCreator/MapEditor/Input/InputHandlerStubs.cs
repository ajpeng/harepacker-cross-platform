/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Input
{
    /// <summary>
    /// Static utility methods extracted from the excluded WinForms InputHandler.cs.
    /// Event-driven input handling will be re-implemented with Avalonia.
    /// </summary>
    public class InputHandler
    {
        public static XNA.Rectangle CreateRectangle(XNA.Point a, XNA.Point b)
        {
            int left, right, top, bottom;
            if (a.X < b.X) { left = a.X; right = b.X; }
            else { left = b.X; right = a.X; }
            if (a.Y < b.Y) { top = a.Y; bottom = b.Y; }
            else { top = b.Y; bottom = a.Y; }
            return new XNA.Rectangle(left, top, right - left, bottom - top);
        }

        public static double Distance(double x, double y)
            => Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

        public static bool IsShiftDown() => false;

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
                        if ((item is BackgroundInstance) && (((BackgroundInstance)item).BaseX != item.moveStartPos.X || ((BackgroundInstance)item).BaseY != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.BackgroundMoved((BackgroundInstance)item, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(((BackgroundInstance)item).BaseX, ((BackgroundInstance)item).BaseY)));
                        else if (!(item is BackgroundInstance) && (item.X != item.moveStartPos.X || item.Y != item.moveStartPos.Y))
                            undoActions.Add(UndoRedoManager.ItemMoved(item, new XNA.Point(item.moveStartPos.X, item.moveStartPos.Y), new XNA.Point(item.X, item.Y)));
                    }
                }
                if (undoActions.Count > 0)
                    board.UndoRedoMan.AddUndoBatch(undoActions);
            }
        }
    }
}
