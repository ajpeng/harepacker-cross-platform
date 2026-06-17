/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HaCreator.MapEditor;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    public class SaveMapDialog : Window
    {
        private readonly Board _board;
        private readonly TextBox _idBox;
        private readonly TextBlock _statusLabel;
        private readonly Button _saveButton;

        public SaveMapDialog(Board board)
        {
            _board = board;

            Title = "Save Map";
            Width = 380;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Padding = new Thickness(16);

            _idBox = new TextBox { Width = 300 };
            _statusLabel = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray };
            _saveButton = new Button { Content = "Save", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelButton = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right };

            _idBox.TextChanged += (_, _) => ValidateAndUpdate();
            _saveButton.Click += SaveButton_Click;
            cancelButton.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Map ID or Name:", FontWeight = FontWeight.Bold },
                    _idBox,
                    _statusLabel,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, _saveButton }
                    }
                }
            };

            Opened += (_, _) => InitialiseId();
        }

        private void InitialiseId()
        {
            if (_board.IsNewMapDesign)
            {
                _idBox.Text = MapConstants.MaxMap.ToString();
            }
            else
            {
                switch (_board.MapInfo.mapType)
                {
                    case MapType.CashShopPreview:
                    case MapType.ITCPreview:
                    case MapType.MapLogin:
                        _idBox.Text = _board.MapInfo.strMapName;
                        break;
                    case MapType.RegularMap:
                        _idBox.Text = _board.MapInfo.id == -1 ? "-1" : _board.MapInfo.id.ToString();
                        break;
                    default:
                        Debug.WriteLine("[SaveMapDialog] Unknown map type");
                        _idBox.Text = string.Empty;
                        break;
                }
            }
            ValidateAndUpdate();
        }

        private MapType GetMapType()
        {
            string text = _idBox.Text ?? string.Empty;
            if (text.StartsWith("MapLogin"))    return MapType.MapLogin;
            if (text == "CashShopPreview")      return MapType.CashShopPreview;
            if (text == "ITCPreview")           return MapType.ITCPreview;
            return MapType.RegularMap;
        }

        private void ValidateAndUpdate()
        {
            string text = _idBox.Text ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                _statusLabel.Text = "Please enter an ID.";
                _statusLabel.Foreground = Brushes.Gray;
                _saveButton.IsEnabled = false;
                return;
            }

            if (GetMapType() != MapType.RegularMap)
            {
                _statusLabel.Text = string.Empty;
                _saveButton.IsEnabled = true;
                return;
            }

            if (!int.TryParse(text, out int id) || id == MapConstants.MaxMap)
            {
                _statusLabel.Text = "Please enter a valid number.";
                _statusLabel.Foreground = Brushes.DarkRed;
                _saveButton.IsEnabled = false;
                return;
            }

            if (id < MapConstants.MinMap || id > MapConstants.MaxMap)
            {
                _statusLabel.Text = $"Out of range: {MapConstants.MinMap} – {MapConstants.MaxMap}";
                _statusLabel.Foreground = Brushes.DarkRed;
                _saveButton.IsEnabled = false;
                return;
            }

            if (Program.InfoManager.MapsNameCache.ContainsKey(id.ToString()))
            {
                if (_board.IsNewMapDesign)
                {
                    _statusLabel.Text = "WARNING: ID already taken — choose an empty ID.";
                    _statusLabel.Foreground = Brushes.DarkOrange;
                    _saveButton.IsEnabled = false;
                }
                else
                {
                    _statusLabel.Text = "WARNING: This will overwrite an existing map.";
                    _statusLabel.Foreground = Brushes.DarkOrange;
                    _saveButton.IsEnabled = true;
                }
                return;
            }

            _statusLabel.Text = string.Empty;
            _saveButton.IsEnabled = true;
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_board.ParentControl.UserObjects?.NewObjects.Count > 0)
            {
                bool flush = await ConfirmAsync(
                    "Save User Objects",
                    "There are new user objects that have not been saved to the WZ file.\n" +
                    "Flush them now (they will be embedded in the map)?");
                if (flush)
                    _board.ParentControl.UserObjects.Flush();
            }

            MapType type = GetMapType();
            MapSaver saver = new MapSaver(_board);
            _board.RegenerateMinimap();

            if (type == MapType.RegularMap)
            {
                int newId = int.Parse(_idBox.Text);
                saver.ChangeMapTypeAndID(newId, MapType.RegularMap);
                saver.SaveMapImage();
                saver.UpdateMapLists();
                Debug.WriteLine($"[SaveMapDialog] Saved map with ID: {newId}");
            }
            else
            {
                _board.MapInfo.strMapName = _idBox.Text;
                if (_board.TabPage is TabItem tabItem && tabItem.Tag is TabItemContainer tic)
                    tic.Text = _board.MapInfo.strMapName;
                saver.ChangeMapTypeAndID(-1, type);
                saver.SaveMapImage();
                Debug.WriteLine($"[SaveMapDialog] Saved map: {_board.MapInfo.strMapName}");
            }
            Close();
        }

        private async System.Threading.Tasks.Task<bool> ConfirmAsync(string title, string message)
        {
            bool result = false;
            var btnYes = new Button { Content = "Yes", IsDefault = true };
            var btnNo  = new Button { Content = "No",  IsCancel  = true };
            var win = new Window
            {
                Title = title, Width = 380, Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new StackPanel { Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6,
                            Children = { btnYes, btnNo } }
                    }
                }
            };
            btnYes.Click += (_, _) => { result = true;  win.Close(); };
            btnNo.Click  += (_, _) => { result = false; win.Close(); };
            await win.ShowDialog(this);
            return result;
        }
    }
}
