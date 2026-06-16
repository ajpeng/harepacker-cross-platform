using Avalonia.Controls;
using Avalonia.Layout;
using Footholds;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaRepacker.FHMapper
{
    public class EditFootholdWindow : Window
    {
        private readonly FootHold.Foothold _fh;
        private readonly List<object> _settings;

        private readonly TextBox _prevBox;
        private readonly TextBox _nextBox;
        private readonly TextBox _forceBox;

        public EditFootholdWindow(FootHold.Foothold fh, List<object> settings)
        {
            _fh = fh;
            _settings = settings;

            Title = "Edit Foothold";
            Width = 320;
            Height = 260;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            string prevCurrent = ((WzIntProperty)fh.Data["prev"]).Value.ToString();
            string nextCurrent = ((WzIntProperty)fh.Data["next"]).Value.ToString();
            string forceCurrent;
            try { forceCurrent = ((WzIntProperty)fh.Data["force"]).Value.ToString(); }
            catch { forceCurrent = "None"; }

            _prevBox = new TextBox { Text = _settings.Count > 1 && (bool)_settings[1] ? _settings[0].ToString() : prevCurrent };
            _nextBox = new TextBox { Text = _settings.Count > 3 && (bool)_settings[3] ? _settings[2].ToString() : nextCurrent };
            _forceBox = new TextBox { Text = _settings.Count > 5 && (bool)_settings[5] ? _settings[4].ToString() : (forceCurrent == "None" ? string.Empty : forceCurrent) };

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
                    new TextBlock { Text = $"Prev: {prevCurrent}" },
                    _prevBox,
                    new TextBlock { Text = $"Next: {nextCurrent}" },
                    _nextBox,
                    new TextBlock { Text = $"Force: {forceCurrent}" },
                    _forceBox,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { okBtn, cancelBtn } }
                }
            };
        }

        private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_prevBox.Text))
                {
                    ((WzIntProperty)_fh.Data["prev"]).Value = int.Parse(_prevBox.Text);
                    _fh.Data["prev"].ParentImage.Changed = true;
                }
                if (!string.IsNullOrEmpty(_nextBox.Text))
                {
                    ((WzIntProperty)_fh.Data["next"]).Value = int.Parse(_nextBox.Text);
                    _fh.Data["next"].ParentImage.Changed = true;
                }
                if (!string.IsNullOrEmpty(_forceBox.Text))
                {
                    int forceVal = int.Parse(_forceBox.Text);
                    if (_fh.Data["force"] == null)
                    {
                        _fh.Data.AddProperty(new WzIntProperty("force", forceVal));
                        _fh.Data.ParentImage.Changed = true;
                    }
                    else
                    {
                        ((WzIntProperty)_fh.Data["force"]).Value = forceVal;
                        _fh.Data["force"].ParentImage.Changed = true;
                    }
                }
                Close();
            }
            catch (FormatException)
            {
                Warning.Error("Invalid input — all fields must be integers.");
            }
        }
    }
}
