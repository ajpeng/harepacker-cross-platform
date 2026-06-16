/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */

using HaCreator.MapEditor.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        public void SetHScrollbarValue(int value) { }
        public void SetVScrollbarValue(int value) { }
        public void AdjustScrollBars() { }
        public void OnMinimapStateChanged(Board board, bool hasMm) { }
        public void OnSelectedItemChanged(BoardItem? selectedItem) { }
        public void LayerTSChanged(Layer layer) { OnLayerTSChanged?.Invoke(layer); }

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
    }
}
