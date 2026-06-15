using Avalonia.Controls;
using Avalonia.Interactivity;
using HaRepacker.GUI.Panels;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class NewFormWindow : Window
    {
        private readonly MainPanel _panel;

        private static readonly string[] EncryptionLabels = { "GMS", "EMS", "BMS (Unencrypted/Modern)", "Custom" };
        private static readonly WzMapleVersion[] EncryptionVersions = { WzMapleVersion.GMS, WzMapleVersion.EMS, WzMapleVersion.BMS, WzMapleVersion.CUSTOM };

        public NewFormWindow(MainPanel panel)
        {
            InitializeComponent();
            _panel = panel;
            foreach (var label in EncryptionLabels) encryptionBox.Items.Add(label);
            SelectEncryption(Program.ConfigurationManager?.ApplicationSettings?.MapleVersion ?? WzMapleVersion.BMS);
        }

        public static Task ShowAsync(Window owner, MainPanel panel)
        {
            var dlg = new NewFormWindow(panel);
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

        private void OnTypeChanged(object? sender, RoutedEventArgs e)
        {
            bool hotfix = radioHotfix.IsChecked == true;
            panelOptions.IsEnabled = !hotfix;
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            string name = (nameBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) { Warning.Error("Please enter a file name."); return; }

            var version = SelectedEncryption;
            if (version == WzMapleVersion.CUSTOM)
                Program.ConfigurationManager?.SetCustomWzUserKeyFromConfig();
            else
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();

            if (radioRegular.IsChecked == true)
            {
                var file = new WzFile((short)(versionBox.Value ?? 1), version);
                file.Header.Copyright = "";
                file.Header.RecalculateFileStart();
                file.Name = name + ".wz";
                file.WzDirectory.Name = name + ".wz";
                _panel.AddRootNode(file);
            }
            else if (radioHotfix.IsChecked == true)
            {
                var img = new WzImage(name + ".wz");
                img.MarkWzImageAsParsed();
                _panel.AddRootNode(img);
            }
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
    }
}
