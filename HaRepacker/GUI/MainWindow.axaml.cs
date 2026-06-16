using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaRepacker.GUI.Input;
using HaRepacker.GUI.Panels;
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

            // Restore saved window size/state
            var appSettings = Program.ConfigurationManager?.ApplicationSettings;
            if (appSettings != null)
            {
                Width = appSettings.Width > 0 ? appSettings.Width : 1280;
                Height = appSettings.Height > 0 ? appSettings.Height : 800;
                if (appSettings.WindowMaximized)
                    WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            // Create initial tab
            var initialPanel = CreateNewTab("New Tab");

            if (wzToLoad != null)
            {
                initialPanel.OpenFile(wzToLoad);
                UpdateActiveTabTitle(Path.GetFileName(wzToLoad));
            }

            if (firstRun)
                Opened += async (_, _) => await FirstRunFormWindow.ShowAsync(this);

            // Persist window size/state changes
            this.PropertyChanged += (_, args) =>
            {
                if (Program.ConfigurationManager == null) return;
                if (args.Property == ClientSizeProperty)
                {
                    if (WindowState != Avalonia.Controls.WindowState.Maximized)
                    {
                        Program.ConfigurationManager.ApplicationSettings.Width = (int)ClientSize.Width;
                        Program.ConfigurationManager.ApplicationSettings.Height = (int)ClientSize.Height;
                    }
                }
                else if (args.Property == WindowStateProperty)
                {
                    Program.ConfigurationManager.ApplicationSettings.WindowMaximized =
                        WindowState == Avalonia.Controls.WindowState.Maximized;
                }
            };
        }

        // ── Tab management ────────────────────────────────────────────────────

        private MainPanel? ActivePanel =>
            (tabControl.SelectedItem as TabItem)?.Content as MainPanel;

        private MainPanel CreateNewTab(string title = "New Tab")
        {
            var panel = new MainPanel();

            var titleBlock = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeBtn = new Button
            {
                Content = "×",
                Padding = new Avalonia.Thickness(3, 0),
                Margin = new Avalonia.Thickness(6, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                FontSize = 12
            };
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(titleBlock);
            header.Children.Add(closeBtn);

            var tab = new TabItem { Header = header, Content = panel, Tag = titleBlock };
            closeBtn.Click += (_, _) => CloseTab(tab);

            tabControl.Items.Add(tab);
            tabControl.SelectedItem = tab;
            return panel;
        }

        private void CloseTab(TabItem tab)
        {
            // Unload all WZ files in this tab's panel
            if (tab.Content is MainPanel panel)
            {
                foreach (var node in panel.GetRootNodes().ToList())
                    if (node.WzObject is WzFile wz)
                        panel.UnloadWzFile(wz);
            }

            int idx = tabControl.Items.IndexOf(tab);
            tabControl.Items.Remove(tab);

            if (tabControl.Items.Count == 0)
                CreateNewTab();
            else
                tabControl.SelectedIndex = Math.Min(idx, tabControl.Items.Count - 1);
        }

        private void UpdateActiveTabTitle(string name)
        {
            if (tabControl.SelectedItem is TabItem tab && tab.Tag is TextBlock tb)
                tb.Text = name;
        }

        private void OnNewTabClick(object? sender, RoutedEventArgs e)
            => CreateNewTab();

        private void OnCloseTabClick(object? sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is TabItem tab)
                CloseTab(tab);
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
                    new FilePickerFileType("ZLZ Encryption DLL") { Patterns = new[] { "ZLZ.dll", "ZLZ64.dll" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
            if (files.Count == 0) return;

            // Handle ZLZ.dll / ZLZ64.dll separately
            var zlzFiles = files.Where(f =>
            {
                string n = Path.GetFileName(f.TryGetLocalPath() ?? f.Path.LocalPath);
                return n.Equals("ZLZ.dll", StringComparison.OrdinalIgnoreCase) ||
                       n.Equals("ZLZ64.dll", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            var wzFiles = files.Except(zlzFiles).ToList();

            foreach (var zlz in zlzFiles)
            {
                string path = zlz.TryGetLocalPath() ?? zlz.Path.LocalPath;
                var win = new ZLZPacketEncryptionKeyWindow();
                win.LoadZLZ(path);
                await win.ShowDialog(this);
            }

            if (wzFiles.Count == 0) return;

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select Encryption Type");
            if (!ok) return;

            var panel = ActivePanel ?? CreateNewTab();
            foreach (var file in wzFiles)
            {
                string path = file.TryGetLocalPath() ?? file.Path.LocalPath;
                panel.OpenFile(path, version);
                UpdateActiveTabTitle(Path.GetFileName(path));
            }
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
        {
            var panel = ActivePanel ?? CreateNewTab();
            await NewFormWindow.ShowAsync(this, panel);
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            if (panel == null) return;
            if (panel.SelectedNode is not WzNode node)
            { Warning.Error("Please select a WZ file or image node first."); return; }
            if (node.WzObject is not WzFile && node.WzObject is not WzImage)
            { Warning.Error("Please select a root WZ file or WZ image node to save."); return; }
            await SaveFormWindow.ShowAsync(this, panel, node);
        }

        private void OnReloadAllClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            if (panel == null) return;
            foreach (var node in panel.GetRootNodes().ToList())
                if (node.WzObject is WzFile wz)
                    panel.ReloadWzFile(wz);
        }

        private void OnUnloadAllClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            if (panel == null) return;
            if (!Warning.Warn("Unload all WZ files in this tab?")) return;
            foreach (var node in panel.GetRootNodes().ToList())
                if (node.WzObject is WzFile wz)
                    panel.UnloadWzFile(wz);
            UpdateActiveTabTitle("New Tab");
        }

        private async void OnOptionsClick(object? sender, RoutedEventArgs e)
            => await OptionsFormWindow.ShowAsync(this);

        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

        // ── Edit ─────────────────────────────────────────────────────────────

        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => ActivePanel?.UndoMan?.Undo();

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => ActivePanel?.UndoMan?.Redo();

        private void OnRemoveSelectedClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            if (panel?.UndoMan == null) return;
            panel.PromptRemoveSelectedTreeNodes(panel.UndoMan);
        }

        private void OnCopyClick(object? sender, RoutedEventArgs e)
            => ActivePanel?.DoCopy();

        private async void OnPasteClick(object? sender, RoutedEventArgs e)
        {
            if (ActivePanel is { } panel)
                await panel.DoPasteAsync(this);
        }

        private void OnExpandAllClick(object? sender, RoutedEventArgs e)
            => ActivePanel?.ExpandAllNodes(true);

        private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
            => ActivePanel?.ExpandAllNodes(false);

        // ── Drag and drop ─────────────────────────────────────────────────────

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void OnFileDrop(object? sender, DragEventArgs e)
        {
            var files = e.Data.GetFiles();
            if (files == null) return;

            var paths = files
                .Select(f => f.TryGetLocalPath())
                .Where(p => p != null && p.EndsWith(".wz", StringComparison.OrdinalIgnoreCase))
                .Cast<string>()
                .ToList();
            if (paths.Count == 0) return;

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select Encryption Type");
            if (!ok) return;

            var panel = ActivePanel ?? CreateNewTab();
            foreach (var path in paths)
            {
                panel.OpenFile(path, version);
                UpdateActiveTabTitle(Path.GetFileName(path));
            }
        }

        // ── Export ───────────────────────────────────────────────────────────

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

        private async void OnExportSelectedXmlClick(object? sender, RoutedEventArgs e)
        {
            var node = ActivePanel?.SelectedNode;
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
            var node = ActivePanel?.SelectedNode;
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
            Func<UserSettings, IWzImageSerializer> makeSerializer)
        {
            string outDir = await SavedFolderBrowser.ShowAsync(this, title);
            if (string.IsNullOrEmpty(outDir)) return;

            var (dirs, imgs) = GetSelectedDirsAndImgs();
            if (dirs.Count == 0 && imgs.Count == 0) { Warning.Error("Please select WZ file, directory, or image nodes."); return; }

            SetStatus("Exporting…");
            var cfg = Program.ConfigurationManager?.UserSettings ?? new UserSettings();
            var serializer = makeSerializer(cfg);
            await Task.Run(() => WzFileExporter.RunWzImgDirsExtraction(dirs, imgs, outDir, serializer));
            SetStatus("Export complete.");
        }

        private (List<WzDirectory> dirs, List<WzImage> imgs) GetSelectedDirsAndImgs()
        {
            var dirs = new List<WzDirectory>();
            var imgs = new List<WzImage>();
            var node = ActivePanel?.SelectedNode;
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
            var panel = ActivePanel;
            var node = panel?.SelectedNode;
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
            await RunImporterAsync(panel!, node, paths, deserializer, null);
            SetStatus("XML import complete.");
        }

        private async void OnImportImgClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            var node = panel?.SelectedNode;
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
            await RunImporterAsync(panel!, node, paths, deserializer, iv);
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

        private async Task RunImporterAsync(MainPanel panel, WzNode parentNode, string[] filePaths,
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
                    parentNode.AddObject(obj, panel.UndoMan);
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

        // ── FH Mapper ─────────────────────────────────────────────────────────

        private void OnFHMapperClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            if (panel?.SelectedNode?.WzObject is not WzImage img)
            {
                Warning.Error("Please select a map .img node (e.g. 100000000.img) first.");
                return;
            }

            var mapper = new FHMapper.FHMapper(panel);
            mapper.ParseSettings();

            double zoom = 1.0;
            if (mapper.settings.Count >= 16 && (bool)mapper.settings[15])
                double.TryParse((string)mapper.settings[14], out zoom);

            var errors = new List<string>();
            if (!mapper.TryRenderMapAndSave(img, zoom, ref errors) && errors.Count > 0)
                Warning.Error(string.Join("\n", errors.Take(10)));
        }

        private async void OnFHMapperSettingsClick(object? sender, RoutedEventArgs e)
        {
            var panel = ActivePanel;
            var mapper = new FHMapper.FHMapper(panel ?? new GUI.Panels.MainPanel());
            mapper.ParseSettings();
            var dlg = new FHMapper.FHSettingsWindow(mapper, mapper.settings);
            await dlg.ShowDialog(this);
        }

        // ── Help ─────────────────────────────────────────────────────────────

        private async void OnAboutClick(object? sender, RoutedEventArgs e)
            => await AboutFormWindow.ShowAsync(this);

        // ── Status bar helper ─────────────────────────────────────────────────

        private void SetStatus(string text)
            => Dispatcher.UIThread.Post(() => statusLabel.Text = text);
    }
}
