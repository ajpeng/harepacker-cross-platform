/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using HaCreator.MapEditor.Info;
using MapleLib;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    internal static class WzFileExtractor
    {
        public static void ExtractMobFile()
        {
            if (Program.InfoManager.MobIconCache.Count != 0) return;

            const string PATH = "mob";
            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase(PATH))
            {
                foreach (WzImage mobImage in dir.WzImages)
                {
                    string mobIdStr = mobImage.Name.Replace(".img", "");
                    switch (mobIdStr)
                    {
                        case "BossAzmothCanyon": case "BossBaldrix": case "BossChampionRaid":
                        case "BossCommon": case "BossEnterAni": case "BossLimbo":
                        case "BossNohime": case "BossSuu": case "PatternSystem":
                        case "pack_ignore.txt":
                            break;
                        default:
                        {
                            if (!int.TryParse(mobIdStr, out int mobId) || mobId == 0)
                            {
                                ErrorLogger.Log(ErrorLevel.Info, "New file in Mob.wz: " + mobIdStr);
                                continue;
                            }
                            WzImageProperty standCanvas = (WzCanvasProperty)mobImage["stand"]?["0"]?.GetLinkedWzImageProperty();
                            if (standCanvas == null) continue;
                            if (!Program.InfoManager.MobIconCache.ContainsKey(mobId))
                                Program.InfoManager.MobIconCache.Add(mobId, standCanvas);
                            break;
                        }
                    }
                }
            }
        }

        public static void ExtractNpcFile()
        {
            if (Program.InfoManager.NpcPropertyCache.Count != 0) return;

            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("npc"))
                foreach (WzImage img in dir.WzImages)
                    if (!Program.InfoManager.NpcPropertyCache.ContainsKey(img.Name.Replace(".img", "")))
                        Program.InfoManager.NpcPropertyCache.Add(img.Name.Replace(".img", ""), img);
        }

        public static void ExtractReactorFile()
        {
            if (Program.InfoManager.Reactors.Count != 0) return;

            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("reactor"))
            {
                foreach (WzImage img in dir.WzImages)
                {
                    WzSubProperty infoProp = (WzSubProperty)img["info"];
                    string reactorId = WzInfoTools_RemoveExtension(img.Name);
                    string name = "NO NAME";
                    if (infoProp != null)
                    {
                        name = ((WzStringProperty)infoProp?["info"])?.Value;
                        if (name == null)
                            name = ((WzStringProperty)infoProp?["viewName"])?.Value ?? string.Empty;
                    }
                    var reactor = new ReactorInfo(null, new System.Drawing.Point(), reactorId, name, img);
                    Program.InfoManager.Reactors[reactor.ID] = reactor;
                }
            }
        }

        public static void ExtractQuestFile()
        {
            if (Program.InfoManager.QuestActs.Count != 0) return;

            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("quest"))
            {
                foreach (WzImage questImage in dir.WzImages)
                {
                    switch (questImage.Name)
                    {
                        case "Act.img":
                            foreach (WzImageProperty p in questImage.WzProperties)
                                Program.InfoManager.QuestActs.Add(p.Name, p as WzSubProperty);
                            break;
                        case "Check.img":
                            foreach (WzImageProperty p in questImage.WzProperties)
                                Program.InfoManager.QuestChecks.Add(p.Name, p as WzSubProperty);
                            break;
                        case "QuestInfo.img":
                            foreach (WzImageProperty p in questImage.WzProperties)
                                Program.InfoManager.QuestInfos.Add(p.Name, p as WzSubProperty);
                            break;
                        case "Say.img":
                            foreach (WzImageProperty p in questImage.WzProperties)
                                Program.InfoManager.QuestSays.Add(p.Name, p as WzSubProperty);
                            break;
                    }
                }
            }
        }

        public static void ExtractSkillFile()
        {
            if (Program.InfoManager.SkillWzImageCache.Count != 0) return;

            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("skill"))
            {
                foreach (WzImage img in dir.WzImages)
                {
                    WzImageProperty imgSkill = img["skill"];
                    if (imgSkill == null) continue;
                    foreach (WzImageProperty skillItem in imgSkill.WzProperties)
                        Program.InfoManager.SkillWzImageCache.Add(skillItem.Name, skillItem);
                }
            }
        }

        public static void ExtractItemFile()
        {
            if (Program.InfoManager.MapsNameCache.Count == 0)
                throw new Exception("ExtractStringFile must be called before ExtractItemFile.");
            if (Program.InfoManager.ItemIconCache.Count != 0) return;

            foreach (WzDirectory itemDir in Program.WzManager.GetWzDirectoriesFromBase("item"))
            {
                Parallel.ForEach(itemDir.WzDirectories, sub =>
                {
                    switch (sub.Name)
                    {
                        case "ItemOption.img":
                        case "Special":
                            break;
                        case "Consume": case "Etc": case "Cash": case "Install":
                            foreach (WzImage grpImg in sub.WzImages)
                                foreach (WzImageProperty itemImg in grpImg.WzProperties)
                                {
                                    if (itemImg is WzSubProperty itemProp)
                                    {
                                        WzCanvasProperty icon = itemProp?["info"]?["icon"] as WzCanvasProperty;
                                        if (icon != null && int.TryParse(itemImg.Name, out int id))
                                            lock (Program.InfoManager.ItemIconCache)
                                                if (!Program.InfoManager.ItemIconCache.ContainsKey(id))
                                                    Program.InfoManager.ItemIconCache.Add(id, icon);
                                    }
                                }
                            break;
                        case "Pet":
                            foreach (WzImage petImg in sub.WzImages)
                            {
                                WzCanvasProperty icon = petImg["info"]?["icon"] as WzCanvasProperty;
                                if (icon != null && int.TryParse(petImg.Name.Replace(".img", ""), out int id))
                                    lock (Program.InfoManager.ItemIconCache)
                                        if (!Program.InfoManager.ItemIconCache.ContainsKey(id))
                                            Program.InfoManager.ItemIconCache.Add(id, icon);
                            }
                            break;
                    }
                });
            }
        }

        public static void ExtractSoundFile()
        {
            if (Program.InfoManager.BGMs.Count != 0) return;

            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("sound"))
            {
                foreach (WzImage soundImage in dir.WzImages)
                {
                    if (!soundImage.Name.ToLower().Contains("bgm")) continue;
                    try
                    {
                        foreach (WzImageProperty bgmProp in soundImage.WzProperties)
                        {
                            WzBinaryProperty binProp = null;
                            if (bgmProp is WzBinaryProperty bin)
                                binProp = bin;
                            else if (bgmProp is WzUOLProperty uol && uol.LinkValue is WzBinaryProperty linkBin)
                                binProp = linkBin;

                            if (binProp != null)
                                Program.InfoManager.BGMs[WzInfoTools_RemoveExtension(soundImage.Name) + @"/" + binProp.Name] = binProp;
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorLogger.Log(ErrorLevel.IncorrectStructure,
                            $"[ExtractSoundFile] Error parsing {dir.Name}, {soundImage.Name}\r\nError: {e}");
                    }
                }
            }
        }

        public static void ExtractMapMarks()
        {
            if (Program.InfoManager.MapMarks.Count != 0) return;

            WzImage mapWzImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapWzImg == null) throw new Exception("MapHelper.img not found in map.wz.");

            foreach (WzCanvasProperty mark in mapWzImg["mark"].WzProperties)
                Program.InfoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
        }

        public static void ExtractMapPortals()
        {
            if (Program.InfoManager.PortalGame.Count != 0) return;

            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("map", "MapHelper.img");
            if (mapImg == null) throw new Exception("Couldn't extract portals. MapHelper.img not found.");

            WzSubProperty portalParent = (WzSubProperty)mapImg["portal"];

            WzSubProperty editorParent = (WzSubProperty)portalParent["editor"];
            foreach (WzCanvasProperty portalProp in editorParent.WzProperties)
            {
                Program.InfoManager.PortalEditor_TypeById.Add(PortalTypeExtensions.FromCode(portalProp.Name));
                PortalInfo.Load(portalProp);
            }

            WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
            foreach (WzImageProperty portalProp in gameParent.WzProperties)
            {
                PortalType portalType = PortalTypeExtensions.FromCode(portalProp.Name);

                if (portalProp["default"]?["portalStart"] != null)
                {
                    Dictionary<string, List<SKBitmap?>> templates = new();
                    foreach (WzSubProperty imgProp in portalProp.WzProperties)
                    {
                        var portalStart = imgProp["portalStart"] as WzSubProperty;
                        List<SKBitmap?> images = new();
                        if (portalStart != null)
                            foreach (WzCanvasProperty canvas in portalStart.WzProperties)
                                images.Add(canvas.GetLinkedWzCanvasBitmap());
                        templates[imgProp.Name] = images;
                    }
                    var first = templates.FirstOrDefault().Value;
                    Program.InfoManager.PortalGame[portalType] = new PortalGameImageInfo(first?.FirstOrDefault(), templates);
                }
                else
                {
                    Dictionary<string, List<SKBitmap?>> templates = new();
                    SKBitmap? defaultImage = null;
                    List<SKBitmap?> images = new();
                    foreach (WzImageProperty prop in portalProp.WzProperties)
                    {
                        if (prop is WzCanvasProperty canvas)
                        {
                            defaultImage = canvas.GetLinkedWzCanvasBitmap();
                            images.Add(defaultImage);
                        }
                    }
                    templates["default"] = images;
                    Program.InfoManager.PortalGame[portalType] = new PortalGameImageInfo(defaultImage, templates);
                }
            }

            for (int i = 0; i < Program.InfoManager.PortalEditor_TypeById.Count; i++)
                Program.InfoManager.PortalIdByType[Program.InfoManager.PortalEditor_TypeById[i]] = i;
        }

        public static void ExtractMapTileSets()
        {
            if (Program.InfoManager.TileSets.Count != 0) return;

            WzDirectory tileDir = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Tile");
            if (tileDir != null)
            {
                foreach (WzImage ts in tileDir.WzImages)
                    Program.InfoManager.TileSets[WzInfoTools_RemoveExtension(ts.Name)] = ts;
            }
            else
            {
                foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("map\\tile"))
                    foreach (WzImage ts in dir.WzImages)
                        Program.InfoManager.TileSets[WzInfoTools_RemoveExtension(ts.Name)] = ts;
            }

            if (Program.InfoManager.TileSets is Dictionary<string, WzImage> d)
                Program.InfoManager.TileSets = d.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }

        public static void ExtractMapObjSets()
        {
            if (Program.InfoManager.ObjectSets.Count != 0) return;

            WzDirectory objDir = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Obj");
            if (objDir != null)
            {
                foreach (WzImage obj in objDir.WzImages)
                    Program.InfoManager.ObjectSets[WzInfoTools_RemoveExtension(obj.Name)] = obj;
                return;
            }
            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("map\\obj"))
                foreach (WzImage obj in dir.WzImages)
                    Program.InfoManager.ObjectSets[WzInfoTools_RemoveExtension(obj.Name)] = obj;
        }

        public static void ExtractMapBackgroundSets()
        {
            if (Program.InfoManager.BackgroundSets.Count != 0) return;

            WzDirectory backDir = (WzDirectory)Program.WzManager.FindWzImageByName("map", "Back");
            if (backDir != null)
            {
                foreach (WzImage bg in backDir.WzImages)
                    Program.InfoManager.BackgroundSets[WzInfoTools_RemoveExtension(bg.Name)] = bg;
                return;
            }
            foreach (WzDirectory dir in Program.WzManager.GetWzDirectoriesFromBase("map\\back"))
                foreach (WzImage bg in dir.WzImages)
                    Program.InfoManager.BackgroundSets[WzInfoTools_RemoveExtension(bg.Name)] = bg;
        }

        public static void ExtractStringFile(bool bIsBetaMapleStory)
        {
            if (Program.InfoManager.MapsNameCache.Count != 0) return;

            // NPC strings
            WzImage npcImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Npc.img");
            foreach (WzSubProperty npc in npcImg.WzProperties)
            {
                string npcId = npc.Name;
                string name = (npc["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string func = (npc["func"] as WzStringProperty)?.Value ?? string.Empty;
                if (!Program.InfoManager.NpcNameCache.ContainsKey(npcId))
                    Program.InfoManager.NpcNameCache[npcId] = new Tuple<string, string>(name, func);
                else
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Npc '{npcId}'");
            }

            // Map strings
            WzImage mapImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Map.img");
            foreach (WzSubProperty mapCat in mapImg.WzProperties)
            {
                foreach (WzSubProperty map in mapCat.WzProperties)
                {
                    string streetName = (map["streetName"] as WzStringProperty)?.Value ?? string.Empty;
                    string mapName = (map["mapName"] as WzStringProperty)?.Value;
                    string mapId = map.Name.Length == 9 ? map.Name : AddLeadingZeros(map.Name, 9);
                    string cat = map.Parent.Name;
                    Program.InfoManager.MapsNameCache[mapId] = mapName == null
                        ? new Tuple<string, string, string>("NO NAME", "NO NAME", "NO NAME")
                        : new Tuple<string, string, string>(streetName, mapName, cat);
                }
            }

            // Mob strings
            WzImage mobImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Mob.img");
            foreach (WzSubProperty mob in mobImg.WzProperties)
            {
                string id = mob.Name;
                string name = (mob["name"] as WzStringProperty)?.Value ?? "NO NAME";
                if (!Program.InfoManager.MobNameCache.ContainsKey(id))
                    Program.InfoManager.MobNameCache[id] = name;
                else
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Mob '{id}'");
            }

            // Skill strings
            WzImage skillImg = (WzImage)Program.WzManager.FindWzImageByName("string", "Skill.img");
            foreach (WzSubProperty skill in skillImg.WzProperties)
            {
                string id = skill.Name;
                string name = (skill["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string desc = (skill["desc"] as WzStringProperty)?.Value ?? "NO DESC";
                if (!Program.InfoManager.SkillNameCache.ContainsKey(id))
                    Program.InfoManager.SkillNameCache[id] = new Tuple<string, string>(name, desc);
                else
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Skill '{id}'");
            }

            // Equipment strings (Eqp)
            WzPropertyCollection eqpProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Eqp"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Eqp.img")).WzProperties;
            foreach (WzSubProperty eqpSub in eqpProps)
                foreach (WzSubProperty catSub in eqpSub.WzProperties)
                {
                    if (bIsBetaMapleStory)
                        ProcessEquipItem(catSub, catSub.Name);
                    else
                        foreach (WzImageProperty itemProp in catSub.WzProperties)
                            ProcessEquipItem(itemProp, catSub.Name);
                }

            // Install strings (Ins)
            WzPropertyCollection insProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Ins"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Ins.img")).WzProperties;
            foreach (WzImageProperty p in insProps)
                if (p is WzSubProperty sub) ProcessGenericItem(sub, "Ins");

            // Cash strings
            WzPropertyCollection cashProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Cash"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Cash.img")).WzProperties;
            foreach (WzSubProperty sub in cashProps)
                ProcessGenericItem(sub, "Cash");

            // Consume strings
            WzPropertyCollection conProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Con"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Consume.img")).WzProperties;
            foreach (WzSubProperty sub in conProps)
                ProcessGenericItem(sub, "Consume");

            // Etc strings
            WzPropertyCollection etcProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Etc"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Etc.img")).WzProperties;
            foreach (WzSubProperty etcSub in etcProps)
            {
                if (bIsBetaMapleStory)
                    ProcessEtcItem(etcSub, etcSub.Name);
                else
                    foreach (WzSubProperty sub in etcSub.WzProperties.Cast<WzSubProperty>())
                        ProcessEtcItem(sub, etcSub.Name);
            }

            // Pet strings
            WzPropertyCollection petProps = bIsBetaMapleStory
                ? ((WzSubProperty)Program.WzManager.FindWzImageByName("string", "Item.img")["Pet"]).WzProperties
                : ((WzImage)Program.WzManager.FindWzImageByName("string", "Pet.img")).WzProperties;
            foreach (WzImageProperty p in petProps)
                ProcessPetItem(p);
        }

        public static void ExtractMaps()
        {
            if (Program.InfoManager.MapsCache.Count != 0) return;

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            Parallel.ForEach(Program.InfoManager.MapsNameCache, parallelOptions, val =>
            {
                if (!int.TryParse(val.Key, out int mapid)) return;

                WzImage mapImage = FindMapImage(mapid.ToString(), Program.WzManager);
                if (mapImage == null || mapImage["info"] == null) return;

                var names = val.Value;
                var info = new MapInfo(mapImage, names.Item1, names.Item2, names.Item3);
                lock (Program.InfoManager.MapsCache)
                    Program.InfoManager.MapsCache[val.Key] =
                        new Tuple<WzImage, string, string, string, MapInfo>(mapImage, names.Item1, names.Item2, names.Item3, info);
            });
        }

        // ── String-file helpers ───────────────────────────────────────

        private static void ProcessEquipItem(WzImageProperty prop, string category)
        {
            if (prop is WzSubProperty sub && int.TryParse(sub.Name, out int id))
            {
                string name = (sub["name"] as WzStringProperty)?.Value ?? "NO NAME";
                string desc = (sub["desc"] as WzStringProperty)?.Value ?? "NO DESC";
                if (!Program.InfoManager.ItemNameCache.ContainsKey(id))
                    Program.InfoManager.ItemNameCache[id] = new Tuple<string, string, string>(category, name, desc);
                else
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Equip item '{sub.Name}'");
            }
        }

        private static void ProcessGenericItem(WzSubProperty sub, string category)
        {
            if (!int.TryParse(sub.Name, out int id)) return;
            string name = (sub["name"] as WzStringProperty)?.Value ?? "NO NAME";
            string desc = (sub["desc"] as WzStringProperty)?.Value ?? "NO DESC";
            if (!Program.InfoManager.ItemNameCache.ContainsKey(id))
                Program.InfoManager.ItemNameCache[id] = new Tuple<string, string, string>(category, name, desc);
            else
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate {category} item '{sub.Name}'");
        }

        private static void ProcessEtcItem(WzSubProperty sub, string parentName)
        {
            if (!int.TryParse(sub.Name, out int id)) return;
            const string cat = "Etc";
            string name = (sub["name"] as WzStringProperty)?.Value ?? "NO NAME";
            string desc = (sub["desc"] as WzStringProperty)?.Value ?? "NO DESC";
            if (!Program.InfoManager.ItemNameCache.ContainsKey(id))
                Program.InfoManager.ItemNameCache[id] = new Tuple<string, string, string>(cat, name, desc);
            else
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Etc item '{sub.Name}' parent={parentName}");
        }

        private static void ProcessPetItem(WzImageProperty p)
        {
            if (p is not WzSubProperty sub || !int.TryParse(sub.Name, out int id)) return;
            const string cat = "Pet";
            string name = (sub["name"] as WzStringProperty)?.Value ?? "NO NAME";
            string desc = (sub["desc"] as WzStringProperty)?.Value ?? "NO DESC";
            if (!Program.InfoManager.ItemNameCache.ContainsKey(id))
                Program.InfoManager.ItemNameCache[id] = new Tuple<string, string, string>(cat, name, desc);
            else
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, $"[WzFileExtractor] Duplicate Pet item '{sub.Name}'");
        }

        // ── Local copies of WzInfoTools helpers needed here ──────────

        private static string WzInfoTools_RemoveExtension(string source)
        {
            int idx = source.LastIndexOf('.');
            return idx < 0 ? source : source.Substring(0, idx);
        }

        private static string AddLeadingZeros(string source, int maxLength)
        {
            while (source.Length < maxLength) source = "0" + source;
            return source;
        }

        private static WzImage FindMapImage(string mapid, WzFileManager fileManager)
        {
            // Delegate to WzInfoTools in HaSharedLibrary if available, else search directly
            return HaSharedLibrary.Wz.WzInfoTools.FindMapImage(mapid, fileManager);
        }
    }
}
