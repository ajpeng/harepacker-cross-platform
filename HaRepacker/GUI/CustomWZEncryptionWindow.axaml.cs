using Avalonia.Controls;
using Avalonia.Interactivity;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class CustomWZEncryptionWindow : Window
    {
        private readonly TextBox[] _aesBoxes = new TextBox[32];
        private readonly ObservableCollection<EncryptionKey> _keys;
        private EncryptionKey? _selectedKey;

        public CustomWZEncryptionWindow()
        {
            InitializeComponent();

            for (int i = 0; i < 32; i++)
            {
                var tb = new TextBox { Width = 44, MaxLength = 2, Margin = new Avalonia.Thickness(2) };
                _aesBoxes[i] = tb;
                aesKeyPanel.Children.Add(tb);
            }

            _keys = new ObservableCollection<EncryptionKey>(
                Program.ConfigurationManager?.CustomKeys ?? new List<EncryptionKey>());
            keyListBox.ItemsSource = _keys;

            LoadFromConfig();
        }

        public static Task ShowAsync(Window owner)
        {
            var dlg = new CustomWZEncryptionWindow();
            return dlg.ShowDialog(owner);
        }

        private void LoadFromConfig()
        {
            var cfg = Program.ConfigurationManager;
            if (cfg == null) { SetDefaultData(); return; }

            string storedIV = cfg.ApplicationSettings.MapleVersion_CustomEncryptionBytes;
            string[] ivParts = storedIV.Split(' ');
            bool ivOk = ivParts.Length == 4 && ivParts.All(IsHexByte);
            if (!ivOk)
            {
                cfg.ApplicationSettings.MapleVersion_CustomEncryptionBytes = "00 00 00 00";
                cfg.Save();
                SetDefaultIV();
            }
            else
            {
                ivBox0.Text = ivParts[0]; ivBox1.Text = ivParts[1];
                ivBox2.Text = ivParts[2]; ivBox3.Text = ivParts[3];
            }

            string storedKey = cfg.ApplicationSettings.MapleVersion_CustomAESUserKey;
            if (string.IsNullOrEmpty(storedKey))
            {
                SetDefaultAESKey();
            }
            else
            {
                string[] keyParts = storedKey.Split(' ');
                bool keyOk = keyParts.Length == 32 && keyParts.All(IsHexByte);
                if (!keyOk)
                {
                    cfg.ApplicationSettings.MapleVersion_CustomAESUserKey = string.Empty;
                    cfg.Save();
                    SetDefaultAESKey();
                }
                else
                {
                    for (int i = 0; i < 32; i++) _aesBoxes[i].Text = keyParts[i];
                }
            }

            var iv = cfg.GetCusomWzIVEncryption();
            cfg.SetCustomWzUserKeyFromConfig();
            var currentKey = WzKeyGenerator.GenerateWzKey(iv, MapleCryptoConstants.UserKey_WzLib);
            int matchIdx = _keys.ToList().FindIndex(k => k.WzKey == currentKey);
            if (matchIdx >= 0)
                keyListBox.SelectedIndex = matchIdx;
            else
                SetDefaultData();
        }

        private void SetDefaultData()
        {
            _selectedKey = null;
            keyListBox.SelectedIndex = -1;
            nameBox.Text = "Custom Key " + (_keys.Count + 1);
            SetDefaultIV();
            SetDefaultAESKey();
        }

        private void SetDefaultIV()
        {
            ivBox0.Text = "00"; ivBox1.Text = "00";
            ivBox2.Text = "00"; ivBox3.Text = "00";
        }

        private void SetDefaultAESKey()
        {
            byte[] def = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT;
            for (int i = 0; i < 32; i++)
                _aesBoxes[i].Text = HexTool.ToString(def[i * 4]);
        }

        private void FillFromSelectedKey()
        {
            if (_selectedKey == null) { SetDefaultIV(); SetDefaultAESKey(); return; }

            nameBox.Text = _selectedKey.Name;

            var iv = _selectedKey.Iv.Split(' ');
            if (iv.Length == 4) { ivBox0.Text = iv[0]; ivBox1.Text = iv[1]; ivBox2.Text = iv[2]; ivBox3.Text = iv[3]; }

            var key = _selectedKey.AesUserKey.Split(' ');
            for (int i = 0; i < 32 && i < key.Length; i++)
                _aesBoxes[i].Text = key[i];
        }

        private void OnKeyListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (keyListBox.SelectedItem is EncryptionKey k)
            {
                _selectedKey = k;
                FillFromSelectedKey();
            }
        }

        private void OnCreateClick(object? sender, RoutedEventArgs e) => SetDefaultData();

        private void OnDeleteClick(object? sender, RoutedEventArgs e)
        {
            if (_keys.Count <= 1) { Warning.Error("Must have at least one custom key."); return; }
            if (_selectedKey == null) { Warning.Error("Nothing to delete."); return; }
            if (!Warning.Warn($"Delete [{_selectedKey.Name}]?")) return;

            int removeIdx = keyListBox.SelectedIndex;
            Program.ConfigurationManager?.CustomKeys.Remove(_selectedKey);
            _keys.RemoveAt(removeIdx);
            _selectedKey = null;

            if (_keys.Count > 0)
            {
                keyListBox.SelectedIndex = Math.Min(removeIdx, _keys.Count - 1);
                _selectedKey = keyListBox.SelectedItem as EncryptionKey;
                FillFromSelectedKey();
            }
            else
            {
                SetDefaultData();
            }
            Program.ConfigurationManager?.Save();
        }

        private void OnResetAESClick(object? sender, RoutedEventArgs e) => SetDefaultAESKey();

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            string name = (nameBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) { Warning.Error("Please enter a name."); return; }

            string[] ivBytes = { ivBox0.Text ?? "", ivBox1.Text ?? "", ivBox2.Text ?? "", ivBox3.Text ?? "" };
            if (!ivBytes.All(IsHexByte)) { Warning.Error("Wrong format for AES IV. Please check the input bytes."); return; }

            string[] keyBytes = _aesBoxes.Select(tb => tb.Text ?? "").ToArray();
            if (!keyBytes.All(IsHexByte)) { Warning.Error("Wrong format for AES User Key. Please check the input bytes."); return; }

            var cfg = Program.ConfigurationManager;
            if (cfg == null) { Close(); return; }

            cfg.ApplicationSettings.MapleVersion_CustomEncryptionName = name;
            cfg.ApplicationSettings.MapleVersion_CustomEncryptionBytes = string.Join(" ", ivBytes);
            cfg.ApplicationSettings.MapleVersion_CustomAESUserKey = string.Join(" ", keyBytes);
            cfg.SetCustomWzUserKeyFromConfig();

            if (_selectedKey == null)
            {
                var newKey = new EncryptionKey
                {
                    Name = name,
                    Iv = cfg.ApplicationSettings.MapleVersion_CustomEncryptionBytes,
                    AesUserKey = cfg.ApplicationSettings.MapleVersion_CustomAESUserKey,
                    MapleVersion = WzMapleVersion.CUSTOM,
                };
                cfg.CustomKeys.Add(newKey);
                _keys.Add(newKey);
            }
            else
            {
                _selectedKey.Name = name;
                _selectedKey.Iv = cfg.ApplicationSettings.MapleVersion_CustomEncryptionBytes;
                _selectedKey.AesUserKey = cfg.ApplicationSettings.MapleVersion_CustomAESUserKey;
            }
            cfg.Save();
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

        private static bool IsHexByte(string s)
        {
            if (s.Length < 1 || s.Length > 2) return false;
            return s.All(c => HexEncoding.IsHexDigit(c));
        }
    }
}
