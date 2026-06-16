/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaCreator.Wz;
using MapleLib;
using MapleLib.ClientLib;
using MapleLib.Helpers;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    public partial class InitializationWindow : Window
    {
        private static WzMapleVersion _wzMapleVersion = WzMapleVersion.BMS;
        public static WzMapleVersion WzMapleVersion => _wzMapleVersion;

        private bool _initialising = false;

        public InitializationWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;

            if (Program.StartupManager?.VersionManager != null)
                Program.StartupManager.VersionManager.VersionsChanged += OnVersionsChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // WZ version dropdown
            foreach (WzMapleVersion v in Enum.GetValues<WzMapleVersion>())
                cmbVersion.Items.Add(v.ToString());
            cmbVersion.SelectedIndex = ApplicationSettings.MapleVersionIndex;

            // WZ path dropdown
            foreach (string path in ApplicationSettings.MapleFoldersList
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(Directory.Exists))
                cmbPath.Items.Add(path);
            foreach (string path in WzFileManager.COMMON_MAPLESTORY_DIRECTORY.Where(Directory.Exists))
                if (!cmbPath.Items.Contains(path))
                    cmbPath.Items.Add(path);
            if (cmbPath.Items.Count == 0)
                cmbPath.Items.Add("Select MapleStory Folder");

            int pathIdx = Math.Min(ApplicationSettings.MapleFolderIndex, cmbPath.Items.Count - 1);
            cmbPath.SelectedIndex = pathIdx;

            // Localisation dropdown — list of { Text, Value } anonymous objects
            var locItems = Enum.GetValues<MapleStoryLocalisation>()
                .Select(v => new LocalisationItem(v.ToString().Replace("MapleStory", "MapleStory "), (int)v))
                .ToList();
            cmbLocalisation.ItemsSource = locItems;
            cmbLocalisation.SelectedItem = locItems.FirstOrDefault(x => x.Value == ApplicationSettings.MapleStoryClientLocalisation)
                                           ?? locItems[0];

            // IMG version list
            RefreshVersionList();

            // Select last used version
            var config = HaCreatorConfig.Load();
            if (!string.IsNullOrEmpty(config.LastUsedVersion))
            {
                var match = lstVersions.Items.OfType<VersionListItem>()
                    .FirstOrDefault(i => i.Version.Version == config.LastUsedVersion);
                if (match != null) lstVersions.SelectedItem = match;
            }
            if (lstVersions.SelectedItem == null && lstVersions.Items.Count > 0)
                lstVersions.SelectedIndex = 0;

            // Default tab
            if (config.DataSourceMode == DataSourceMode.ImgFileSystem && lstVersions.Items.Count > 0)
                tabControl.SelectedIndex = 1;
            else
                tabControl.SelectedIndex = 0;
        }

        private void RefreshVersionList()
        {
            lstVersions.Items.Clear();
            if (Program.StartupManager?.VersionManager == null) return;

            Program.StartupManager.VersionManager.Refresh();
            foreach (var v in Program.StartupManager.VersionManager.AvailableVersions)
                lstVersions.Items.Add(new VersionListItem(v));

            pnlVersionDetails.IsVisible = lstVersions.Items.Count > 0;
        }

        private void LstVersions_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (lstVersions.SelectedItem is VersionListItem item)
            {
                var v = item.Version;
                pnlVersionDetails.IsVisible = true;
                lblVersionName.Text   = v.DisplayName ?? v.Version;
                lblExtractedDate.Text = $"Extracted: {v.ExtractedDate:yyyy-MM-dd HH:mm}";
                lblEncryption.Text    = $"Encryption: {v.Encryption}";
                lblFormat.Text        = $"Format: {GetFormatDetails(v)}";
                int totalImgs = v.Categories.Values.Sum(c => c.FileCount);
                lblImageCount.Text    = $"Total Images: {totalImgs:N0}";
                lblCategoryCount.Text = $"Categories: {v.Categories.Count}";
                lblFeatures.Text      = $"Info: {GetFeatures(v)}";

                if (!v.IsValid && v.ValidationErrors.Count > 0)
                {
                    lblValidation.Text = $"Warning: {v.ValidationErrors.First()}";
                    lblValidation.Foreground = Avalonia.Media.Brushes.OrangeRed;
                }
                else
                {
                    lblValidation.Text = "Status: Valid";
                    lblValidation.Foreground = Avalonia.Media.Brushes.Green;
                }
            }
            else
            {
                pnlVersionDetails.IsVisible = false;
            }
        }

        private static string GetFormatDetails(VersionInfo v)
        {
            var parts = new List<string>();
            if (v.IsBetaMs) parts.Add("Beta MapleStory");
            else if (v.IsPreBB) parts.Add("Pre-Big Bang");
            else if (v.IsBigBang2) parts.Add("Big Bang 2 / Chaos");
            else parts.Add("Post-Big Bang");
            if (v.Is64Bit) parts.Add("64-bit");
            return string.Join(", ", parts);
        }

        private static string GetFeatures(VersionInfo v)
        {
            var parts = new List<string>();
            if (v.PatchVersion > 0) parts.Add($"Patch v{v.PatchVersion}");
            if (!string.IsNullOrEmpty(v.SourceRegion)) parts.Add(v.SourceRegion);
            if (v.IsExternal) parts.Add("External");
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }

        private void OnVersionsChanged(object? sender, VersionsChangedEventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(RefreshVersionList);
        }

        // ── Buttons ──────────────────────────────────────────────────

        private async void BtnBrowseWz_Click(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the MapleStory folder",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;

            string path = folders[0].TryGetLocalPath() ?? string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                if (!cmbPath.Items.Contains(path))
                    cmbPath.Items.Add(path);
                cmbPath.SelectedItem = path;
            }
        }

        private async void BtnInitialize_Click(object? sender, RoutedEventArgs e)
        {
            if (_initialising) return;
            _initialising = true;
            btnInitialize.IsEnabled = false;

            try
            {
                bool ok = tabControl.SelectedIndex == 0
                    ? await InitializeFromWzFilesAsync()
                    : await InitializeFromImgVersionAsync();

                if (ok)
                {
                    var editor = new HaEditorWindow();
                    (Avalonia.Application.Current?.ApplicationLifetime
                        as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)!
                        .MainWindow = editor;
                    editor.Show();
                    Close();
                }
            }
            finally
            {
                _initialising = false;
                btnInitialize.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e) => Close();

        // ── WZ loading ───────────────────────────────────────────────

        private async Task<bool> InitializeFromWzFilesAsync()
        {
            string wzPath = cmbPath.SelectedItem?.ToString() ?? string.Empty;
            if (wzPath == "Select MapleStory Folder" || !Directory.Exists(wzPath))
            {
                await ShowError("Please select a valid MapleStory folder.");
                return false;
            }

            ApplicationSettings.MapleVersionIndex  = cmbVersion.SelectedIndex;
            ApplicationSettings.MapleFolderIndex   = cmbPath.SelectedIndex;
            if (cmbLocalisation.SelectedItem is LocalisationItem loc)
                ApplicationSettings.MapleStoryClientLocalisation = loc.Value;

            if (!ApplicationSettings.MapleFoldersList.Contains(wzPath))
                ApplicationSettings.MapleFoldersList = ApplicationSettings.MapleFoldersList == ""
                    ? wzPath : (ApplicationSettings.MapleFoldersList + "," + wzPath);

            var fileVersion = (WzMapleVersion)cmbVersion.SelectedIndex;

            var config = HaCreatorConfig.Load();
            config.DataSourceMode = DataSourceMode.WzFiles;
            config.Save();

            return await Task.Run(() => InitializeFromWzFilesInternal(wzPath, fileVersion));
        }

        private async Task<bool> InitializeFromImgVersionAsync()
        {
            if (lstVersions.SelectedItem is not VersionListItem item)
            {
                await ShowError("Please select a version from the list.");
                return false;
            }
            var version = item.Version;

            var config = HaCreatorConfig.Load();
            config.LastUsedVersion = version.Version;
            config.DataSourceMode  = DataSourceMode.ImgFileSystem;
            config.AddToRecentVersionPaths(version.DirectoryPath);
            config.Save();

            return await Task.Run(() => InitializeFromImgFileSystem(version));
        }

        private bool InitializeFromWzFilesInternal(string wzPath, WzMapleVersion fileVersion)
        {
            try
            {
                Program.WzManager?.Dispose();
                Program.WzManager = null;
                Program.InfoManager?.Clear();

                _wzMapleVersion = fileVersion;
                Program.WzManager = new WzFileManager(wzPath, false);
                Program.WzManager.BuildWzFileList();

                if (Program.WzManager.IsPreBBDataWzFormat)
                {
                    SetStatus("Loading Data.wz…");
                    Program.WzManager.LoadLegacyDataWzFile("Data", _wzMapleVersion);
                    ExtractAll_PreBB();
                    ImageFormatDetector.UsePreBigBangImageFormats = true;
                }
                else
                {
                    Program.WzManager.LoadListWzFile(_wzMapleVersion);
                    LoadWzCategory("string");
                    ExtractStringFile(false);
                    LoadWzCategory("mob");       ExtractMobFile();
                    LoadWzCategory("npc");       ExtractNpcFile();
                    LoadWzCategory("reactor");   ExtractReactorFile();
                    LoadWzCategory("sound");     ExtractSoundFile();
                    LoadWzCategory("quest");     ExtractQuestFile();
                    LoadWzCategory("character");
                    LoadWzCategory("skill");     ExtractSkillFile();
                    LoadWzCategory("item");      ExtractItemFile();
                    LoadWzCategory("map");
                    for (int i = 0; i <= 9; i++) LoadWzCategory($"map\\map\\map{i}");
                    LoadWzCategory("map\\tile");
                    LoadWzCategory("map\\obj");
                    LoadWzCategory("map\\back");
                    ExtractMapMarks();
                    ExtractMapPortals();
                    ExtractMapTileSets();
                    ExtractMapObjSets();
                    ExtractMapBackgroundSets();
                    ExtractMaps();
                    ImageFormatDetector.UsePreBigBangImageFormats = Program.IsPreBBDataWzFormat;
                    LoadWzCategory("ui");
                }
                SetStatus("Initialization complete.");
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.InvokeAsync(() => ShowError($"Error: {ex.Message}"));
                return false;
            }
        }

        private bool InitializeFromImgFileSystem(VersionInfo version)
        {
            try
            {
                SetStatus("Creating data source…");
                Program.WzManager?.Dispose();
                Program.WzManager = null;
                Program.DataSource?.Dispose();
                Program.DataSource = null;
                Program.InfoManager?.Clear();

                Program.DataSource = Program.StartupManager.CreateDataSource(version);

                if (Enum.TryParse<WzMapleVersion>(version.Encryption, out var mv))
                    _wzMapleVersion = mv;

                SetStatus("Extracting game data…");
                var extractor = new ImgDataExtractor(Program.DataSource, Program.InfoManager);
                extractor.ProgressChanged += (_, args) => SetStatus(args.Message);
                extractor.ExtractAll();

                ImageFormatDetector.UsePreBigBangImageFormats = Program.IsPreBBDataWzFormat;
                SetStatus("Initialization complete.");
                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.InvokeAsync(() => ShowError($"Error: {ex.Message}"));
                return false;
            }
        }

        // ── WZ extraction helpers (mirrors Initialization.cs) ────────

        private void LoadWzCategory(string dir)
        {
            string normDir = dir.Replace("\\", "/");
            SetStatus($"Loading {Path.GetFileName(dir)}.wz…");
            foreach (string name in Program.WzManager.GetWzFileNameListFromBase(dir))
                Program.WzManager.LoadWzFile(name, _wzMapleVersion);
            WzFileManager.fileManager.LoadCanvasSection(normDir, _wzMapleVersion);
        }

        private void ExtractAll_PreBB()
        {
            ExtractStringFile(true);
            ExtractMobFile(); ExtractNpcFile(); ExtractReactorFile();
            ExtractSoundFile(); ExtractQuestFile(); ExtractSkillFile(); ExtractItemFile();
            ExtractMapMarks(); ExtractMapPortals(); ExtractMapTileSets();
            ExtractMapObjSets(); ExtractMapBackgroundSets(); ExtractMaps();
        }

        private void ExtractStringFile(bool preBB) => WzFileExtractor.ExtractStringFile(preBB);
        private void ExtractMobFile()    => WzFileExtractor.ExtractMobFile();
        private void ExtractNpcFile()    => WzFileExtractor.ExtractNpcFile();
        private void ExtractReactorFile() => WzFileExtractor.ExtractReactorFile();
        private void ExtractSoundFile()  => WzFileExtractor.ExtractSoundFile();
        private void ExtractQuestFile()  => WzFileExtractor.ExtractQuestFile();
        private void ExtractSkillFile()  => WzFileExtractor.ExtractSkillFile();
        private void ExtractItemFile()   => WzFileExtractor.ExtractItemFile();
        private void ExtractMapMarks()   => WzFileExtractor.ExtractMapMarks();
        private void ExtractMapPortals() => WzFileExtractor.ExtractMapPortals();
        private void ExtractMapTileSets()       => WzFileExtractor.ExtractMapTileSets();
        private void ExtractMapObjSets()        => WzFileExtractor.ExtractMapObjSets();
        private void ExtractMapBackgroundSets() => WzFileExtractor.ExtractMapBackgroundSets();
        private void ExtractMaps()       => WzFileExtractor.ExtractMaps();

        // ── UI helpers ───────────────────────────────────────────────

        private void SetStatus(string msg) =>
            Dispatcher.UIThread.InvokeAsync(() => txtStatus.Text = msg);

        private Task ShowError(string msg) =>
            new Window
            {
                Title = "Error",
                Width = 400, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16), Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = msg, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                    }
                }
            }.ShowDialog(this);

        // ── Helper types ─────────────────────────────────────────────

        private record LocalisationItem(string Text, int Value)
        {
            public override string ToString() => Text;
        }

        internal class VersionListItem
        {
            public VersionInfo Version { get; }
            public VersionListItem(VersionInfo v) => Version = v;
            public override string ToString()
            {
                string name = Version.DisplayName ?? Version.Version;
                var tags = new List<string>();
                if (Version.IsBetaMs) tags.Add("Beta");
                else if (Version.IsPreBB) tags.Add("Pre-BB");
                if (Version.PatchVersion > 0 && !name.Contains($"v{Version.PatchVersion}"))
                    tags.Add($"v{Version.PatchVersion}");
                if (!string.IsNullOrEmpty(Version.SourceRegion) &&
                    !name.ToUpper().Contains(Version.SourceRegion.ToUpper()))
                    tags.Add(Version.SourceRegion);
                return tags.Count > 0 ? $"{name} [{string.Join(", ", tags)}]" : name;
            }
        }
    }
}
