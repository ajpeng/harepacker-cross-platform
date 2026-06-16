using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HaRepacker.FHMapper
{
    public class FHSettingsWindow : Window
    {
        private readonly FHMapper _main;
        private readonly List<object> _settings;

        // Controls
        private readonly TextBox _prevTBox, _nextTBox, _forceTBox;
        private readonly CheckBox _prevCBox, _nextCBox, _forceCBox;
        private readonly TextBox _xTBox, _yTBox, _typeTBox;
        private readonly CheckBox _xCBox, _yCBox, _typeCBox;
        private readonly TextBox _filepathTBox, _sizeTBox;
        private readonly CheckBox _filepathCBox, _sizeCBox;

        public FHSettingsWindow(FHMapper main, List<object> settings)
        {
            _main = main;
            _settings = settings;

            Title = "FH Mapper Settings";
            Width = 480;
            Height = 520;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Initialize controls from settings list
            _prevTBox = new TextBox { Text = settings[0].ToString() };
            _prevCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[1] };
            _nextTBox = new TextBox { Text = settings[2].ToString() };
            _nextCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[3] };
            _forceTBox = new TextBox { Text = settings[4].ToString() };
            _forceCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[5] };
            _xTBox = new TextBox { Text = settings[6].ToString() };
            _xCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[7] };
            _yTBox = new TextBox { Text = settings[8].ToString() };
            _yCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[9] };
            _typeTBox = new TextBox { Text = settings[10].ToString() };
            _typeCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[11] };
            _filepathTBox = new TextBox { Text = settings[12].ToString() };
            _filepathCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[13] };
            _sizeTBox = new TextBox { Text = settings[14].ToString() };
            _sizeCBox = new CheckBox { Content = "Use default", IsChecked = (bool)settings[15] };

            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += OnBrowseClick;

            var okBtn = new Button { Content = "OK", Width = 80 };
            var cancelBtn = new Button { Content = "Cancel", Width = 80 };
            okBtn.Click += OnOk;
            cancelBtn.Click += (_, _) => Close();

            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12),
                    Spacing = 8,
                    Children =
                    {
                        MakeSection("Default Prev:", _prevTBox, _prevCBox),
                        MakeSection("Default Next:", _nextTBox, _nextCBox),
                        MakeSection("Default Force:", _forceTBox, _forceCBox),
                        MakeSection("Default X:", _xTBox, _xCBox),
                        MakeSection("Default Y:", _yTBox, _yCBox),
                        MakeSection("Default Portal Type:", _typeTBox, _typeCBox),
                        new TextBlock { Text = "WZ File Path:" },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4,
                            Children = { _filepathTBox, browseBtn, _filepathCBox } },
                        MakeSection("Display Scale:", _sizeTBox, _sizeCBox),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelBtn, okBtn }
                        }
                    }
                }
            };
        }

        private static StackPanel MakeSection(string label, TextBox box, CheckBox chk)
        {
            box.Width = 120;
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { new TextBlock { Text = label, Width = 160, VerticalAlignment = VerticalAlignment.Center }, box, chk }
            };
        }

        private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select WZ File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } } }
            });
            if (files.Count > 0)
                _filepathTBox.Text = files[0].Path.LocalPath;
        }

        private void OnOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Validate numeric fields
            if (!TryParseInt(_prevTBox.Text, "Prev") ||
                !TryParseInt(_nextTBox.Text, "Next") ||
                !TryParseInt(_forceTBox.Text, "Force") ||
                !TryParseInt(_xTBox.Text, "X") ||
                !TryParseInt(_yTBox.Text, "Y") ||
                !TryParseInt(_typeTBox.Text, "Portal Type") ||
                !TryParseDouble(_sizeTBox.Text, "Display Scale"))
                return;

            try
            {
                string s;
                using (var r = new StreamReader(FHMapper.SettingsPath)) s = r.ReadToEnd();

                s = Regex.Replace(s, @"(?<=!DPt:)-?\d*(?=!)", _prevTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DPc:)\w+(?=!)", _prevCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DNt:)-?\d*(?=!)", _nextTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DNc:)\w+(?=!)", _nextCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DFt:)-?\d*(?=!)", _forceTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DFc:)\w+(?=!)", _forceCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DXt:)-?\d*(?=!)", _xTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DXc:)\w+(?=!)", _xCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DYt:)-?\d*(?=!)", _yTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DYc:)\w+(?=!)", _yCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DTt:)-?\d*(?=!)", _typeTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DTc:)\w+(?=!)", _typeCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DFPt:)C:(%\w+)+.wz(?=!)", (_filepathTBox.Text ?? string.Empty).Replace('/', '%'));
                s = Regex.Replace(s, @"(?<=!DFPc:)\w+(?=!)", _filepathCBox.IsChecked.ToString()!);
                s = Regex.Replace(s, @"(?<=!DSt:)\d*,?\d*(?=!)", _sizeTBox.Text!);
                s = Regex.Replace(s, @"(?<=!DSc:)\w+(?=!)", _sizeCBox.IsChecked.ToString()!);

                using (var w = new StreamWriter(FHMapper.SettingsPath)) w.Write(s);

                _main.ParseSettings();
                Close();
            }
            catch (Exception ex)
            {
                Warning.Error("Failed to save settings: " + ex.Message);
            }
        }

        private static bool TryParseInt(string? text, string fieldName)
        {
            if (string.IsNullOrEmpty(text)) return true;
            if (!int.TryParse(text, out _))
            {
                Warning.Error($"Invalid value for {fieldName} — must be an integer.");
                return false;
            }
            return true;
        }

        private static bool TryParseDouble(string? text, string fieldName)
        {
            if (string.IsNullOrEmpty(text)) return true;
            if (!double.TryParse(text, out _))
            {
                Warning.Error($"Invalid value for {fieldName} — must be a number.");
                return false;
            }
            return true;
        }
    }
}
