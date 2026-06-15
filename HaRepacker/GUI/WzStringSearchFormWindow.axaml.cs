using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MapleLib.Helpers;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class WzStringSearchFormWindow : Window
    {
        public static Dictionary<int, KeyValuePair<string, string>> HexJumpList = new();
        public static Dictionary<int, int> JumpList_Map = new();

        private WzStringSearchFormDataCache? _dataCache;
        private string? _loadedWzVersion;

        public WzStringSearchFormWindow(WzStringSearchFormDataCache? dataCache, string? loadedWzVersion)
        {
            InitializeComponent();
            _dataCache = dataCache;
            _loadedWzVersion = loadedWzVersion;
            if (!string.IsNullOrEmpty(loadedWzVersion))
                versionLabel.Text = loadedWzVersion;
        }

        public WzStringSearchFormWindow() : this(null, null) { }

        public static void ShowWindow(Window? owner, WzStringSearchFormDataCache? dataCache, string? loadedWzVersion)
        {
            var win = new WzStringSearchFormWindow(dataCache, loadedWzVersion);
            if (owner != null) win.ShowDialog(owner);
            else win.Show();
        }

        private async void OnLoadClick(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select MapleStory WZ directory (containing Base.wz)",
                AllowMultiple = false,
            });
            if (folders == null || folders.Count == 0) return;

            string dir = folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;

            // Determine maple version
            var (ok, ver) = await Input.InputDialogs.ShowWzMapleVersionAsync(this, "Select WZ Encryption");
            if (!ok) return;

            _dataCache = new WzStringSearchFormDataCache(ver);
            bool loaded = _dataCache.OpenBaseWZFileFromDirectory(dir, out string version);
            if (loaded)
            {
                _loadedWzVersion = version;
                versionLabel.Text = version;
            }
            else
            {
                _dataCache = null;
                Warning.Error("Failed to load WZ files from the selected directory.");
            }
        }

        private void OnSearchTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            string query = textBox_itemidFind.Text ?? "";
            if (query.Length <= 2 && query.Length > 0) return;

            HexJumpList.Clear();
            comboBox_hexlist.Items.Clear();

            if (_dataCache == null) return;

            if (checkBox_searcheq.IsChecked == true)
                _dataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Eqp, query, HexJumpList);
            if (checkBox_searchuse.IsChecked == true)
                _dataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Use, query, HexJumpList);
            if (checkBox_searchsetup.IsChecked == true)
                _dataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Setup, query, HexJumpList);
            if (checkBox_searchetc.IsChecked == true)
                _dataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Etc, query, HexJumpList);
            if (checkBox_searchcash.IsChecked == true)
                _dataCache.LookupItemNameDesc(WzStringSearchFormDataCache.WzDataCacheItemType.Cash, query, HexJumpList);
            if (checkBox_searchquest.IsChecked == true)
                _dataCache.LookupQuest(query, HexJumpList);
            if (checkBox_searchNPC.IsChecked == true)
                _dataCache.LookupNPCs(query, HexJumpList);
            if (checkBox_searchMaps.IsChecked == true)
                _dataCache.LookupMaps(query, HexJumpList);
            if (checkbox_searchSkill.IsChecked == true)
                _dataCache.LookupSkills(query, HexJumpList);
            if (checkBox_searchJobs.IsChecked == true)
                _dataCache.LookupJobs(query, HexJumpList);

            if (HexJumpList.Count == 0)
            {
                textBox6.Text = "0";
                label_itemname.Text = "Not found";
                label_itemdesc.Text = "Not found";
            }
            else
            {
                bool first = true;
                foreach (var data in HexJumpList)
                {
                    if (first)
                    {
                        textBox6.Text = data.Key.ToString();
                        label_itemname.Text = data.Value.Key;
                        label_itemdesc.Text = data.Value.Value;
                        first = false;
                    }
                    comboBox_hexlist.Items.Add($"{data.Value.Key} - {data.Value.Value}");
                }
            }
        }

        private void OnHexListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = comboBox_hexlist.SelectedIndex;
            if (selectedIndex < 0) return;

            int count = 0;
            foreach (var data in HexJumpList)
            {
                if (count == selectedIndex)
                {
                    textBox6.Text = data.Key.ToString();
                    label_itemname.Text = data.Value.Key;
                    label_itemdesc.Text = data.Value.Value;
                    break;
                }
                count++;
            }
        }

        private async void OnIntegerTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            int data = 0;
            if (int.TryParse(textBox6.Text, out int parsed)) data = parsed;

            string hex = BitConverter.ToString(ByteUtils.IntegerToLittleEndian(data)).Replace("-", " ");
            textBox5.Text = hex;

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null) await clipboard.SetTextAsync(hex);
            }
            catch { }
        }
    }
}
