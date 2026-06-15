using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HaRepacker.GUI.Panels;
using HaRepacker.Models;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib;
using MapleLib.WzLib.MSFile;
using MapleLib.WzLib.Util;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class SaveFormWindow : Window
    {
        private readonly MainPanel _panel;
        private readonly WzNode _node;
        private readonly WzFile? _wzf;
        private readonly WzImage? _wzImg;
        private readonly bool _isRegularWzFile;

        private static readonly string[] EncryptionLabels = { "GMS", "EMS", "BMS (Unencrypted/Modern)", "Custom" };
        private static readonly WzMapleVersion[] EncryptionVersions = { WzMapleVersion.GMS, WzMapleVersion.EMS, WzMapleVersion.BMS, WzMapleVersion.CUSTOM };

        public SaveFormWindow(MainPanel panel, WzNode node)
        {
            InitializeComponent();
            _panel = panel;
            _node = node;

            foreach (var label in EncryptionLabels) encryptionBox.Items.Add(label);

            if (node.WzObject is WzImage image)
            {
                _wzImg = image;
                _isRegularWzFile = false;
                SelectEncryption(WzMapleVersion.BMS);
                versionBox.IsEnabled = false;
                check64Bit.IsEnabled = false;
            }
            else if (node.WzObject is WzFile wzf)
            {
                _wzf = wzf;
                _isRegularWzFile = true;
                SelectEncryption(wzf.MapleVersion);
                versionBox.Value = wzf.Version;
                check64Bit.IsChecked = wzf.Is64BitWzFile;
                versionBox.IsEnabled = !wzf.Is64BitWzFile;
            }
            UpdateUiState();
        }

        public static Task ShowAsync(Window owner, MainPanel panel, WzNode node)
        {
            var dlg = new SaveFormWindow(panel, node);
            return dlg.ShowDialog(owner);
        }

        private void SelectEncryption(WzMapleVersion version)
        {
            int idx = Array.IndexOf(EncryptionVersions, version);
            encryptionBox.SelectedIndex = idx >= 0 ? idx : 2;
        }

        private WzMapleVersion SelectedEncryption =>
            encryptionBox.SelectedIndex >= 0 && encryptionBox.SelectedIndex < EncryptionVersions.Length
                ? EncryptionVersions[encryptionBox.SelectedIndex]
                : WzMapleVersion.BMS;

        private void UpdateUiState()
        {
            panelWzOptions.IsEnabled = radioWzFile.IsChecked == true;
            if (check64Bit.IsChecked == true)
            {
                versionBox.Value = 777;
                versionBox.IsEnabled = false;
            }
            else
            {
                versionBox.IsEnabled = true;
            }
        }

        private void OnFormatChanged(object? sender, RoutedEventArgs e) => UpdateUiState();
        private void On64BitChanged(object? sender, RoutedEventArgs e) => UpdateUiState();

        private void OnEncryptionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var ver = SelectedEncryption;
            if (ver == WzMapleVersion.CUSTOM)
                Program.ConfigurationManager?.SetCustomWzUserKeyFromConfig();
            else
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            bool saveAsWz = radioWzFile.IsChecked == true;
            var version = SelectedEncryption;

            if (saveAsWz)
            {
                var files = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save WZ File",
                    SuggestedFileName = _node.Name,
                    FileTypeChoices = new[] { new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } } }
                });
                if (files == null) return;
                string outPath = files.TryGetLocalPath() ?? files.Path.LocalPath;

                bool save64bit = check64Bit.IsChecked == true;
                try
                {
                    if (_isRegularWzFile && _wzf != null)
                    {
                        if (_wzf.MapleVersion != version) PrepareAllImgs(_wzf.WzDirectory);
                        _wzf.Version = (short)(versionBox.Value ?? 1);
                        _wzf.MapleVersion = version;

                        if (string.Equals(_wzf.FilePath, outPath, StringComparison.OrdinalIgnoreCase))
                        {
                            string tmp = outPath + "$tmp";
                            _wzf.SaveToDisk(tmp, save64bit, version);
                            _panel.RemoveRootNode(_wzf);
                            try { File.Delete(outPath); File.Move(tmp, outPath); }
                            catch (IOException ex) { Warning.Error($"Error overwriting: {ex.Message}"); return; }
                        }
                        else
                        {
                            _wzf.SaveToDisk(outPath, save64bit, version);
                            _panel.RemoveRootNode(_wzf);
                        }

                        var reloaded = Program.WzFileManager?.LoadWzFile(outPath, version);
                        if (reloaded != null) _panel.AddRootNode(reloaded);
                    }
                    else if (_wzImg != null)
                    {
                        byte[] iv = WzTool.GetIvByMapleVersion(version);
                        string tmp = outPath + ".tmp";
                        using (var fs = File.Open(tmp, FileMode.OpenOrCreate))
                        using (var writer = new WzBinaryWriter(fs, iv))
                            _wzImg.SaveImage(writer, true);
                        try { File.Copy(tmp, outPath, true); File.Delete(tmp); } catch { }

                        _node.DeleteNode();
                        _panel.RemoveRootNode(_wzImg);
                        var reloaded = Program.WzFileManager?.LoadDataWzHotfixFile(outPath, version);
                        if (reloaded != null) _panel.AddRootNode(reloaded);
                    }
                }
                catch (Exception ex) { Warning.Error($"Save error: {ex.Message}"); return; }
            }
            else
            {
                // Save as .ms file
                if (_wzf == null) { Warning.Error("Can only save WZ files as .ms format."); return; }
                var files = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save MS File",
                    SuggestedFileName = _node.Name.Replace(".wz", ".ms"),
                    FileTypeChoices = new[] { new FilePickerFileType("MS Files") { Patterns = new[] { "*.ms" } } }
                });
                if (files == null) return;
                string outPath = files.TryGetLocalPath() ?? files.Path.LocalPath;
                try
                {
                    using var ms = new MemoryStream();
                    var msFile = new WzMsFile(ms, Path.GetFileName(outPath), outPath, true, isSavingFile: true);
                    var saved = msFile.Save(_wzf);
                    using var fs = new FileStream(outPath, FileMode.OpenOrCreate);
                    saved.CopyTo(fs);
                }
                catch (Exception ex) { Warning.Error($"Save error: {ex.Message}"); return; }
            }
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

        private static void PrepareAllImgs(WzDirectory dir)
        {
            foreach (var img in dir.WzImages) img.Changed = true;
            foreach (var sub in dir.WzDirectories) PrepareAllImgs(sub);
        }
    }
}
