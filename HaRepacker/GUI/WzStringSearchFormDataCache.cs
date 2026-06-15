using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HaRepacker.GUI
{
    public class WzStringSearchFormDataCache
    {
        public enum WzDataCacheItemType
        {
            Cash, Use, Setup, Eqp, Etc
        }

        private WzMapleVersion WzMapleVersion = WzMapleVersion.BMS;

        private Dictionary<string, WzFile> Files { get; set; }

        private Dictionary<int, Tuple<string, string, string>> MapNameCache;
        private Dictionary<int, KeyValuePair<string, string>> CashItemCache;
        private Dictionary<int, KeyValuePair<string, string>> EtcItemCache;
        private Dictionary<int, KeyValuePair<string, string>> SetupItemCache;
        private Dictionary<int, KeyValuePair<string, string>> UseItemCache;
        private Dictionary<int, KeyValuePair<string, string>> EqpItemCache;
        private Dictionary<int, Tuple<string, string, string>> Quests;
        private Dictionary<int, Tuple<string, string, string>> SkillsCache;
        private Dictionary<int, string> JobsCache;
        private Dictionary<int, KeyValuePair<string, string>> NPCsCache;

        public WzStringSearchFormDataCache(WzMapleVersion wzMapleVersion)
        {
            WzMapleVersion = wzMapleVersion;
            Files = new Dictionary<string, WzFile>();
            MapNameCache = new Dictionary<int, Tuple<string, string, string>>();
            CashItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            EtcItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            SetupItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            UseItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            EqpItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            Quests = new Dictionary<int, Tuple<string, string, string>>();
            SkillsCache = new Dictionary<int, Tuple<string, string, string>>();
            JobsCache = new Dictionary<int, string>();
            NPCsCache = new Dictionary<int, KeyValuePair<string, string>>();
        }

        #region Getter Methods
        public string GetMapName(int mapid, string defaultValue = null)
        {
            return MapNameCache.ContainsKey(mapid) ? MapNameCache[mapid].Item2 : defaultValue;
        }

        public void LookupMaps(string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            searchQuery = searchQuery.ToLower();
            foreach (var item in MapNameCache)
            {
                var val = item.Value;
                if (val.Item1.ToLower().Contains(searchQuery) ||
                    val.Item2.ToLower().Contains(searchQuery) ||
                    val.Item3.ToLower().Contains(searchQuery))
                {
                    hexJumpList[item.Key] = new KeyValuePair<string, string>($"[{val.Item1}]{val.Item3}", val.Item2);
                }
            }
        }

        public void LookupQuest(string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            searchQuery = searchQuery.ToLower();
            foreach (var item in Quests)
            {
                if (item.Value.Item1.ToLower().Contains(searchQuery))
                    hexJumpList[item.Key] = new KeyValuePair<string, string>(item.Value.Item1, item.Value.Item2);
            }
        }

        public void LookupSkills(string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            searchQuery = searchQuery.ToLower();
            foreach (var item in SkillsCache)
            {
                string skillName = item.Value.Item1;
                if (skillName == null)
                {
                    Debug.WriteLine("Skillid of " + item.Key + " is null.");
                    continue;
                }
                string desc = item.Value.Item2;
                if (skillName.ToLower().Contains(searchQuery) || desc.ToLower().Contains(searchQuery))
                    hexJumpList[item.Key] = new KeyValuePair<string, string>(skillName, $"{desc}\r\n{item.Value.Item3}");
            }
        }

        public void LookupJobs(string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            searchQuery = searchQuery.ToLower();
            foreach (var item in JobsCache)
            {
                if (item.Value.ToLower().Contains(searchQuery))
                    hexJumpList[item.Key] = new KeyValuePair<string, string>(item.Key.ToString(), item.Value);
            }
        }

        public void LookupNPCs(string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            searchQuery = searchQuery.ToLower();
            foreach (var item in NPCsCache)
            {
                string name = item.Value.Key.ToLower();
                string func = item.Value.Value.ToLower();
                if (name.Contains(searchQuery) || func.Contains(searchQuery))
                    hexJumpList[item.Key] = new KeyValuePair<string, string>(item.Value.Key, item.Value.Value);
            }
        }

        public string GetItemName(WzDataCacheItemType type, int itemid, string defaultValue = null)
        {
            var cache = GetCache(type);
            return cache != null && cache.ContainsKey(itemid) ? cache[itemid].Key : defaultValue;
        }

        public string GetItemDesc(WzDataCacheItemType type, int itemid, string defaultValue = null)
        {
            var cache = GetCache(type);
            return cache != null && cache.ContainsKey(itemid) ? cache[itemid].Value : defaultValue;
        }

        public void LookupItemNameDesc(WzDataCacheItemType type, string searchQuery, Dictionary<int, KeyValuePair<string, string>> hexJumpList)
        {
            var cache = GetCache(type);
            if (cache == null) return;
            searchQuery = searchQuery.ToLower();
            foreach (var item in cache)
            {
                if (item.Value.Key.ToLower().Contains(searchQuery) || item.Value.Value.ToLower().Contains(searchQuery))
                    hexJumpList[item.Key] = item.Value;
            }
        }

        private Dictionary<int, KeyValuePair<string, string>> GetCache(WzDataCacheItemType type)
        {
            return type switch
            {
                WzDataCacheItemType.Cash => CashItemCache,
                WzDataCacheItemType.Eqp => EqpItemCache,
                WzDataCacheItemType.Etc => EtcItemCache,
                WzDataCacheItemType.Setup => SetupItemCache,
                WzDataCacheItemType.Use => UseItemCache,
                _ => null,
            };
        }
        #endregion

        #region Cache
        /// <summary>
        /// Loads WZ files from a directory (chosen by the caller) and builds caches.
        /// Returns true on success, sets loadedVersion to a display string.
        /// </summary>
        public bool OpenBaseWZFileFromDirectory(string dir, out string loadedVersion)
        {
            loadedVersion = string.Empty;
            foreach (string filePath in Directory.GetFiles(dir))
            {
                var info = new FileInfo(filePath);
                if (!string.Equals(info.Extension, ".wz", StringComparison.OrdinalIgnoreCase))
                    continue;

                var wzFile = new WzFile(filePath, WzMapleVersion);
                var parseStatus = wzFile.ParseWzFile();
                if (parseStatus == WzFileParseStatus.Success)
                {
                    Files[info.Name] = wzFile;
                    if (string.IsNullOrEmpty(loadedVersion))
                        loadedVersion = $"MapleStory v.{wzFile.Version} WZ version: {wzFile.MapleVersion}";
                }
                else
                {
                    Warning.Error(parseStatus.GetErrorDescription());
                }
            }
            if (Files.Count == 0) return false;
            ParseWZFiles();
            return true;
        }

        public WzFile GetLoadedWZFile(string fileName)
        {
            return Files.TryGetValue(fileName, out var f) ? f : null;
        }

        private void ParseWZFiles()
        {
            if (!Files.Any()) throw new Exception("BaseWZ file not opened yet.");
            CacheMaps();
            CacheInventoryData();
            CacheQuestData();
        }

        private void CacheQuestData()
        {
            var questDir = Files["Quest.wz"].WzDirectory;
            var questInfoImg = (WzImage)questDir["QuestInfo.img"];
            foreach (WzSubProperty item in questInfoImg.WzProperties)
            {
                int questID = int.Parse(item.Name);
                string questName = item["name"].ReadString("NULL");
                string parentQuestName = item["parent"].ReadString("NULL");
                string questDescription = item["0"].ReadString("NULL");
                Quests[questID] = new Tuple<string, string, string>(questName, questDescription, parentQuestName);
            }
        }

        private void CacheInventoryData()
        {
            var stringDir = Files["String.wz"].WzDirectory;

            var skillDir = (WzImage)stringDir["Skill.img"];
            foreach (WzSubProperty skill in skillDir.WzProperties)
            {
                int id = int.Parse(skill.Name);
                string bookName = skill["bookName"]?.ReadString(null);
                if (bookName != null)
                {
                    if (!JobsCache.ContainsKey(id)) JobsCache[id] = bookName;
                }
                else
                {
                    string name = skill["name"]?.ReadString("NULL");
                    string desc = skill["desc"]?.ReadString(null) ?? string.Empty;
                    string h1 = skill["h1"]?.ReadString(null) ?? string.Empty;
                    if (name != null && !SkillsCache.ContainsKey(id))
                        SkillsCache[id] = new Tuple<string, string, string>(name, desc, h1);
                }
            }

            var npcDir = (WzImage)stringDir["Npc.img"];
            foreach (WzSubProperty npc in npcDir.WzProperties)
            {
                int id = int.Parse(npc.Name);
                if (!NPCsCache.ContainsKey(id))
                    NPCsCache[id] = new KeyValuePair<string, string>(npc["name"].ReadString("NULL"), npc["func"].ReadString("NULL"));
            }

            AddToCache((WzImage)stringDir["Cash.img"], CashItemCache);
            AddToCache((WzImage)stringDir["Consume.img"], UseItemCache);
            AddToCache((WzImage)stringDir["Ins.img"], SetupItemCache);

            foreach (WzSubProperty mapArea in ((WzImage)stringDir["Etc.img"]).WzProperties)
                foreach (WzSubProperty item in mapArea.WzProperties)
                    AddItem(int.Parse(item.Name), item, EtcItemCache);

            foreach (WzSubProperty eqp in ((WzImage)stringDir["Eqp.img"]).WzProperties)
                foreach (WzSubProperty cat in eqp.WzProperties)
                    foreach (WzSubProperty item in cat.WzProperties)
                        AddItem(int.Parse(item.Name), item, EqpItemCache);
        }

        private static void AddToCache(WzImage img, Dictionary<int, KeyValuePair<string, string>> cache)
        {
            foreach (WzSubProperty item in img.WzProperties)
                AddItem(int.Parse(item.Name), item, cache);
        }

        private static void AddItem(int id, WzSubProperty item, Dictionary<int, KeyValuePair<string, string>> cache)
        {
            if (!cache.ContainsKey(id))
                cache[id] = new KeyValuePair<string, string>(item["name"].ReadString("NULL"), item["desc"].ReadString("NULL"));
        }

        private void CacheMaps()
        {
            var stringDir = Files["String.wz"].WzDirectory;
            var mapStringDir = (WzImage)stringDir["Map.img"];
            foreach (WzSubProperty mapArea in mapStringDir.WzProperties)
            {
                string regionName = mapArea.Name;
                foreach (WzSubProperty map in mapArea.WzProperties)
                {
                    int id = int.Parse(map.Name);
                    string mapName = ((WzStringProperty)map["mapName"]).ReadString("");
                    string streetName = ((WzStringProperty)map["streetName"]).ReadString("");
                    if (!MapNameCache.ContainsKey(id))
                        MapNameCache[id] = new Tuple<string, string, string>(regionName, mapName, streetName);
                }
            }
        }
        #endregion
    }
}
