using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaRepacker.GUI.Input;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using System;
using System.Collections.Generic;
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

        // ── Export ───────────────────────────────────────────────────────────

        private async void OnExportXmlClick(object? sender, RoutedEventArgs e)
        {
            var (wzFiles, outDir, version) = await PickFilesAndDir("Export WZ as XML");
            if (wzFiles == null) return;

            SetStatus("Exporting XML…");
            await Task.Run(() =>
            {
                var cfg = Program.ConfigurationManager;
                int indent = cfg?.UserSettings?.Indentation ?? 4;
                var lineBreak = cfg?.UserSettings?.LineBreakType ?? LineBreak.None;
                var serializer = new WzClassicXmlSerializer(indent, lineBreak, false);
                WzFileExporter.RunWzFilesExtraction(wzFiles, outDir, version, serializer);
            });
            SetStatus("XML export complete.");
        }

        private async void OnExportImgClick(object? sender, RoutedEventArgs e)
        {
            var (wzFiles, outDir, version) = await PickFilesAndDir("Export WZ as IMG");
            if (wzFiles == null) return;

            SetStatus("Exporting IMG…");
            await Task.Run(() =>
            {
                var serializer = new WzImgSerializer();
                WzFileExporter.RunWzFilesExtraction(wzFiles, outDir, version, serializer);
            });
            SetStatus("IMG export complete.");
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
