/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
// Minimal stubs for WinForms editor panel UserControls — pending Avalonia port.
using HaCreator.Wz;

namespace HaCreator.MapEditor
{
    public class TilePanel
    {
        public void LoadTileSetList() { }
        public void SetSelectedTileSet(string tileSet) { }
        public void SubscribeToHotSwap(HotSwapRefreshService svc) { }
    }

    public class ObjPanel
    {
        public void OnL1Changed(string l1) { }
        public void SubscribeToHotSwap(HotSwapRefreshService svc) { }
    }

    public class BackgroundPanel
    {
        public void SubscribeToHotSwap(HotSwapRefreshService svc) { }
    }

    public class LifePanel
    {
        public void SubscribeToHotSwap(HotSwapRefreshService svc) { }
    }

    public class BlackBorderPanel
    {
        public void UpdateBoardData() { }
    }

    public class ObjectViewerPanel
    {
        public void OnBoardChanged(Board board) { }
    }
}
