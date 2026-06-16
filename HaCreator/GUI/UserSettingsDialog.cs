/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using System;

namespace HaCreator.GUI
{
    /// <summary>
    /// Avalonia port of UserSettingsForm.
    /// Color customisation requires Avalonia 11.3+ ColorPicker; omitted for now.
    /// </summary>
    public class UserSettingsDialog : Window
    {
        // General
        private readonly CheckBox _chkErrors;
        private readonly NumericUpDown _numLineW;
        private readonly NumericUpDown _numDotW;
        private readonly NumericUpDown _numAlpha;
        private readonly CheckBox _chkClip;
        private readonly CheckBox _chkFixFh;
        private readonly CheckBox _chkInvertUD;
        private readonly CheckBox _chkBackup;
        private readonly TextBox _txtFont;
        private readonly NumericUpDown _numFontSize;
        private readonly NumericUpDown _numHiddenR;

        // Mob/NPC offsets
        private readonly NumericUpDown _numMobRx0;
        private readonly NumericUpDown _numMobRx1;
        private readonly NumericUpDown _numNpcRx0;
        private readonly NumericUpDown _numNpcRx1;
        private readonly NumericUpDown _numMobTime;
        private readonly NumericUpDown _numReactTime;
        private readonly NumericUpDown _numZShift;
        private readonly NumericUpDown _numSnapDist;
        private readonly NumericUpDown _numScrollDist;
        private readonly NumericUpDown _numScrollBase;
        private readonly NumericUpDown _numScrollExp;
        private readonly NumericUpDown _numScrollFact;
        private readonly NumericUpDown _numMoveDist;

        public UserSettingsDialog()
        {
            Title = "User Settings";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Padding = new Thickness(14);

            // ── General ─────────────────────────────────────────────────
            _chkErrors   = new CheckBox { Content = "Show error messages",             IsChecked = UserSettings.ShowErrorsMessage };
            _chkClip     = new CheckBox { Content = "Clip text to bounds",              IsChecked = UserSettings.ClipText };
            _chkFixFh    = new CheckBox { Content = "Fix foothold mispositions",        IsChecked = UserSettings.FixFootholdMispositions };
            _chkInvertUD = new CheckBox { Content = "Invert up/down scroll direction",  IsChecked = UserSettings.InverseUpDown };
            _chkBackup   = new CheckBox { Content = "Enable auto-backup",               IsChecked = UserSettings.BackupEnabled };

            _numLineW    = MakeNum(UserSettings.LineWidth, 0, 20);
            _numDotW     = MakeNum(UserSettings.DotWidth,  0, 20);
            _numAlpha    = MakeNum(UserSettings.NonActiveAlpha, 0, 255);
            _numHiddenR  = MakeNum(UserSettings.HiddenLifeR, 0, 10000);
            _txtFont     = new TextBox { Text = UserSettings.FontName, Width = 180 };
            _numFontSize = MakeNum(UserSettings.FontSize, 6, 72);

            // ── Mob/NPC offsets ─────────────────────────────────────────
            _numMobRx0    = MakeNum(UserSettings.Mobrx0Offset,        -9999, 9999);
            _numMobRx1    = MakeNum(UserSettings.Mobrx1Offset,        -9999, 9999);
            _numNpcRx0    = MakeNum(UserSettings.Npcrx0Offset,        -9999, 9999);
            _numNpcRx1    = MakeNum(UserSettings.Npcrx1Offset,        -9999, 9999);
            _numMobTime   = MakeNum(UserSettings.defaultMobTime,       0, 99999);
            _numReactTime = MakeNum(UserSettings.defaultReactorTime,   0, 99999);
            _numZShift    = MakeNum(UserSettings.zShift,              -9999, 9999);
            _numSnapDist  = MakeNum((decimal)UserSettings.SnapDistance, 0, 100, 1);
            _numScrollDist= MakeNum(UserSettings.ScrollDistance,       0, 100);
            _numScrollBase= MakeNum((decimal)UserSettings.ScrollBase,  0, 10, 2);
            _numScrollExp = MakeNum((decimal)UserSettings.ScrollExponentFactor, 0, 10, 2);
            _numScrollFact= MakeNum((decimal)UserSettings.ScrollFactor, 0, 10, 2);
            _numMoveDist  = MakeNum((decimal)UserSettings.SignificantDistance, 0, 100, 1);

            var okBtn     = new Button { Content = "OK",     HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right };
            okBtn.Click     += OkButton_Click;
            cancelBtn.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    MakeSection("General",
                        _chkErrors, _chkClip, _chkFixFh, _chkInvertUD, _chkBackup,
                        MakeRow("Line width:",        _numLineW),
                        MakeRow("Dot width:",         _numDotW),
                        MakeRow("Inactive alpha:",    _numAlpha),
                        MakeRow("Hidden life radius:",_numHiddenR),
                        MakeRow("Font name:",         _txtFont),
                        MakeRow("Font size:",         _numFontSize)),

                    MakeSection("Mob/NPC Offsets",
                        MakeRow("Mob rx0:",     _numMobRx0),
                        MakeRow("Mob rx1:",     _numMobRx1),
                        MakeRow("NPC rx0:",     _numNpcRx0),
                        MakeRow("NPC rx1:",     _numNpcRx1),
                        MakeRow("Mob time:",    _numMobTime),
                        MakeRow("Reactor time:",_numReactTime)),

                    MakeSection("Editor Behaviour",
                        MakeRow("Z-shift:",         _numZShift),
                        MakeRow("Snap distance:",   _numSnapDist),
                        MakeRow("Scroll distance:", _numScrollDist),
                        MakeRow("Scroll base:",     _numScrollBase),
                        MakeRow("Scroll exponent:", _numScrollExp),
                        MakeRow("Scroll factor:",   _numScrollFact),
                        MakeRow("Move sensitivity:",_numMoveDist)),

                    new TextBlock
                    {
                        Text = "Note: Colour customisation requires Avalonia 11.3+ and will be added in a future update.",
                        Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap
                    },

                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelBtn, okBtn }
                    }
                }
            };
        }

        private static NumericUpDown MakeNum(decimal v, decimal min, decimal max, int decimals = 0)
            => new NumericUpDown { Value = v, Minimum = min, Maximum = max, FormatString = decimals == 0 ? "0" : $"F{decimals}", Width = 100 };

        private static Control MakeRow(string label, Control ctrl)
            => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = label, Width = 150, VerticalAlignment = VerticalAlignment.Center },
                    ctrl
                }
            };

        private static Control MakeSection(string header, params Control[] children)
        {
            var sp = new StackPanel { Spacing = 6 };
            sp.Children.Add(new TextBlock { Text = header, FontWeight = FontWeight.Bold });
            foreach (var c in children)
                sp.Children.Add(c);
            return new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 8),
                Child = sp
            };
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            UserSettings.ShowErrorsMessage       = _chkErrors.IsChecked == true;
            UserSettings.ClipText                = _chkClip.IsChecked == true;
            UserSettings.FixFootholdMispositions = _chkFixFh.IsChecked == true;
            UserSettings.InverseUpDown           = _chkInvertUD.IsChecked == true;
            UserSettings.BackupEnabled           = _chkBackup.IsChecked == true;

            UserSettings.LineWidth        = (int)(_numLineW.Value ?? UserSettings.LineWidth);
            UserSettings.DotWidth         = (int)(_numDotW.Value  ?? UserSettings.DotWidth);
            MapleDot.OnDotWidthChanged();
            UserSettings.NonActiveAlpha   = (int)(_numAlpha.Value  ?? UserSettings.NonActiveAlpha);
            UserSettings.HiddenLifeR      = (int)(_numHiddenR.Value ?? UserSettings.HiddenLifeR);
            UserSettings.FontName         = _txtFont.Text ?? UserSettings.FontName;
            UserSettings.FontSize         = (int)(_numFontSize.Value ?? UserSettings.FontSize);

            UserSettings.Mobrx0Offset         = (int)(_numMobRx0.Value    ?? 0);
            UserSettings.Mobrx1Offset         = (int)(_numMobRx1.Value    ?? 0);
            UserSettings.Npcrx0Offset         = (int)(_numNpcRx0.Value    ?? 0);
            UserSettings.Npcrx1Offset         = (int)(_numNpcRx1.Value    ?? 0);
            UserSettings.defaultMobTime       = (int)(_numMobTime.Value   ?? 0);
            UserSettings.defaultReactorTime   = (int)(_numReactTime.Value ?? 0);
            UserSettings.zShift               = (int)(_numZShift.Value    ?? 0);
            UserSettings.SnapDistance         = (float)(_numSnapDist.Value  ?? 0);
            UserSettings.ScrollDistance       = (int)(_numScrollDist.Value ?? 0);
            UserSettings.ScrollBase           = (double)(_numScrollBase.Value ?? 0);
            UserSettings.ScrollExponentFactor = (double)(_numScrollExp.Value  ?? 0);
            UserSettings.ScrollFactor         = (double)(_numScrollFact.Value ?? 0);
            UserSettings.SignificantDistance  = (float)(_numMoveDist.Value  ?? 0);

            MultiBoard.RecalculateSettings();
            Close();
        }
    }
}
