using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class PackToWzFormWindow : Window
    {
        private static readonly WzMapleVersion[] EncryptionVersions =
            { WzMapleVersion.BMS, WzMapleVersion.GMS, WzMapleVersion.EMS, WzMapleVersion.CLASSIC };

        private readonly string _versionPath;
        private readonly VersionInfo? _versionInfo;
        private readonly List<CheckBox> _categoryCheckBoxes = new();
        private CancellationTokenSource _cts = new();
        private bool _isPacking = false;

        public PackToWzFormWindow(string versionPath)
        {
            InitializeComponent();
            _versionPath = versionPath;

            string manifestPath = Path.Combine(versionPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    _versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<VersionInfo>(json);
                }
                catch { _versionInfo = null; }
            }

            PopulateCategoryList();
            textBox_outputPath.Text = Path.Combine(Path.GetDirectoryName(versionPath) ?? versionPath, "WZ_Output");

            if (_versionInfo != null)
            {
                label_versionInfo.Text = $"Version: {_versionInfo.DisplayName ?? _versionInfo.Version}";
                checkBox_64bit.IsChecked = _versionInfo.Is64Bit;

                if (_versionInfo.IsBetaMs)
                {
                    label_format.Text = "Source: Beta (Single Data.wz)";
                    label_format.Foreground = Avalonia.Media.Brushes.DarkGreen;
                    checkBox_betaFormat.IsChecked = true;
                }
                else if (_versionInfo.Is64Bit)
                {
                    label_format.Text = "Source: 64-bit (Data folder)";
                    label_format.Foreground = Avalonia.Media.Brushes.DarkBlue;
                }
                else if (_versionInfo.IsPreBB)
                {
                    label_format.Text = "Source: Pre-Big Bang";
                    label_format.Foreground = Avalonia.Media.Brushes.DarkOrange;
                }
                else
                {
                    label_format.Text = "Source: Standard";
                    label_format.Foreground = Avalonia.Media.Brushes.Black;
                }

                int patchVersion = _versionInfo.PatchVersion;
                if (patchVersion >= 0 && patchVersion <= 32767)
                    numericUpDown_patchVersion.Value = patchVersion;
            }
            else
            {
                label_versionInfo.Text = "Version: Unknown (no manifest.json)";
                label_format.Text = "Source: Unknown";
                label_format.Foreground = Avalonia.Media.Brushes.Gray;
            }

            PopulateEncryptionDropdown();
            UpdateFormatOptionsState();

            Closing += (_, e) =>
            {
                if (_isPacking)
                {
                    e.Cancel = true;
                    Warning.Error("Please wait for packing to complete or cancel it first.");
                }
                else
                {
                    _cts.Dispose();
                }
            };
        }

        public static void ShowWindow(Window? owner, string versionPath)
        {
            var win = new PackToWzFormWindow(versionPath);
            if (owner != null) win.ShowDialog(owner);
            else win.Show();
        }

        private void PopulateEncryptionDropdown()
        {
            var recommended = GetRecommendedEncryption();
            comboBox_encryption.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < EncryptionVersions.Length; i++)
            {
                var enc = EncryptionVersions[i];
                string label = enc == recommended ? $"{enc} (Recommended)" : enc.ToString();
                comboBox_encryption.Items.Add(label);
                if (enc == recommended) selectedIndex = i;
            }
            comboBox_encryption.SelectedIndex = selectedIndex;
        }

        private WzMapleVersion GetSelectedEncryption()
        {
            int idx = comboBox_encryption.SelectedIndex;
            return idx >= 0 && idx < EncryptionVersions.Length ? EncryptionVersions[idx] : WzMapleVersion.BMS;
        }

        private WzMapleVersion GetRecommendedEncryption()
        {
            if (_versionInfo != null)
            {
                if (!string.IsNullOrEmpty(_versionInfo.Encryption) &&
                    Enum.TryParse<WzMapleVersion>(_versionInfo.Encryption, true, out var parsed))
                    return parsed;

                switch (_versionInfo.SourceRegion?.Trim().ToUpperInvariant())
                {
                    case "GMS": return WzMapleVersion.GMS;
                    case "EMS": return WzMapleVersion.EMS;
                    case "CLASSIC": return WzMapleVersion.CLASSIC;
                }
            }
            return WzMapleVersion.BMS;
        }

        private void PopulateCategoryList()
        {
            categoriesPanel.Children.Clear();
            _categoryCheckBoxes.Clear();

            var addedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in WzPackingService.STANDARD_CATEGORIES)
            {
                if (TryBuildCategoryDisplay(_versionPath, category, out string displayName))
                {
                    AddCategoryCheckBox(displayName);
                    addedCategories.Add(category);
                }
            }

            foreach (var dirPath in Directory.EnumerateDirectories(_versionPath))
            {
                string dirName = Path.GetFileName(dirPath);
                if (addedCategories.Contains(dirName) || dirName.StartsWith(".") ||
                    string.Equals(dirName, "manifest", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryBuildCategoryDisplay(_versionPath, dirName, out string displayName))
                    AddCategoryCheckBox(displayName);
            }
        }

        private static bool TryBuildCategoryDisplay(string versionPath, string category, out string displayName)
        {
            displayName = string.Empty;
            string categoryPath = Path.Combine(versionPath, category);
            if (!Directory.Exists(categoryPath)) return false;

            int imgCount = Directory.EnumerateFiles(categoryPath, "*.img", SearchOption.AllDirectories).Count();
            int subDirCount = Directory.EnumerateDirectories(categoryPath, "*", SearchOption.AllDirectories).Count();
            bool hasListJson = string.Equals(category, "List", StringComparison.OrdinalIgnoreCase) &&
                               File.Exists(Path.Combine(categoryPath, "List.json"));

            if (imgCount == 0 && subDirCount == 0 && !hasListJson) return false;

            if (imgCount > 0) displayName = $"{category} ({imgCount} images)";
            else if (hasListJson) displayName = $"{category} (List.json)";
            else displayName = $"{category} (directory structure)";
            return true;
        }

        private void AddCategoryCheckBox(string displayName)
        {
            var cb = new CheckBox { Content = displayName, IsChecked = true };
            _categoryCheckBoxes.Add(cb);
            categoriesPanel.Children.Add(cb);
        }

        private void OnSelectAllClick(object? sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckBoxes) cb.IsChecked = true;
        }

        private void OnSelectNoneClick(object? sender, RoutedEventArgs e)
        {
            foreach (var cb in _categoryCheckBoxes) cb.IsChecked = false;
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            string path = await SavedFolderBrowser.ShowAsync(this, "Select output folder for WZ files");
            if (!string.IsNullOrEmpty(path)) textBox_outputPath.Text = path;
        }

        private void OnFormatChanged(object? sender, RoutedEventArgs e) => UpdateFormatOptionsState();

        private void UpdateFormatOptionsState()
        {
            if (checkBox_betaFormat.IsChecked == true)
            {
                checkBox_64bit.IsChecked = false;
                checkBox_64bit.IsEnabled = false;
            }
            else
            {
                checkBox_64bit.IsEnabled = true;
            }
        }

        private void SavePackingSettingsToManifest()
        {
            if (_versionInfo == null) return;
            string manifestPath = Path.Combine(_versionPath, "manifest.json");
            if (!File.Exists(manifestPath)) return;
            try
            {
                _versionInfo.Encryption = GetSelectedEncryption().ToString();
                _versionInfo.Is64Bit = checkBox_64bit.IsChecked == true;
                _versionInfo.IsBetaMs = checkBox_betaFormat.IsChecked == true;
                _versionInfo.PatchVersion = (int)(numericUpDown_patchVersion.Value ?? 0);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_versionInfo, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(manifestPath, json);
            }
            catch { }
        }

        private async void OnPackClick(object? sender, RoutedEventArgs e)
        {
            if (_isPacking)
            {
                _cts.Cancel();
                return;
            }

            var selectedCategories = _categoryCheckBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => (cb.Content as string ?? "").Split(' ')[0])
                .ToList();

            if (selectedCategories.Count == 0)
            {
                Warning.Error("Please select at least one category to pack.");
                return;
            }
            string outputPath = textBox_outputPath.Text ?? "";
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Warning.Error("Please specify an output path.");
                return;
            }

            SavePackingSettingsToManifest();

            _isPacking = true;
            _cts = new CancellationTokenSource();
            button_pack.Content = "Cancel";
            SetControlsEnabled(false);

            try
            {
                var packingService = new WzPackingService();
                var encr = GetSelectedEncryption();
                short patchVer = (short)(numericUpDown_patchVersion.Value ?? 0);
                bool is64bit = checkBox_64bit.IsChecked == true;
                bool isBeta = checkBox_betaFormat.IsChecked == true;

                var progress = new Progress<PackingProgress>(p =>
                    Dispatcher.UIThread.Post(() => UpdateProgress(p)));

                PackingResult result = isBeta
                    ? await packingService.PackBetaDataWzAsync(_versionPath, outputPath, selectedCategories, _cts.Token, progress, patchVer, encr)
                    : await packingService.PackCategoriesAsync(_versionPath, outputPath, selectedCategories, is64bit, _cts.Token, progress, patchVer, false, encr);

                if (result.Success)
                {
                    label_status.Text = $"Completed! Packed {result.TotalImagesPacked} images.";
                    Warning.Error($"Successfully packed {result.TotalImagesPacked} images into {result.CategoriesPacked.Count} WZ files.\n\nOutput: {outputPath}\nTotal size: {FormatFileSize(result.TotalOutputSize)}\nDuration: {result.Duration.TotalSeconds:F1}s");
                }
                else
                {
                    label_status.Text = $"Failed: {result.ErrorMessage}";
                    Warning.Error($"Packing failed: {result.ErrorMessage}");
                }
            }
            catch (OperationCanceledException)
            {
                label_status.Text = "Cancelled";
            }
            catch (Exception ex)
            {
                label_status.Text = $"Error: {ex.Message}";
                Warning.Error($"Error during packing: {ex.Message}");
            }
            finally
            {
                _isPacking = false;
                button_pack.Content = "Pack";
                SetControlsEnabled(true);
                UpdateFormatOptionsState();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            foreach (var cb in _categoryCheckBoxes) cb.IsEnabled = enabled;
            textBox_outputPath.IsEnabled = enabled;
            checkBox_betaFormat.IsEnabled = enabled;
            numericUpDown_patchVersion.IsEnabled = enabled;
            comboBox_encryption.IsEnabled = enabled;
        }

        private void UpdateProgress(PackingProgress p)
        {
            progressBar.Value = Math.Min(p.ProgressPercentage, 100);
            label_status.Text = $"{p.CurrentPhase}: {p.CurrentFile ?? ""} ({p.ProcessedFiles}/{p.TotalFiles})";
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            if (_isPacking) _cts.Cancel();
            else Close();
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
            return $"{size:F2} {sizes[order]}";
        }
    }
}
