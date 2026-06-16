using Avalonia.Controls;
using Avalonia.Layout;
using Footholds;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaRepacker.FHMapper
{
    public class EditPortalsWindow : Window
    {
        private readonly Portals.Portal _portal;
        private readonly List<object> _settings;

        private readonly TextBox _typeBox;
        private readonly TextBox _xBox;
        private readonly TextBox _yBox;

        public EditPortalsWindow(Portals.Portal portal, List<object> settings)
        {
            _portal = portal;
            _settings = settings;

            Title = "Edit Portal";
            Width = 320;
            Height = 240;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            string typeCurrent = ((WzIntProperty)portal.Data["pt"]).Value.ToString();
            string destCurrent = ((WzIntProperty)portal.Data["tm"]).Value.ToString();
            string xCurrent = ((WzIntProperty)portal.Data["x"]).Value.ToString();
            string yCurrent = ((WzIntProperty)portal.Data["y"]).Value.ToString();

            _typeBox = new TextBox { Text = _settings.Count > 11 && (bool)_settings[11] ? _settings[10].ToString() : typeCurrent };
            _xBox = new TextBox { Text = _settings.Count > 7 && (bool)_settings[7] ? _settings[6].ToString() : xCurrent };
            _yBox = new TextBox { Text = _settings.Count > 9 && (bool)_settings[9] ? _settings[8].ToString() : yCurrent };

            var okBtn = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Stretch };
            var cancelBtn = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch };
            okBtn.Click += OnOk;
            cancelBtn.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(10),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = $"Type (pt): {typeCurrent}   Dest (tm): {destCurrent}" },
                    new TextBlock { Text = $"X: {xCurrent}   Y: {yCurrent}" },
                    new TextBlock { Text = "New Type:" }, _typeBox,
                    new TextBlock { Text = "New X:" }, _xBox,
                    new TextBlock { Text = "New Y:" }, _yBox,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { okBtn, cancelBtn } }
                }
            };
        }

        private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_typeBox.Text))
                {
                    ((WzIntProperty)_portal.Data["pt"]).Value = int.Parse(_typeBox.Text);
                    _portal.Data["pt"].ParentImage.Changed = true;
                }
                if (!string.IsNullOrEmpty(_xBox.Text))
                {
                    ((WzIntProperty)_portal.Data["x"]).Value = int.Parse(_xBox.Text);
                    _portal.Data["x"].ParentImage.Changed = true;
                }
                if (!string.IsNullOrEmpty(_yBox.Text))
                {
                    ((WzIntProperty)_portal.Data["y"]).Value = int.Parse(_yBox.Text);
                    _portal.Data["y"].ParentImage.Changed = true;
                }
            }
            catch (FormatException)
            {
                Warning.Error("Invalid input — all fields must be integers.");
            }
            Close();
        }
    }
}
