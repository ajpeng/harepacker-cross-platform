using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaRepacker.GUI.Input;
using HaRepacker.Models;
using MapleLib.Configuration;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class MainWindow : Window
    {
        public MainWindow(string? wzToLoad = null, bool firstRun = false)
        {
            InitializeComponent();

            if (wzToLoad != null)
                mainPanel.OpenFile(wzToLoad);

            if (firstRun)
                _ = FirstRunFormWindow.ShowAsync(this);
        }

        // ── File ──────────────────────────────────────────────────────────────

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open WZ File",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
            if (files.Count == 0) return;

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select Encryption Type");
            if (!ok) return;

            foreach (var file in files)
                mainPanel.OpenFile(file.TryGetLocalPath() ?? file.Path.LocalPath, version);
        }

        private async void OnOpenVersionDirClick(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Version Directory (IMG filesystem)",
                AllowMultiple = false,
            });
            if (folders.Count == 0) return;
            string dir = folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
            PackToWzFormWindow.ShowWindow(this, dir);
        }

        private async void OnNewClick(object? sender, RoutedEventArgs e)
            => await NewFormWindow.ShowAsync(this, mainPanel);

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (mainPanel.SelectedNode is not Models.WzNode node)
            { Warning.Error("Please select a WZ file or image node first."); return; }
            if (node.WzObject is not WzFile && node.WzObject is not WzImage)
            { Warning.Error("Please select a root WZ file or WZ image node to save."); return; }
            await SaveFormWindow.ShowAsync(this, mainPanel, node);
        }

        private void OnReloadAllClick(object? sender, RoutedEventArgs e)
        {
            var nodes = mainPanel.GetRootNodes().ToList();
            foreach (var node in nodes)
            {
                if (node.WzObject is WzFile wz)
                    mainPanel.ReloadWzFile(wz);
            }
        }

        private void OnUnloadAllClick(object? sender, RoutedEventArgs e)
        {
            if (!Warning.Warn("Unload all WZ files?")) return;
            var nodes = mainPanel.GetRootNodes().ToList();
            foreach (var node in nodes)
            {
                if (node.WzObject is WzFile wz)
                    mainPanel.UnloadWzFile(wz);
            }
        }

        private async void OnOptionsClick(object? sender, RoutedEventArgs e)
            => await OptionsFormWindow.ShowAsync(this);

        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

        // ── Edit ─────────────────────────────────────────────────────────────

        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => mainPanel.UndoMan?.Undo();

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => mainPanel.UndoMan?.Redo();

        private void OnRemoveSelectedClick(object? sender, RoutedEventArgs e)
        {
            if (mainPanel.UndoMan == null) return;
            mainPanel.PromptRemoveSelectedTreeNodes(mainPanel.UndoMan);
        }

        private void OnExpandAllClick(object? sender, RoutedEventArgs e)
            => mainPanel.ExpandAllNodes(true);

        private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
            => mainPanel.ExpandAllNodes(false);

        // ── Export ───────────────────────────────────────────────────────────

        // ── Export from WZ files on disk ─────────────────────────────────────

        private async void OnExportXmlClick(object? sender, RoutedEventArgs e)
        {
            var (wzFiles, outDir, version) = await PickFilesAndDir("Export WZ as Classic XML");
            if (wzFiles == null) return;
            SetStatus("Exporting XML…");
            var cfg = Program.ConfigurationManager;
            int indent = cfg?.UserSettings?.Indentation ?? 4;
            var lineBreak = cfg?.UserSettings?.LineBreakType ?? LineBreak.None;
            await Task.Run(() => WzFileExporter.RunWzFilesExtraction(wzFiles, outDir, version,
                new WzClassicXmlSerializer(indent, lineBreak, false)));
            SetStatus("XML export complete.");
        }

        private async void OnExportImgClick(object? sender, RoutedEventArgs e)
        {
            var (wzFiles, outDir, version) = await PickFilesAndDir("Export WZ as IMG");
            if (wzFiles == null) return;
            SetStatus("Exporting IMG…");
            await Task.Run(() => WzFileExporter.RunWzFilesExtraction(wzFiles, outDir, version, new WzImgSerializer()));
            SetStatus("IMG export complete.");
        }

        private async void OnExportWzPngClick(object? sender, RoutedEventArgs e)
        {
            var (wzFiles, outDir, version) = await PickFilesAndDir("Export WZ as PNGs/MP3s");
            if (wzFiles == null) return;
            SetStatus("Exporting PNGs/MP3s…");
            await Task.Run(() => WzFileExporter.RunWzFilesExtraction(wzFiles, outDir, version, new WzPngMp3Serializer()));
            SetStatus("PNG/MP3 export complete.");
        }

        // ── Export selected node ──────────────────────────────────────────────

        private async void OnExportSelectedXmlClick(object? sender, RoutedEventArgs e)
        {
            var node = mainPanel.SelectedNode;
            if (node?.WzObject == null) { Warning.Error("Please select a node to export."); return; }

            var saveResult = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Selected Node as New XML",
                SuggestedFileName = node.Name + ".xml",
                FileTypeChoices = new[] { new FilePickerFileType("XML Files") { Patterns = new[] { "*.xml" } } }
            });
            if (saveResult == null) return;
            string outPath = saveResult.TryGetLocalPath() ?? saveResult.Path.LocalPath;

            SetStatus("Exporting selected node as XML…");
            var objs = new List<WzObject> { node.WzObject };
            var cfg = Program.ConfigurationManager;
            int indent = cfg?.UserSettings?.Indentation ?? 4;
            var lineBreak = cfg?.UserSettings?.LineBreakType ?? LineBreak.None;
            await Task.Run(() => WzFileExporter.RunWzXmlExtraction(objs, outPath, new WzNewXmlSerializer(indent, lineBreak)));
            SetStatus("XML export complete.");
        }

        private async void OnExportSelectedClassicXmlClick(object? sender, RoutedEventArgs e)
            => await ExportSelectedDirsImgs("Export Selected as Classic XML (with metadata)",
                cfg => new WzClassicXmlSerializer(cfg.Indentation, cfg.LineBreakType, true));

        private async void OnExportSelectedPrivateSrvXmlClick(object? sender, RoutedEventArgs e)
            => await ExportSelectedDirsImgs("Export Selected as Private Server XML",
                cfg => new WzClassicXmlSerializer(cfg.Indentation, cfg.LineBreakType, false));

        private async void OnExportSelectedImgClick(object? sender, RoutedEventArgs e)
            => await ExportSelectedDirsImgs("Export Selected as IMG", _ => new WzImgSerializer());

        private async void OnExportSelectedPngClick(object? sender, RoutedEventArgs e)
        {
            var node = mainPanel.SelectedNode;
            if (node?.WzObject == null) { Warning.Error("Please select a node to export."); return; }
            string outDir = await SavedFolderBrowser.ShowAsync(this, "Select output directory for PNGs/MP3s");
            if (string.IsNullOrEmpty(outDir)) return;
            SetStatus("Exporting PNGs/MP3s…");
            var wzObj = node.WzObject;
            await Task.Run(() => new WzPngMp3Serializer().SerializeObject(wzObj, outDir));
            SetStatus("PNG/MP3 export complete.");
        }

        private async void OnExportSelectedJsonClick(object? sender, RoutedEventArgs e)
            => await ExportSelectedBsonJson(isJson: true);

        private async void OnExportSelectedBsonClick(object? sender, RoutedEventArgs e)
            => await ExportSelectedBsonJson(isJson: false);

        private async Task ExportSelectedBsonJson(bool isJson)
        {
            string outDir = await SavedFolderBrowser.ShowAsync(this, $"Select output directory for {(isJson ? "JSON" : "BSON")}");
            if (string.IsNullOrEmpty(outDir)) return;

            var (ok, includeBase64) = await Warning.AskYesNo(this,
                "Include base64-encoded binary data (images/sounds)?",
                "Export Options");
            if (!ok) return;

            var cfg = Program.ConfigurationManager;
            int indent = cfg?.UserSettings?.Indentation ?? 4;
            var lineBreak = cfg?.UserSettings?.LineBreakType ?? LineBreak.None;

            var (dirs, imgs) = GetSelectedDirsAndImgs();
            if (dirs.Count == 0 && imgs.Count == 0) { Warning.Error("Please select WZ file, directory, or image nodes."); return; }

            SetStatus($"Exporting {(isJson ? "JSON" : "BSON")}…");
            await Task.Run(() =>
                WzFileExporter.RunWzImgDirsExtraction(dirs, imgs, outDir,
                    new WzJsonBsonSerializer(indent, lineBreak, includeBase64, isJson)));
            SetStatus($"{(isJson ? "JSON" : "BSON")} export complete.");
        }

        private async Task ExportSelectedDirsImgs(string title,
            Func<MapleLib.Configuration.UserSettings, IWzImageSerializer> makeSerializer)
        {
            string outDir = await SavedFolderBrowser.ShowAsync(this, title);
            if (string.IsNullOrEmpty(outDir)) return;

            var (dirs, imgs) = GetSelectedDirsAndImgs();
            if (dirs.Count == 0 && imgs.Count == 0) { Warning.Error("Please select WZ file, directory, or image nodes."); return; }

            SetStatus($"Exporting…");
            var cfg = Program.ConfigurationManager?.UserSettings ?? new MapleLib.Configuration.UserSettings();
            var serializer = makeSerializer(cfg);
            await Task.Run(() => WzFileExporter.RunWzImgDirsExtraction(dirs, imgs, outDir, serializer));
            SetStatus("Export complete.");
        }

        private (List<WzDirectory> dirs, List<WzImage> imgs) GetSelectedDirsAndImgs()
        {
            var dirs = new List<WzDirectory>();
            var imgs = new List<WzImage>();
            var node = mainPanel.SelectedNode;
            if (node?.WzObject == null) return (dirs, imgs);
            if (node.WzObject is WzDirectory dir) dirs.Add(dir);
            else if (node.WzObject is WzImage img) imgs.Add(img);
            else if (node.WzObject is WzFile file) dirs.Add(file.WzDirectory);
            return (dirs, imgs);
        }

        private async Task<(string[]? files, string dir, WzMapleVersion version)> PickFilesAndDir(string title)
        {
            var fileResults = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"{title} — Select WZ Files",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } } }
            });
            if (fileResults.Count == 0) return (null, "", WzMapleVersion.BMS);

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select Encryption Type");
            if (!ok) return (null, "", WzMapleVersion.BMS);

            string outDir = await SavedFolderBrowser.ShowAsync(this, "Select output directory");
            if (string.IsNullOrEmpty(outDir)) return (null, "", WzMapleVersion.BMS);

            string[] paths = fileResults.Select(f => f.TryGetLocalPath() ?? f.Path.LocalPath).ToArray();
            return (paths, outDir, version);
        }

        // ── Import ────────────────────────────────────────────────────────────

        private async void OnImportXmlClick(object? sender, RoutedEventArgs e)
        {
            var node = mainPanel.SelectedNode;
            if (!IsValidImportTarget(node)) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import XML Files into Selected Node",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("XML Files") { Patterns = new[] { "*.xml" } } }
            });
            if (files.Count == 0) return;

            var wzFile = node!.WzObject!.WzFileParent;
            if (wzFile == null) return;
            var deserializer = new WzXmlDeserializer(true, WzTool.GetIvByMapleVersion(wzFile.MapleVersion));
            string[] paths = files.Select(f => f.TryGetLocalPath() ?? f.Path.LocalPath).ToArray();

            SetStatus("Importing XML…");
            await RunImporterAsync(node, paths, deserializer, null);
            SetStatus("XML import complete.");
        }

        private async void OnImportImgClick(object? sender, RoutedEventArgs e)
        {
            var node = mainPanel.SelectedNode;
            if (!IsValidImportTarget(node)) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import IMG Files into Selected Node",
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("WZ IMG Files") { Patterns = new[] { "*.img" } } }
            });
            if (files.Count == 0) return;

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select IMG Encryption Type");
            if (!ok) return;

            var deserializer = new WzImgDeserializer(true);
            byte[] iv = WzTool.GetIvByMapleVersion(version);
            string[] paths = files.Select(f => f.TryGetLocalPath() ?? f.Path.LocalPath).ToArray();

            SetStatus("Importing IMG…");
            await RunImporterAsync(node, paths, deserializer, iv);
            SetStatus("IMG import complete.");
        }

        private static bool IsValidImportTarget(WzNode? node)
        {
            if (node?.WzObject == null)
            { Warning.Error("Please select a WZ directory, file, or container node first."); return false; }
            if (node.WzObject is not WzDirectory && node.WzObject is not WzFile && node.WzObject is not IPropertyContainer)
            { Warning.Error("Selected node must be a WZ directory, file, or property container."); return false; }
            if (node.WzObject.WzFileParent == null)
            { Warning.Error("Could not find the parent WZ file for the selected node."); return false; }
            return true;
        }

        private async Task RunImporterAsync(WzNode parentNode, string[] filePaths,
            ProgressingWzSerializer deserializer, byte[]? iv)
        {
            ReplaceResult replaceAll = ReplaceResult.NoneSelectedYet;

            foreach (string path in filePaths)
            {
                List<WzObject> parsed;
                try
                {
                    if (deserializer is WzXmlDeserializer xmlDs)
                        parsed = await Task.Run(() => xmlDs.ParseXML(path));
                    else if (deserializer is WzImgDeserializer imgDs)
                    {
                        var img = await Task.Run(() =>
                            imgDs.WzImageFromIMGFile(path, iv!, Path.GetFileName(path), out bool ok));
                        if (img == null) continue;
                        parsed = new List<WzObject> { img };
                    }
                    else continue;
                }
                catch (Exception ex)
                {
                    Warning.Error($"Error reading \"{path}\":\n{ex.Message}");
                    continue;
                }

                foreach (WzObject obj in parsed)
                {
                    var existing = WzNode.GetChildNode(parentNode, obj.Name);
                    if (existing != null)
                    {
                        bool replace;
                        if (replaceAll == ReplaceResult.YesToAll) replace = true;
                        else if (replaceAll == ReplaceResult.NoToAll) replace = false;
                        else
                        {
                            var (_, result) = await InputDialogs.ShowReplaceAsync(this, obj.Name);
                            if (result == ReplaceResult.YesToAll) replaceAll = ReplaceResult.YesToAll;
                            else if (result == ReplaceResult.NoToAll) replaceAll = ReplaceResult.NoToAll;
                            replace = result == ReplaceResult.Yes || result == ReplaceResult.YesToAll;
                        }
                        if (!replace) continue;
                        existing.DeleteNode();
                        parentNode.Nodes.Remove(existing);
                    }
                    parentNode.AddObject(obj, mainPanel.UndoMan);
                }
            }
            MapleLib.Helpers.ErrorLogger.SaveToFile("WzImport_Errors.txt");
        }

        // ── Tools ─────────────────────────────────────────────────────────────

        private void OnWzStringSearchClick(object? sender, RoutedEventArgs e)
            => WzStringSearchFormWindow.ShowWindow(this, null, null);

        private void OnWzKeyBruteforceClick(object? sender, RoutedEventArgs e)
            => WzKeyBruteforceFormWindow.ShowWindow(this);

        private async void OnCustomEncryptionClick(object? sender, RoutedEventArgs e)
            => await CustomWZEncryptionWindow.ShowAsync(this);

        private void OnPackToWzClick(object? sender, RoutedEventArgs e)
            => Warning.Error("Please use File > Open Version Directory… to select the IMG filesystem directory first.");

        // ── Help ─────────────────────────────────────────────────────────────

        private async void OnAboutClick(object? sender, RoutedEventArgs e)
            => await AboutFormWindow.ShowAsync(this);

        // ── Status bar helper ─────────────────────────────────────────────────

        private void SetStatus(string text)
            => Dispatcher.UIThread.Post(() => statusLabel.Text = text);
    }
}
