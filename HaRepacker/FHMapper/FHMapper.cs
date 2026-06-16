using Footholds;
using MapleLib; // WzDataReader extension methods (ReadString, ReadValue)
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing; // System.Drawing.Primitives (cross-platform): Point, PointF, Size, Rectangle
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls.ApplicationLifetimes;
using HaRepacker.GUI.Panels;
using HaRepacker.Models;
using MapleLib.Configuration;

namespace HaRepacker.FHMapper
{
    public class FHMapper
    {
        public static string SettingsPath = Path.Combine(ConfigurationManager.GetLocalFolderPath(), "Settings.ini");
        public List<object> settings = new List<object>();
        private readonly MainPanel _mainPanel;
        private WzNode? _node;
        private readonly List<DisplayMapWindow> _openDisplayMaps = new();

        public FHMapper(MainPanel mainPanel)
        {
            _mainPanel = mainPanel;
        }

        private static Avalonia.Controls.Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        private static void DrawTextAt(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
        {
            font.GetFontMetrics(out var metrics);
            canvas.DrawText(text, x, y - metrics.Ascent, font, paint);
        }

        private static SKBitmap FlipHorizontal(SKBitmap source)
        {
            var flipped = new SKBitmap(source.Width, source.Height);
            using var c = new SKCanvas(flipped);
            c.Translate(source.Width, 0);
            c.Scale(-1, 1);
            c.DrawBitmap(source, 0, 0);
            return flipped;
        }

        private static void SaveBitmap(SKBitmap bitmap, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var img = SKImage.FromBitmap(bitmap);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }

        #region Renders
        private SKBitmap RenderMinimap(Size bmpSize, WzFile wzFile, WzImage img, string mapIdName, WzSubProperty? miniMapSubProperty)
        {
            var minimapRender = new SKBitmap(400, 200);
            using var drawBuf = new SKCanvas(minimapRender);
            drawBuf.Clear(SKColors.White);

            using var blackPaint = new SKPaint { Color = SKColors.Black };
            using var fontMapId = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 20);
            using var fontMinimap = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 18);

            // Map mark
            if (img["info"]?["mapMark"] is WzStringProperty mapMark)
            {
                string mapMarkPath = wzFile.WzDirectory.Name + "/MapHelper.img/mark/" + mapMark.GetString();
                if (wzFile.GetObjectFromPath(mapMarkPath) is WzCanvasProperty markCanvas && mapMark.ToString() != "None")
                    drawBuf.DrawBitmap(markCanvas.GetLinkedWzCanvasBitmap(), 10, 10);
            }

            // Map name lookup
            string mapName = string.Empty;
            string streetName = string.Empty;
            if (WzFile.GetObjectFromMultipleWzFilePath("String.wz/Map.img", Program.WzFileManager.WzFileList) is WzImage mapNameImages)
            {
                foreach (WzSubProperty area in mapNameImages.WzProperties)
                {
                    foreach (WzSubProperty mapImg in area.WzProperties)
                    {
                        if (mapImg.Name == mapIdName)
                        {
                            mapName = mapImg["mapName"].ReadString(string.Empty);
                            streetName = mapImg["streetName"].ReadString(string.Empty);
                            break;
                        }
                    }
                }
            }

            DrawTextAt(drawBuf, $"[{mapIdName}] {streetName}", 60, 10, fontMapId, blackPaint);
            DrawTextAt(drawBuf, mapName, 60, 34, fontMapId, blackPaint);

            if (miniMapSubProperty?["canvas"] is WzCanvasProperty minimapCanvas)
                drawBuf.DrawBitmap(minimapCanvas.GetLinkedWzCanvasBitmap(), 10, 80);
            else
                DrawTextAt(drawBuf, "Minimap not available", 10, 45, fontMinimap, blackPaint);

            return minimapRender;
        }
        #endregion

        public bool TryRenderMapAndSave(WzImage img, double zoom, ref List<string> errorList)
        {
            string mapIdName = img.Name.Substring(0, img.Name.Length - 4);
            _node = _mainPanel.SelectedNode;
            WzFile wzFile = img.WzFileParent;

            var MSPs = new List<SpawnPoint.Spawnpoint>();
            var FHs = new List<FootHold.Foothold>();
            var Ps = new List<Portals.Portal>();

            var miniMapSub = img["miniMap"] as WzSubProperty;
            Size bmpSize;
            Point center;

            try
            {
                bmpSize = new Size(((WzIntProperty)miniMapSub!["width"]).Value, ((WzIntProperty)miniMapSub["height"]).Value);
                center = new Point(((WzIntProperty)miniMapSub["centerX"]).Value, ((WzIntProperty)miniMapSub["centerY"]).Value);
            }
            catch (Exception exp)
            {
                if (exp is KeyNotFoundException || exp is NullReferenceException)
                {
                    try
                    {
                        var info = (WzSubProperty)img["info"];
                        bmpSize = new Size(
                            ((WzIntProperty)info["VRRight"]).Value - ((WzIntProperty)info["VRLeft"]).Value,
                            ((WzIntProperty)info["VRBottom"]).Value - ((WzIntProperty)info["VRTop"]).Value);
                        center = new Point(((WzIntProperty)info["VRRight"]).Value, ((WzIntProperty)info["VRBottom"]).Value);
                    }
                    catch
                    {
                        errorList.Add("Missing map info. Need miniMap/width,height,centerX,centerY OR info/VRRight,VRLeft,VRBottom,VRTop");
                        return false;
                    }
                }
                else return false;
            }

            string renderDir = Path.Combine("Renders", mapIdName);

            using var fontPortal = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 8);
            using var fontTooltip = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 9);
            using var blackPaint = new SKPaint { Color = SKColors.Black };
            using var redPaint = new SKPaint { Color = SKColors.Red };
            using var strokePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

            // Minimap
            using var minimapRender = RenderMinimap(bmpSize, wzFile, img, mapIdName, miniMapSub);
            SaveBitmap(minimapRender, Path.Combine(renderDir, mapIdName + "_miniMapRender.png"));

            // Map (portals, life, footholds)
            var mapRender = new SKBitmap(bmpSize.Width, bmpSize.Height);
            using (var drawBuf = new SKCanvas(mapRender))
            {
                drawBuf.Clear(SKColors.Transparent);

                // Portals
                if (img["portal"] is WzSubProperty ps)
                {
                    foreach (WzSubProperty p in ps.WzProperties)
                    {
                        int x = ((WzIntProperty)p["x"]).Value + center.X;
                        int y = ((WzIntProperty)p["y"]).Value + center.Y;
                        int pt = ((WzIntProperty)p["pt"]).Value;
                        string pn = ((WzStringProperty)p["pn"]).ReadString(string.Empty);

                        SKColor pColor = pt switch
                        {
                            0 => SKColors.Orange,
                            2 or 7 => SKColors.Blue,
                            3 => SKColors.Magenta,
                            1 or 8 => SKColors.BlueViolet,
                            _ => SKColors.IndianRed
                        };

                        bool drewPortalImg = false;
                        if (pn != string.Empty || pt == 2)
                        {
                            string portalPath = wzFile.WzDirectory.Name + "/MapHelper.img/portal/editor/" + (pt == 2 ? "pv" : pn);
                            if (wzFile.GetObjectFromPath(portalPath) is WzCanvasProperty portalCanvas)
                            {
                                drewPortalImg = true;
                                PointF origin = portalCanvas.GetCanvasOriginPosition();
                                drawBuf.DrawBitmap(portalCanvas.GetLinkedWzCanvasBitmap(), x - origin.X, y - origin.Y);
                            }
                        }
                        if (!drewPortalImg)
                        {
                            using var fill = new SKPaint { Color = pColor.WithAlpha(95), Style = SKPaintStyle.Fill };
                            drawBuf.DrawRect(x - 20, y - 20, 40, 40, fill);
                            drawBuf.DrawRect(x - 20, y - 20, 40, 40, strokePaint);
                        }
                        DrawTextAt(drawBuf, "Portal: " + p.Name, x - 8, y - 8, fontPortal, redPaint);

                        Ps.Add(new Portals.Portal { Shape = new Rectangle(x - 20, y - 20, 40, 40), Data = p });
                    }
                }

                // Life (mobs/NPCs)
                if (img["life"] is WzSubProperty lifeSub)
                {
                    foreach (WzSubProperty sp in lifeSub.WzProperties)
                    {
                        string type = ((WzStringProperty)sp["type"]).Value;
                        if (type != "n" && type != "m") continue;
                        bool isNPC = type == "n";
                        int lifeId = int.Parse(((WzStringProperty)sp["id"]).GetString());
                        int x = ((WzIntProperty)sp["x"]).Value + center.X;
                        int y = ((WzIntProperty)sp["y"]).Value + center.Y;
                        bool facingLeft = ((WzIntProperty)sp["f"]).ReadValue(0) == 0;

                        MSPs.Add(new SpawnPoint.Spawnpoint { Shape = new Rectangle(x - 15, y - 15, 30, 30), Data = sp });

                        string lifeStrId = lifeId.ToString().PadLeft(7, '0');
                        string wzBase = isNPC ? "Npc" : "Mob";
                        string linkPath = $"{wzBase}.wz/{lifeStrId}.img/info/link";
                        string namePath = $"String.wz/{wzBase}.img/{lifeId}/name";

                        if (WzFile.GetObjectFromMultipleWzFilePath(linkPath, Program.WzFileManager.WzFileList) is WzStringProperty linkInfo)
                        {
                            lifeId = int.Parse(linkInfo.GetString());
                            lifeStrId = lifeId.ToString().PadLeft(7, '0');
                        }

                        string standPath = $"{wzBase}.wz/{lifeStrId}.img/stand/0";
                        if (WzFile.GetObjectFromMultipleWzFilePath(standPath, Program.WzFileManager.WzFileList) is WzCanvasProperty lifeImg)
                        {
                            PointF origin = lifeImg.GetCanvasOriginPosition();
                            SKBitmap lifeBmp = lifeImg.GetLinkedWzCanvasBitmap();
                            SKBitmap? flipped = null;
                            if (!facingLeft) { flipped = FlipHorizontal(lifeBmp); lifeBmp = flipped; }
                            drawBuf.DrawBitmap(lifeBmp, x - origin.X, y - origin.Y);
                            flipped?.Dispose();
                        }
                        else
                        {
                            errorList.Add($"Missing mob/npc. Path: {linkPath}\r\n{standPath}");
                        }

                        if (WzFile.GetObjectFromMultipleWzFilePath(namePath, Program.WzFileManager.WzFileList) is WzStringProperty nameStr)
                            DrawTextAt(drawBuf, $"SP:{sp.Name} {nameStr.GetString()} ID:{lifeId}", x - 15 + 7, y - 15 + 7, fontPortal, redPaint);
                        else
                            errorList.Add("Missing mob/npc string. Path: " + namePath);
                    }
                }

                // Footholds
                if (img["foothold"] is WzSubProperty fhsSub)
                {
                    foreach (WzImageProperty fhspl0 in fhsSub.WzProperties)
                    {
                        foreach (WzImageProperty fhspl1 in fhspl0.WzProperties)
                        {
                            int ci = GetPseudoRandomColor(fhspl1.Name);
                            var fhColor = new SKColor((byte)((ci >> 16) & 0xFF), (byte)((ci >> 8) & 0xFF), (byte)(ci & 0xFF), 95);

                            foreach (WzSubProperty fh in fhspl1.WzProperties)
                            {
                                int x = ((WzIntProperty)fh["x1"]).Value + center.X;
                                int y = ((WzIntProperty)fh["y1"]).Value + center.Y;
                                int w = ((WzIntProperty)fh["x2"]).Value + center.X - x;
                                int h = ((WzIntProperty)fh["y2"]).Value + center.Y - y;
                                if (w < 0) { x += w; w = -w; }
                                if (h < 0) { y += h; h = -h; }
                                if (w < 15) w = 15;
                                h += 10;

                                FHs.Add(new FootHold.Foothold { Shape = new Rectangle(x, y, w, h), Data = fh });
                                using var fill = new SKPaint { Color = fhColor, Style = SKPaintStyle.Fill };
                                drawBuf.DrawRect(x, y, w, h, fill);
                                drawBuf.DrawRect(x, y, w, h, strokePaint);
                                DrawTextAt(drawBuf, fh.Name, x + w / 2f - 8, y + h / 2f - 8, fontPortal, redPaint);
                            }
                        }
                    }
                }
            }
            SaveBitmap(mapRender, Path.Combine(renderDir, mapIdName + "_footholdRender.png"));

            // Background
            var backgroundRender = new SKBitmap(bmpSize.Width, bmpSize.Height);
            using (var tileBuf = new SKCanvas(backgroundRender))
            {
                tileBuf.Clear(SKColors.Transparent);
                if (img["back"] is WzSubProperty backSub)
                {
                    foreach (WzSubProperty bgItem in backSub.WzProperties)
                    {
                        string bS = ((WzStringProperty)bgItem["bS"]).Value;
                        if (bS == string.Empty) continue;
                        int no = ((WzIntProperty)bgItem["no"]).Value;
                        int x = ((WzIntProperty)bgItem["x"]).Value;
                        int y = ((WzIntProperty)bgItem["y"]).Value;
                        bool facingLeft = ((WzIntProperty)bgItem["f"]).ReadValue(0) == 0;

                        string bgPath = $"Map.wz/Back/{bS}.img/Back/{no}";
                        if (WzFile.GetObjectFromMultipleWzFilePath(bgPath, Program.WzFileManager.WzFileList) is WzCanvasProperty bgCanvas)
                        {
                            PointF origin = bgCanvas.GetCanvasOriginPosition();
                            SKBitmap bgBmp = bgCanvas.GetLinkedWzCanvasBitmap();
                            SKBitmap? flipped = null;
                            if (!facingLeft) { flipped = FlipHorizontal(bgBmp); bgBmp = flipped; }
                            tileBuf.DrawBitmap(bgBmp, x + origin.X + center.X, y + origin.X + center.Y); // original uses .X for Y (preserved bug)
                            flipped?.Dispose();
                        }
                        else errorList.Add("Missing Map BG. Path: " + bgPath);
                    }
                }
            }
            SaveBitmap(backgroundRender, Path.Combine(renderDir, mapIdName + "_backgroundRender.png"));

            // Tooltips
            SKBitmap? toolTip = null;
            if (img["ToolTip"] is WzSubProperty tooltipProp)
            {
                toolTip = new SKBitmap(bmpSize.Width, bmpSize.Height);
                using var ttBuf = new SKCanvas(toolTip);
                ttBuf.Clear(SKColors.Transparent);
                string ttPath = "String.wz/ToolTipHelp.img/Mapobject/" + mapIdName;
                var wzTT = WzFile.GetObjectFromMultipleWzFilePath(ttPath, Program.WzFileManager.WzFileList) as WzSubProperty;
                if (wzTT == null) errorList.Add("Missing tooltip. Path: " + ttPath);

                for (int i = 0; i < 99; i++)
                {
                    if (tooltipProp[i.ToString()] is not WzSubProperty ttItem) break;
                    int x1 = ttItem["x1"].ReadValue();
                    int y1 = ttItem["y1"].ReadValue();

                    if (wzTT?[i.ToString()] is not WzSubProperty ttForI)
                    { errorList.Add($"Missing tooltip entry. Path: {ttPath}/{i}"); continue; }

                    string title = ttForI["Title"].ReadString(string.Empty);
                    string desc = ttForI["Desc"].ReadString(string.Empty);
                    DrawTextAt(ttBuf, $"{title}\n{desc}", x1 + center.X, y1 + center.Y, fontTooltip, blackPaint);
                }
                SaveBitmap(toolTip, Path.Combine(renderDir, mapIdName + "_tooltip.png"));
            }

            // Tiles + objects
            var tileRender = new SKBitmap(bmpSize.Width, bmpSize.Height);
            using (var tileBuf = new SKCanvas(tileRender))
            {
                tileBuf.Clear(SKColors.Transparent);
                for (int i = 0; i < 7; i++)
                {
                    if (img[i.ToString()] is not WzSubProperty iProperty) continue;
                    var objProps = iProperty["obj"] as WzSubProperty;
                    var infoProps = iProperty["info"] as WzSubProperty;
                    var tileProps = iProperty["tile"] as WzSubProperty;

                    if (objProps?.WzProperties.Count > 0)
                    {
                        foreach (WzSubProperty obj in objProps.WzProperties)
                        {
                            string imgName = ((WzStringProperty)obj["oS"]).Value + ".img";
                            string l0 = ((WzStringProperty)obj["l0"]).Value;
                            string l1 = ((WzStringProperty)obj["l1"]).Value;
                            string l2 = ((WzStringProperty)obj["l2"]).Value;
                            int x = ((WzIntProperty)obj["x"]).Value + center.X;
                            int y = ((WzIntProperty)obj["y"]).Value + center.Y;

                            string objPath = $"{wzFile.WzDirectory.Name}/Obj/{imgName}/{l0}/{l1}/{l2}/0";
                            var objData = WzFile.GetObjectFromMultipleWzFilePath(objPath, Program.WzFileManager.WzFileList) as WzImageProperty;

                            WzCanvasProperty? png = null;
                            PointF origin = PointF.Empty;
                            int retries = 0;
                        tryagain:
                            if (retries++ > 10) { errorList.Add("UOL loop at tile renderer"); return false; }
                            if (objData is WzCanvasProperty cp)
                            { png = cp; origin = cp.GetCanvasOriginPosition(); }
                            else if (objData is WzUOLProperty uol)
                            {
                                WzObject curr = objData.Parent!;
                                foreach (string d in uol.Value.Split('/'))
                                {
                                    if (d == "..") curr = curr.Parent!;
                                    else curr = curr switch
                                    {
                                        WzSubProperty s => s[d],
                                        WzCanvasProperty c2 => c2[d],
                                        WzImage wi => wi[d],
                                        WzConvexProperty cx => cx[d],
                                        _ => throw new InvalidOperationException("UOL error")
                                    };
                                }
                                objData = (WzImageProperty)curr;
                                goto tryagain;
                            }
                            else { errorList.Add("Unknown WZ type at tile renderer"); return false; }

                            if (png != null)
                                tileBuf.DrawBitmap(png.GetLinkedWzCanvasBitmap(), x - origin.X, y - origin.Y);
                        }
                    }

                    if (infoProps == null || tileProps == null) continue;
                    if (infoProps.WzProperties.Count == 0 || tileProps.WzProperties.Count == 0) continue;

                    string tileSetName = ((WzStringProperty)infoProps["tS"]).Value;
                    string tilePath = wzFile.WzDirectory.Name + "/Tile/" + tileSetName + ".img";
                    if (WzFile.GetObjectFromMultipleWzFilePath(tilePath, Program.WzFileManager.WzFileList) is not WzImage tileSet) continue;
                    if (!tileSet.Parsed) tileSet.ParseImage();

                    foreach (WzSubProperty tile in tileProps.WzProperties)
                    {
                        int x = ((WzIntProperty)tile["x"]).Value + center.X;
                        int y = ((WzIntProperty)tile["y"]).Value + center.Y;
                        string packName = ((WzStringProperty)tile["u"]).Value;
                        string tileID = ((WzIntProperty)tile["no"]).Value.ToString();
                        if (tileSet[packName] is not WzSubProperty tilePack
                            || tilePack[tileID] is not WzCanvasProperty tileCanvas)
                        { errorList.Add($"Tile {packName}/{tileID} not found."); continue; }
                        PointF tv = tileCanvas.GetCanvasOriginPosition();
                        tileBuf.DrawBitmap(tileCanvas.GetLinkedWzCanvasBitmap(), x - tv.X, y - tv.Y);
                    }
                }
            }
            SaveBitmap(tileRender, Path.Combine(renderDir, mapIdName + "_tileRender.png"));

            // NodeInfo
            SKBitmap? nodeInfoRender = null;
            if (img["nodeInfo"] is WzSubProperty nodeInfoProp)
            {
                nodeInfoRender = new SKBitmap(bmpSize.Width, bmpSize.Height);
                using var niBuf = new SKCanvas(nodeInfoRender);
                niBuf.Clear(SKColors.Transparent);
                using var wheatFill = new SKPaint { Color = SKColors.Wheat, Style = SKPaintStyle.Fill };

                foreach (WzImageProperty ni in nodeInfoProp.WzProperties)
                {
                    if (ni.Name is "edgeInfo" or "end" or "start" || !int.TryParse(ni.Name, out _)) continue;
                    int key = ((WzIntProperty)ni["key"]).ReadValue();
                    int x = ((WzIntProperty)ni["x"]).ReadValue() + center.X;
                    int y = ((WzIntProperty)ni["y"]).ReadValue() + center.Y;
                    niBuf.DrawRect(x, y, 200, 20, wheatFill);
                    niBuf.DrawRect(x, y, 200, 20, strokePaint);
                    DrawTextAt(niBuf, $"Key:{key} x:{x} y:{y}", x + 92, y + 2, fontPortal, blackPaint);
                }
                SaveBitmap(nodeInfoRender, Path.Combine(renderDir, mapIdName + "_nodeInfoRender.png"));
            }

            // Composite
            var fullBmp = new SKBitmap(bmpSize.Width, bmpSize.Height + 10);
            using (var fullBuf = new SKCanvas(fullBmp))
            {
                fullBuf.Clear(SKColors.CornflowerBlue);
                fullBuf.DrawBitmap(backgroundRender, 0, 0);
                fullBuf.DrawBitmap(tileRender, 0, 0);
                fullBuf.DrawBitmap(mapRender, 0, 0);
                if (toolTip != null) fullBuf.DrawBitmap(toolTip, 0, 0);
                if (nodeInfoRender != null) fullBuf.DrawBitmap(nodeInfoRender, 0, 0);
                fullBuf.DrawBitmap(minimapRender, 0, 0);
            }
            SaveBitmap(fullBmp, Path.Combine(renderDir, mapIdName + "_fullRender.png"));

            backgroundRender.Dispose();
            tileRender.Dispose();
            mapRender.Dispose();
            toolTip?.Dispose();
            nodeInfoRender?.Dispose();

            if (errorList.Count > 0)
            {
                fullBmp.Dispose();
                return false;
            }

            // fullBmp ownership transferred to DisplayMapWindow
            var showMap = new DisplayMapWindow(fullBmp, zoom, FHs, Ps, MSPs, settings);
            showMap.Closed += DisplayMapClosed;
            _openDisplayMaps.Add(showMap);
            showMap.Closed += (_, _) => _openDisplayMaps.Remove(showMap);

            try
            {
                var mainWin = GetMainWindow();
                if (mainWin != null) showMap.Show(mainWin);
                else showMap.Show();
                return true;
            }
            catch (FormatException)
            {
                Warning.Error("Invalid render scale value.");
                return false;
            }
        }

        public int GetPseudoRandomColor(string x)
        {
            byte[] md5 = MD5.HashData(Encoding.ASCII.GetBytes(x));
            return BitConverter.ToInt32(md5, 0) & 0xFFFFFF;
        }

        private void DisplayMapClosed(object? sender, EventArgs e)
            => _node?.Reparse();

        internal void ParseSettings()
        {
            settings.Clear();
            try
            {
                if (!File.Exists(SettingsPath))
                    File.WriteAllText(SettingsPath,
                        "!TAB1-!DPt:0!DPc:False!DNt:0!DNc:True!DFt:-230!DFc:False!\r\n" +
                        "!TAB2-!DXt:100!DXc:False!DYt:100!DYc:False!DTt:2!DTc:False!\r\n" +
                        "!TAB3-!DFPt:C:\\NEXON\\MapleStory\\Map.wz!DFPc:False!DSt:1!DSc:True!");

                string s;
                using (var r = new System.IO.StreamReader(SettingsPath)) s = r.ReadToEnd();

                settings.Add(Regex.Match(s, @"(?<=!DPt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DPc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DNt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DNc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DFt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DFc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DXt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DXc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DYt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DYc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DTt:)\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DTc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DFPt:)C:(%\w+)+.wz(?=!)").Value.Replace('%', '/'));
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DFPc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(s, @"(?<=!DSt:)\d*,?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(s, @"(?<=!DSc:)\w+(?=!)").Value));
            }
            catch
            {
                Warning.Error("Failed to load FH Mapper settings.");
                return;
            }
            foreach (var win in _openDisplayMaps)
                win.Settings = settings;
        }
    }
}
