using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using HaCreator.Collections;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance.Shapes;
using HaSharedLibrary.Util;
using System.Runtime.CompilerServices;
using HaCreator.MapEditor.Instance;
using SkiaSharp;
using System.Linq;

namespace HaCreator.MapEditor
{
    public class Board
    {
        private Point mapSize;
        private Rectangle minimapArea;
        //private Point maxMapSize;
        private Point centerPoint;
        private readonly BoardItemsManager boardItems;
        private readonly List<Layer> mapLayers = new List<Layer>();
        private readonly List<BoardItem> selected = new List<BoardItem>();
        private MultiBoard parent;
        private readonly Mouse mouse;
        private MapInfo mapInfo = new MapInfo();
        private bool bIsNewMapDesign = false; // determines if this board is a new map design or editing an existing map.
        private SKBitmap? miniMap;
        private System.Drawing.Point miniMapPos;
        private Texture2D? miniMapTexture;

        // App settings
        private int selectedLayerIndex = ApplicationSettings.lastDefaultLayer;
        private int selectedPlatform = 0;
        private bool selectedAllLayers = ApplicationSettings.lastAllLayers;
        private bool selectedAllPlats = true;
        private int _hScroll = 0;
        private int _vScroll = 0;
        private int _mag = 16;
        private float _zoom = 1.0f;

        // Zoom limits
        public const float MinZoom = 0.1f;
        public const float MaxZoom = 4.0f;
        public const float ZoomStep = 0.1f;
        private readonly UndoRedoManager undoRedoMan;
        private ItemTypes visibleTypes;
        private ItemTypes editedTypes;
        private bool loading = false;
        private VRRectangle vrRect = null;
        private MinimapRectangle mmRect = null;
        private object? menu = null;
        private readonly SerializationManager? serMan = null;
        private object? page = null;
        private bool dirty;
        private readonly int uid;

        private static int uidCounter = 0;

        // Cached portal connection pairs for efficient rendering
        private List<(PortalInstance, PortalInstance)> _cachedPortalPairs = null;
        private int _cachedPortalCount = -1;

        public ItemTypes VisibleTypes { get { return visibleTypes; } set { visibleTypes = value; } }
        public ItemTypes EditedTypes { get { return editedTypes; } set { editedTypes = value; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mapSize"></param>
        /// <param name="centerPoint"></param>
        /// <param name="parent"></param>
        /// <param name="bIsNewMapDesign">Determines if this board is a new map design or editing an existing map.</param>
        /// <param name="menu"></param>
        /// <param name="visibleTypes"></param>
        /// <param name="editedTypes"></param>
        public Board(Point mapSize, Point centerPoint, MultiBoard parent, bool bIsNewMapDesign, object? menu, ItemTypes visibleTypes, ItemTypes editedTypes)
        {
            this.uid = Interlocked.Increment(ref uidCounter);
            this.MapSize = mapSize;
            this.centerPoint = centerPoint;
            this.parent = parent;
            this.bIsNewMapDesign = bIsNewMapDesign;
            this.visibleTypes = visibleTypes;
            this.editedTypes = editedTypes;
            this.menu = menu;

            boardItems = new BoardItemsManager(this);
            undoRedoMan = new UndoRedoManager(this);
            mouse = new Mouse(this);
            serMan = new SerializationManager(this);
        }

        public static SKBitmap ResizeImage(SKBitmap source, float coeff)
        {
            int w = (int)Math.Round(source.Width / coeff);
            int h = (int)Math.Round(source.Height / coeff);
            SKBitmap result = new SKBitmap(w, h, source.ColorType, source.AlphaType);
            using SKCanvas canvas = new SKCanvas(result);
            canvas.DrawBitmap(source, new SKRect(0, 0, w, h));
            return result;
        }

        public static SKBitmap CropImage(SKBitmap img, System.Drawing.Rectangle selection)
        {
            SKBitmap result = new SKBitmap(selection.Width, selection.Height, img.ColorType, img.AlphaType);
            using SKCanvas canvas = new SKCanvas(result);
            canvas.DrawBitmap(img, new SKRect(selection.X, selection.Y, selection.X + selection.Width, selection.Y + selection.Height),
                              new SKRect(0, 0, selection.Width, selection.Height));
            return result;
        }


        /// <summary>
        /// Re-generates the minimap image from the board
        /// </summary>
        /// <returns></returns>
        public bool RegenerateMinimap()
        {
            try
            {
                lock (parent)
                {
                    if (MinimapRectangle == null)
                    {
                        MiniMap = null;
                    }
                    else
                    {
                        SKBitmap bmp = new SKBitmap(mapSize.X, mapSize.Y, SKColorType.Bgra8888, SKAlphaType.Premul);
                        using (SKCanvas canvas = new SKCanvas(bmp))
                        {
                            canvas.Clear(SKColors.Transparent);
                            foreach (BoardItem item in BoardItems.TileObjs)
                            {
                                if (item.Image == null) continue;
                                int dx = item.X + centerPoint.X - item.Origin.X;
                                int dy = item.Y + centerPoint.Y - item.Origin.Y;
                                SKRect dest = new SKRect(dx, dy, dx + item.Image.Width, dy + item.Image.Height);
                                if (item.IsFlipped())
                                {
                                    canvas.Save();
                                    canvas.Scale(-1, 1, dx + item.Image.Width / 2f, 0);
                                    canvas.DrawBitmap(item.Image, dest);
                                    canvas.Restore();
                                }
                                else
                                {
                                    canvas.DrawBitmap(item.Image, dest);
                                }
                            }
                        }
                        bmp = CropImage(bmp, new System.Drawing.Rectangle(MinimapRectangle.X + centerPoint.X, MinimapRectangle.Y + centerPoint.Y, MinimapRectangle.Width, MinimapRectangle.Height));
                        MiniMap = ResizeImage(bmp, (float)_mag);
                        MinimapPosition = new System.Drawing.Point(MinimapRectangle.X, MinimapRectangle.Y);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RenderBoard(SpriteBatch sprite)
        {
            if (mapInfo == null) 
                return;
            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render the object lists
            foreach (IMapleList list in boardItems.AllItemLists)
            {
                RenderList(list, sprite, xShift, yShift, sel);
            }

            // Render the user's selection square
            if (mouse.MultiSelectOngoing)
            {
                Rectangle selectionRect = InputHandler.CreateRectangle(
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.X, centerPoint.X, hScroll, 0), 
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.Y, centerPoint.Y, vScroll, 0)),
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.X, centerPoint.X, hScroll, 0), 
                        MultiBoard.VirtualToPhysical(mouse.Y, centerPoint.Y, vScroll, 0)));
                parent.DrawRectangle(sprite, selectionRect, UserSettings.SelectSquare);
                selectionRect.X++;
                selectionRect.Y++;
                selectionRect.Width--;
                selectionRect.Height--;
                parent.FillRectangle(sprite, selectionRect, UserSettings.SelectSquareFill);
            }
            
            // Render VR if it exists
            if (VRRectangle != null && (sel.visibleTypes & VRRectangle.Type) != 0)
            {
                VRRectangle.Draw(sprite, xShift, yShift, sel);
            }
            // Render minimap rectangle
            if (MinimapRectangle != null && (sel.visibleTypes & MinimapRectangle.Type) != 0)
            {
                MinimapRectangle.Draw(sprite, xShift, yShift, sel);
            }

            // Render center point if InfoMode on
            if (ApplicationSettings.InfoMode)
            {
                parent.FillRectangle(sprite, new Rectangle(MultiBoard.VirtualToPhysical(-5, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(-5 , centerPoint.Y, vScroll, 0), 10, 10), Color.DarkRed);
            }
        }

        /// <summary>
        /// Renders backgrounds separately. This should be called with a sprite batch
        /// that does NOT have the zoom transform applied, so backgrounds stay at a fixed
        /// screen position regardless of viewport zoom level.
        /// </summary>
        public void RenderBackgrounds(SpriteBatch sprite)
        {
            if (mapInfo == null)
                return;

            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render back backgrounds
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.BackBackgrounds)
                {
                    bg.Draw(sprite, bg.GetColor(sel, bg.Selected), xShift, yShift);
                }
            }
        }

        /// <summary>
        /// Renders front backgrounds separately. This should be called after other items
        /// but without the zoom transform.
        /// </summary>
        public void RenderFrontBackgrounds(SpriteBatch sprite)
        {
            if (mapInfo == null)
                return;

            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render front backgrounds
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.FrontBackgrounds)
                {
                    bg.Draw(sprite, bg.GetColor(sel, bg.Selected), xShift, yShift);
                }
            }
        }

        /// <summary>
        /// Renders the minimap overlay. This should be called with a separate sprite batch
        /// that does NOT have the zoom transform applied, so the minimap stays at a fixed
        /// screen size regardless of viewport zoom level.
        /// </summary>
        public void RenderMinimap(SpriteBatch sprite)
        {
            if (miniMap == null || !UserSettings.useMiniMap)
                return;

            // Area for the image itself
            Rectangle minimapImageArea = new Rectangle(
                (miniMapPos.X + centerPoint.X) / _mag,
                (miniMapPos.Y + centerPoint.Y) / _mag,
                miniMap.Width,
                miniMap.Height);

            // Render gray area
            parent.FillRectangle(sprite, minimapArea, Color.Gray);

            // Render minimap
            if (miniMapTexture == null && miniMap != null && parent.GraphicsDevice != null)
                miniMapTexture = miniMap.ToTexture2D(parent.GraphicsDevice);

            sprite.Draw(miniMapTexture, minimapImageArea, null, Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0.99999f);

            // Render current location on minimap
            // Account for zoom: when zoomed in, viewport shows less virtual space
            int viewportWidth = (int)(parent.CurrentDXWindowSize.Width / _zoom);
            int viewportHeight = (int)(parent.CurrentDXWindowSize.Height / _zoom);
            parent.DrawRectangle(sprite, new Rectangle(
                hScroll / _mag,
                vScroll / _mag,
                viewportWidth / _mag,
                viewportHeight / _mag), Color.Blue);

            // Render minimap borders
            parent.DrawRectangle(sprite, minimapImageArea, Color.Black);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderList(IMapleList list, SpriteBatch sprite, int xShift, int yShift, SelectionInfo sel)
        {
            // Skip backgrounds - they are rendered separately without zoom transform
            if (list.ListType == ItemTypes.Backgrounds)
                return;

            if (list.ListType == ItemTypes.None)
            {
                foreach (BoardItem item in list)
                {
                    if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y) && ((sel.visibleTypes & item.Type) != 0))
                        item.Draw(sprite, item.GetColor(sel, item.Selected), xShift, yShift);
                }
            }
            else if ((sel.visibleTypes & list.ListType) != 0)
            {
                if (list.IsItem)
                {
                    foreach (BoardItem item in list)
                    {
                        if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y))
                        {
                            item.Draw(sprite, item.GetColor(sel, item.Selected), xShift, yShift);
                        }
                    }

                    // Render lines between local teleport portal (using cached pairs)
                    if (list.ListType == ItemTypes.Portals)
                    {
                        Color portalLineColor = (sel.editedTypes & ItemTypes.Portals) == ItemTypes.Portals ? Color.LightBlue : MultiBoard.InactiveColor;

                        foreach (var (portal1, portal2) in GetPortalConnectionPairs())
                        {
                            // Calculate screen positions
                            int x1 = MultiBoard.VirtualToPhysical(portal1.X, centerPoint.X, hScroll, 0);
                            int y1 = MultiBoard.VirtualToPhysical(portal1.Y, centerPoint.Y, vScroll, 0);
                            int x2 = MultiBoard.VirtualToPhysical(portal2.X, centerPoint.X, hScroll, 0);
                            int y2 = MultiBoard.VirtualToPhysical(portal2.Y, centerPoint.Y, vScroll, 0);

                            // Draw the line
                            parent.DrawLine(sprite,
                                new Vector2(x1, y1),
                                new Vector2(x2, y2),
                                portalLineColor);
                        }
                    }
                }
                else
                {
                    foreach (MapleLine line in list)
                    {
                        if (parent.IsItemInRange(Math.Min(line.FirstDot.X, line.SecondDot.X), Math.Min(line.FirstDot.Y, line.SecondDot.Y), Math.Abs(line.FirstDot.X - line.SecondDot.X), Math.Abs(line.FirstDot.Y - line.SecondDot.Y), xShift, yShift))
                            line.Draw(sprite, line.GetColor(sel), xShift, yShift);
                    }
                }
            }
        }

        /// <summary>
        /// Gets cached portal connection pairs for local teleport portals.
        /// Rebuilds cache if portals have changed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<(PortalInstance, PortalInstance)> GetPortalConnectionPairs()
        {
            int currentCount = BoardItems.Portals.Count;

            // Rebuild cache if portal count changed or cache doesn't exist
            if (_cachedPortalPairs == null || _cachedPortalCount != currentCount)
            {
                _cachedPortalPairs = new List<(PortalInstance, PortalInstance)>();
                _cachedPortalCount = currentCount;

                // Build lookup dictionary for O(1) portal name lookups
                var portalsByName = new Dictionary<string, PortalInstance>();
                var localTeleportPortals = new List<PortalInstance>();

                foreach (var portal in BoardItems.Portals)
                {
                    if (portal.pt == PortalType.Hidden || portal.pt == PortalType.Invisible)
                    {
                        localTeleportPortals.Add(portal);
                    }
                    // Store all portals by name for target lookup
                    if (!string.IsNullOrEmpty(portal.pn) && !portalsByName.ContainsKey(portal.pn))
                    {
                        portalsByName[portal.pn] = portal;
                    }
                }

                // Build pairs using HashSet to avoid duplicates
                var processedPairs = new HashSet<(string, string)>();
                foreach (var portal1 in localTeleportPortals)
                {
                    if (string.IsNullOrEmpty(portal1.tn) || !portalsByName.TryGetValue(portal1.tn, out var portal2))
                        continue;
                    if (portal1 == portal2)
                        continue;

                    // Create unique pair identifier
                    var pair = string.CompareOrdinal(portal1.pn, portal2.pn) < 0
                        ? (portal1.pn, portal2.pn)
                        : (portal2.pn, portal1.pn);

                    if (!processedPairs.Contains(pair))
                    {
                        processedPairs.Add(pair);
                        _cachedPortalPairs.Add((portal1, portal2));
                    }
                }
            }

            return _cachedPortalPairs;
        }

        /// <summary>
        /// Invalidates the cached portal pairs. Call when portal pn/tn properties change.
        /// </summary>
        public void InvalidatePortalPairCache()
        {
            _cachedPortalPairs = null;
            _cachedPortalCount = -1;
        }

        // ── SkiaSharp render path ──────────────────────────────────────────

        public void RenderBackgroundsSK(SKCanvas canvas)
        {
            if (mapInfo == null) return;
            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.BackBackgrounds)
                    bg.DrawSK(canvas, bg.GetColor(sel, bg.Selected), xShift, yShift);
            }
        }

        public void RenderFrontBackgroundsSK(SKCanvas canvas)
        {
            if (mapInfo == null) return;
            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.FrontBackgrounds)
                    bg.DrawSK(canvas, bg.GetColor(sel, bg.Selected), xShift, yShift);
            }
        }

        public void RenderBoardSK(SKCanvas canvas)
        {
            if (mapInfo == null) return;
            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            foreach (IMapleList list in boardItems.AllItemLists)
                RenderListSK(list, canvas, xShift, yShift, sel);

            if (mouse.MultiSelectOngoing)
            {
                Rectangle selectionRect = InputHandler.CreateRectangle(
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.X, centerPoint.X, hScroll, 0),
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.Y, centerPoint.Y, vScroll, 0)),
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.X, centerPoint.X, hScroll, 0),
                        MultiBoard.VirtualToPhysical(mouse.Y, centerPoint.Y, vScroll, 0)));
                MultiBoard.DrawRectangleSK(canvas, selectionRect.X, selectionRect.Y, selectionRect.Width, selectionRect.Height, UserSettings.SelectSquare);
                MultiBoard.FillRectangleSK(canvas, selectionRect.X + 1, selectionRect.Y + 1, selectionRect.Width - 1, selectionRect.Height - 1, UserSettings.SelectSquareFill);
            }

            if (VRRectangle != null && (sel.visibleTypes & VRRectangle.Type) != 0)
                VRRectangle.DrawSK(canvas, xShift, yShift, sel);
            if (MinimapRectangle != null && (sel.visibleTypes & MinimapRectangle.Type) != 0)
                MinimapRectangle.DrawSK(canvas, xShift, yShift, sel);

            if (ApplicationSettings.InfoMode)
            {
                int cx = MultiBoard.VirtualToPhysical(-5, centerPoint.X, hScroll, 0);
                int cy = MultiBoard.VirtualToPhysical(-5, centerPoint.Y, vScroll, 0);
                MultiBoard.FillRectangleSK(canvas, cx, cy, 10, 10, Color.DarkRed);
            }
        }

        public void RenderMinimapSK(SKCanvas canvas)
        {
            if (miniMap == null || !UserSettings.useMiniMap) return;

            Rectangle minimapImageArea = new Rectangle(
                (miniMapPos.X + centerPoint.X) / _mag,
                (miniMapPos.Y + centerPoint.Y) / _mag,
                miniMap.Width,
                miniMap.Height);

            MultiBoard.FillRectangleSK(canvas, minimapArea.X, minimapArea.Y, minimapArea.Width, minimapArea.Height, Color.Gray);

            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawBitmap(miniMap,
                SKRect.Create(minimapImageArea.X, minimapImageArea.Y, minimapImageArea.Width, minimapImageArea.Height),
                paint);

            int viewportWidth  = (int)(parent.CurrentDXWindowSize.Width  / _zoom);
            int viewportHeight = (int)(parent.CurrentDXWindowSize.Height / _zoom);
            MultiBoard.DrawRectangleSK(canvas, hScroll / _mag, vScroll / _mag, viewportWidth / _mag, viewportHeight / _mag, Color.Blue);
            MultiBoard.DrawRectangleSK(canvas, minimapImageArea.X, minimapImageArea.Y, minimapImageArea.Width, minimapImageArea.Height, Color.Black);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderListSK(IMapleList list, SKCanvas canvas, int xShift, int yShift, SelectionInfo sel)
        {
            if (list.ListType == ItemTypes.Backgrounds)
                return;

            if (list.ListType == ItemTypes.None)
            {
                foreach (BoardItem item in list)
                {
                    if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y) && ((sel.visibleTypes & item.Type) != 0))
                        item.DrawSK(canvas, item.GetColor(sel, item.Selected), xShift, yShift);
                }
            }
            else if ((sel.visibleTypes & list.ListType) != 0)
            {
                if (list.IsItem)
                {
                    foreach (BoardItem item in list)
                    {
                        if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y))
                            item.DrawSK(canvas, item.GetColor(sel, item.Selected), xShift, yShift);
                    }

                    if (list.ListType == ItemTypes.Portals)
                    {
                        Color portalLineColor = (sel.editedTypes & ItemTypes.Portals) == ItemTypes.Portals ? Color.LightBlue : MultiBoard.InactiveColor;
                        foreach (var (p1, p2) in GetPortalConnectionPairs())
                        {
                            int x1 = MultiBoard.VirtualToPhysical(p1.X, centerPoint.X, hScroll, 0);
                            int y1 = MultiBoard.VirtualToPhysical(p1.Y, centerPoint.Y, vScroll, 0);
                            int x2 = MultiBoard.VirtualToPhysical(p2.X, centerPoint.X, hScroll, 0);
                            int y2 = MultiBoard.VirtualToPhysical(p2.Y, centerPoint.Y, vScroll, 0);
                            MultiBoard.DrawLineSK(canvas, x1, y1, x2, y2, portalLineColor);
                        }
                    }
                }
                else
                {
                    foreach (MapleLine line in list)
                    {
                        if (parent.IsItemInRange(Math.Min(line.FirstDot.X, line.SecondDot.X), Math.Min(line.FirstDot.Y, line.SecondDot.Y), Math.Abs(line.FirstDot.X - line.SecondDot.X), Math.Abs(line.FirstDot.Y - line.SecondDot.Y), xShift, yShift))
                            line.DrawSK(canvas, line.GetColor(sel), xShift, yShift);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (parent)
            {
                parent.Boards.Remove(this);
                boardItems.Clear();
                selected.Clear();
                mapLayers.Clear();
            }
            // This must be called when MultiBoard is unlocked, to prevent BackupManager deadlocking
            parent.OnBoardRemoved(this);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #region Properties
        public int UniqueID
        {
            get { return uid; }
        }

        public bool Dirty
        {
            get { return dirty; }
            set { dirty = value; }
        }

        public UndoRedoManager UndoRedoMan
        {
            get { return undoRedoMan; }
        }

        public int mag
        {
            get { return _mag; }
            set { lock (parent) { _mag = value; } }
        }

        /// <summary>
        /// Zoom level for the main viewport (1.0 = 100%, 0.5 = 50%, 2.0 = 200%)
        /// </summary>
        public float Zoom
        {
            get { return _zoom; }
            set
            {
                lock (parent)
                {
                    _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
                    parent.AdjustScrollBars();
                }
            }
        }

        /// <summary>
        /// Zoom in by one step
        /// </summary>
        public void ZoomIn()
        {
            Zoom = _zoom + ZoomStep;
        }

        /// <summary>
        /// Zoom out by one step
        /// </summary>
        public void ZoomOut()
        {
            Zoom = _zoom - ZoomStep;
        }

        /// <summary>
        /// Reset zoom to 100%
        /// </summary>
        public void ResetZoom()
        {
            Zoom = 1.0f;
        }

        public MapInfo MapInfo
        {
            get { return mapInfo; }
            set 
            { 
                lock (parent) 
                { 
                    mapInfo = value; 
                } 
            }
        }

        /// <summary>
        /// Determines if this board is a new map design or editing an existing map.
        /// </summary>
        public bool IsNewMapDesign { 
            get { return bIsNewMapDesign; }
            private set { }
        }


        public SKBitmap? MiniMap
        {
            get { return miniMap; }
            set { lock (parent) { miniMap = value; miniMapTexture = null; } }
        }

        public System.Drawing.Point MinimapPosition
        {
            get { return miniMapPos; }
            set { miniMapPos = value; }
        }

        public int hScroll
        {
            get { return _hScroll; }
            set { lock (parent) { _hScroll = value; } }
        }

        public Point CenterPoint
        {
            get { return centerPoint; }
            internal set { centerPoint = value; }
        }

        public int vScroll
        {
            get { return _vScroll; }
            set { lock (parent) { _vScroll = value; } }
        }

        public MultiBoard ParentControl
        {
            get
            {
                return parent;
            }
            internal set
            {
                parent = value;
            }
        }

        public Mouse Mouse
        {
            get { return mouse; }
        }

        public Point MapSize
        {
            get
            {
                return mapSize;
            }
            set
            {
                mapSize = value;
                minimapArea = new Rectangle(0, 0, mapSize.X / _mag, mapSize.Y / _mag);
            }
        }

        public Rectangle MinimapArea
        {
            get { return minimapArea; }
        }

        public VRRectangle VRRectangle
        {
            get { return vrRect; }
            set
            {
                vrRect = value;
            }
        }

        public MinimapRectangle MinimapRectangle
        {
            get { return mmRect; }
            set
            {
                mmRect = value;
                parent.OnMinimapStateChanged(this, mmRect != null);
            }
        }

        public BoardItemsManager BoardItems
        {
            get
            {
                return boardItems;
            }
        }

        public List<BoardItem> SelectedItems
        {
            get
            {
                return selected;
            }
        }

        /// <summary>
        /// Map layers
        /// </summary>
        public void CreateMapLayers()
        {
            for (int i = 0; i <= MapConstants.MaxMapLayers; i++)
            {
                AddMapLayer(new Layer(this));
            }
        }
        
        public void AddMapLayer(Layer layer)
        {
            lock (parent)
                mapLayers.Add(layer);
        }

        /// <summary>
        /// Gets the map layers
        /// </summary>
        public ReadOnlyCollection<Layer> Layers
        {
            get
            {
                return mapLayers.AsReadOnly();
            }
        }

        public int SelectedLayerIndex
        {
            get
            {
                return selectedLayerIndex;
            }
            set
            {
                lock (parent)
                {
                    selectedLayerIndex = value;
                }
            }
        }

        public bool SelectedAllLayers
        {
            get { return selectedAllLayers; }
            set { selectedAllLayers = value; }
        }

        public object? Menu
        {
            get { return menu; }
        }

        public Layer SelectedLayer
        {
            get { return Layers[SelectedLayerIndex]; }
        }

        public int SelectedPlatform
        {
            get { return selectedPlatform; }
            set { selectedPlatform = value; }
        }

        public bool SelectedAllPlatforms
        {
            get { return selectedAllPlats; }
            set { selectedAllPlats = value; }
        }

        public SelectionInfo GetUserSelectionInfo()
        {
            return new SelectionInfo(selectedAllLayers ? -1 : selectedLayerIndex, selectedAllPlats ? -1 : selectedPlatform, visibleTypes, editedTypes);
        }

        public bool Loading { get { return loading; } set { loading = value; } }

        public SerializationManager SerializationManager
        {
            get { return serMan; }
        }

        public object? TabPage
        {
            get { return page; }
            set { page = value; }
        }
        #endregion

        #region Foothold Utilities
        /// <summary>
        /// Finds the foothold below the given position.
        /// Used by MapSimulator and AI MapEditor for placing entities on platforms.
        /// </summary>
        /// <param name="x">X coordinate to check</param>
        /// <param name="y">Y coordinate to check</param>
        /// <param name="searchRange">Maximum distance below to search (default 500)</param>
        /// <param name="upwardTolerance">Allow finding footholds slightly above (default 10)</param>
        /// <returns>The foothold below, or null if none found</returns>
        public FootholdLine FindFootholdBelow(float x, float y, float searchRange = 500f, float upwardTolerance = 10f)
        {
            var footholds = BoardItems.FootholdLines;
            if (footholds == null || footholds.Count == 0)
                return null;

            FootholdLine bestFh = null;
            float bestDist = float.MaxValue;

            foreach (var fh in footholds)
            {
                // Skip walls (vertical footholds)
                if (fh.IsWall)
                    continue;

                // Check if X is within foothold range
                float fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                float fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                if (x < fhMinX || x > fhMaxX)
                    continue;

                // Calculate Y at X position on this foothold (linear interpolation for slopes)
                float dx = fh.SecondDot.X - fh.FirstDot.X;
                float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                float fhY = fh.FirstDot.Y + t * dy;

                // Check if foothold is within range (below or slightly above)
                // dist > 0 means foothold is below, dist < 0 means foothold is above
                float dist = fhY - y;
                float absDist = Math.Abs(dist);

                // Accept footholds below (within searchRange) or slightly above (within tolerance)
                if ((dist >= 0 && dist < searchRange) || (dist < 0 && -dist <= upwardTolerance))
                {
                    if (absDist < bestDist)
                    {
                        bestDist = absDist;
                        bestFh = fh;
                    }
                }
            }

            return bestFh;
        }

        /// <summary>
        /// Calculates the Y coordinate on a foothold at a given X position.
        /// </summary>
        public static float CalculateYOnFoothold(FootholdLine fh, float x)
        {
            float dx = fh.SecondDot.X - fh.FirstDot.X;
            float dy = fh.SecondDot.Y - fh.FirstDot.Y;
            float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
            return fh.FirstDot.Y + t * dy;
        }
        #endregion
    }
}
