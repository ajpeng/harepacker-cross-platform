using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MapleLib.WzLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SystemPoint = System.Drawing.Point;

namespace HaRepacker.GUI.Input
{
    public enum ReplaceResult { Yes, No, YesToAll, NoToAll, NoneSelectedYet }

    public static class InputDialogs
    {
        // ── Name (single text field) ─────────────────────────────────────
        public static async Task<(bool ok, string? name)> ShowNameAsync(
            Window owner, string title, int maxLength = 0, string defaultValue = "")
        {
            var dlg = MakeWindow(title, 360, 140);
            var nameBox = new TextBox { Text = defaultValue, Watermark = "Name" };
            if (maxLength > 0) nameBox.MaxLength = maxLength;
            string? result = null;
            var buttons = MakeOkCancel(
                () => { if (!string.IsNullOrEmpty(nameBox.Text)) { result = nameBox.Text; dlg.Close(true); } else ShowValidationError(); },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, result);
        }

        // ── Name + Value ──────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, string? value)> ShowNameValueAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 360, 180);
            var nameBox = new TextBox { Watermark = "Name" };
            var valueBox = new TextBox { Watermark = "Value" };
            string? rName = null, rValue = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (!string.IsNullOrEmpty(nameBox.Text) && !string.IsNullOrEmpty(valueBox.Text))
                    { rName = nameBox.Text; rValue = valueBox.Text; dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, valueBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rValue);
        }

        // ── Int ───────────────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, int? value)> ShowIntAsync(
            Window owner, string title,
            string defaultName = "", int defaultValue = 0, bool hideName = false)
        {
            var dlg = MakeWindow(title, 360, hideName ? 120 : 160);
            var numBox = new NumericUpDown
            {
                Minimum = int.MinValue, Maximum = int.MaxValue,
                Value = defaultValue, Increment = 1, FormatString = "0"
            };
            string? rName = null; int? rVal = null;

            Control[] rows;
            TextBox? nameBox = null;
            if (!hideName)
            {
                nameBox = new TextBox { Text = defaultName, Watermark = "Name" };
                rows = new Control[] { nameBox, numBox };
            }
            else rows = new Control[] { numBox };

            var buttons = MakeOkCancel(
                () =>
                {
                    if (hideName || !string.IsNullOrEmpty(nameBox?.Text))
                    { rName = nameBox?.Text ?? ""; rVal = (int?)numBox.Value; dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(rows, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rVal);
        }

        // ── Long ──────────────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, long? value)> ShowLongAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 360, 160);
            var nameBox = new TextBox { Watermark = "Name" };
            var numBox = new NumericUpDown { Minimum = long.MinValue, Maximum = long.MaxValue, Increment = 1, FormatString = "0" };
            string? rName = null; long? rVal = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (!string.IsNullOrEmpty(nameBox.Text))
                    { rName = nameBox.Text; rVal = (long?)numBox.Value; dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, numBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rVal);
        }

        // ── Float ─────────────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, double? value)> ShowFloatAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 360, 160);
            var nameBox = new TextBox { Watermark = "Name" };
            var numBox = new NumericUpDown { Increment = 0.1m, FormatString = "0.##########" };
            string? rName = null; double? rVal = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (!string.IsNullOrEmpty(nameBox.Text))
                    { rName = nameBox.Text; rVal = (double?)numBox.Value; dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, numBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rVal);
        }

        // ── Vector ────────────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, SystemPoint? point)> ShowVectorAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 360, 200);
            var nameBox = new TextBox { Watermark = "Name" };
            var xBox = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Increment = 1, Watermark = "X", FormatString = "0" };
            var yBox = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue, Increment = 1, Watermark = "Y", FormatString = "0" };
            string? rName = null; SystemPoint? rPt = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    rName = nameBox.Text ?? "";
                    rPt = new SystemPoint((int)(xBox.Value ?? 0), (int)(yBox.Value ?? 0));
                    dlg.Close(true);
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, xBox, yBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rPt);
        }

        // ── Rename ────────────────────────────────────────────────────────
        public static async Task<(bool ok, string? newName)> ShowRenameAsync(
            Window owner, string title, string currentName)
        {
            var dlg = MakeWindow(title, 360, 130);
            var nameBox = new TextBox { Text = currentName, Watermark = "New name" };
            string? result = null;
            var buttons = MakeOkCancel(
                () => { if (!string.IsNullOrEmpty(nameBox.Text)) { result = nameBox.Text; dlg.Close(true); } else ShowValidationError(); },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, result);
        }

        // ── Sound file ────────────────────────────────────────────────────
        public static async Task<(bool ok, string? name, string? path)> ShowSoundAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 420, 160);
            var nameBox = new TextBox { Watermark = "Name" };
            var pathBox = new TextBox { Watermark = "Sound file path", IsReadOnly = true };
            var browseRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            HStackFill(browseRow, pathBox);
            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += async (_, _) =>
            {
                var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select MP3",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("MP3 Audio") { Patterns = new[] { "*.mp3" } } }
                });
                if (files.Count > 0) pathBox.Text = files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
            };
            browseRow.Children.Add(browseBtn);

            string? rName = null, rPath = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (!string.IsNullOrEmpty(nameBox.Text) && !string.IsNullOrEmpty(pathBox.Text) && File.Exists(pathBox.Text))
                    { rName = nameBox.Text; rPath = pathBox.Text; dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, browseRow, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rPath);
        }

        // ── Bitmap (image file picker + preview) ──────────────────────────
        public static async Task<(bool ok, string? name, List<SKBitmap>? bitmaps)> ShowBitmapAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 440, 290);
            var nameBox = new TextBox { Watermark = "Name (leave empty for GIF frames)" };
            var pathBox = new TextBox { Watermark = "Image path", IsReadOnly = true };
            var preview = new Image { Stretch = Stretch.Uniform, MaxHeight = 110 };

            var browseRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            HStackFill(browseRow, pathBox);
            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += async (_, _) =>
            {
                var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Image",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Images") { Patterns = new[] { "*.png","*.bmp","*.jpg","*.jpeg","*.gif" } }
                    }
                });
                if (files.Count > 0)
                {
                    string path = files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;
                    pathBox.Text = path;
                    try
                    {
                        using var bm = SKBitmap.Decode(path);
                        if (bm != null)
                        {
                            using var img = SKImage.FromBitmap(bm);
                            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                            using var ms = new System.IO.MemoryStream(data.ToArray());
                            preview.Source = new Bitmap(ms);
                        }
                    }
                    catch { preview.Source = null; }
                }
            };
            browseRow.Children.Add(browseBtn);

            string? rName = null; List<SKBitmap>? rBmps = null;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (!string.IsNullOrEmpty(pathBox.Text) && preview.Source != null)
                    { rName = nameBox.Text ?? ""; rBmps = LoadBitmapFrames(pathBox.Text); dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(nameBox, browseRow, preview, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rName, rBmps);
        }

        // ── WzMapleVersion picker ─────────────────────────────────────────
        public static async Task<(bool ok, WzMapleVersion version)> ShowWzMapleVersionAsync(
            Window owner, string title)
        {
            var dlg = MakeWindow(title, 360, 140);
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.Items.Add("GMS (Global MapleStory)");
            combo.Items.Add("EMS (MapleSEA/JMS)");
            combo.Items.Add("BMS (Unencrypted/Modern)");
            combo.Items.Add("Custom");
            combo.Items.Add("Generate");
            combo.SelectedIndex = MapleVersionToIndex(
                Program.ConfigurationManager?.ApplicationSettings?.MapleVersion ?? WzMapleVersion.BMS);

            WzMapleVersion rVersion = WzMapleVersion.BMS;
            var buttons = MakeOkCancel(
                () =>
                {
                    if (combo.SelectedIndex >= 0)
                    { rVersion = IndexToMapleVersion(combo.SelectedIndex); dlg.Close(true); }
                    else ShowValidationError();
                },
                () => dlg.Close(false));
            dlg.Content = Padded(VStack(combo, buttons));
            bool ok = await dlg.ShowDialog<bool>(owner);
            return (ok, rVersion);
        }

        // ── Replace confirm ───────────────────────────────────────────────
        public static async Task<(bool shown, ReplaceResult result)> ShowReplaceAsync(
            Window owner, string name)
        {
            var dlg = MakeWindow("Replace", 400, 150);
            var label = new TextBlock { Text = $"Replace \"{name}\"?", TextWrapping = TextWrapping.Wrap };
            ReplaceResult result = ReplaceResult.No;
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
            void AddBtn(string text, ReplaceResult r)
            {
                var b = new Button { Content = text, Width = 86 };
                b.Click += (_, _) => { result = r; dlg.Close(true); };
                btnRow.Children.Add(b);
            }
            AddBtn("Yes", ReplaceResult.Yes);
            AddBtn("No", ReplaceResult.No);
            AddBtn("Yes to all", ReplaceResult.YesToAll);
            AddBtn("No to all", ReplaceResult.NoToAll);
            dlg.Content = Padded(VStack(label, btnRow));
            await dlg.ShowDialog<bool>(owner);
            return (true, result);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static Window MakeWindow(string title, double width, double height) => new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        private static StackPanel VStack(params Control[] children)
        {
            var sp = new StackPanel { Spacing = 10 };
            foreach (var c in children) sp.Children.Add(c);
            return sp;
        }

        private static StackPanel VStack(Control[] extra, Control last)
        {
            var sp = new StackPanel { Spacing = 10 };
            foreach (var c in extra) sp.Children.Add(c);
            sp.Children.Add(last);
            return sp;
        }

        private static void HStackFill(StackPanel row, Control fillControl)
        {
            fillControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.Children.Add(fillControl);
        }

        private static Border Padded(Control inner) => new Border { Padding = new Avalonia.Thickness(14), Child = inner };

        private static StackPanel MakeOkCancel(Action onOk, Action onCancel)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            var ok = new Button { Content = "OK", Width = 80 };
            var cancel = new Button { Content = "Cancel", Width = 80 };
            ok.Click += (_, _) => onOk();
            cancel.Click += (_, _) => onCancel();
            row.Children.Add(ok);
            row.Children.Add(cancel);
            return row;
        }

        private static void ShowValidationError() => Warning.Error("Please enter a valid value.");

        private static List<SKBitmap> LoadBitmapFrames(string path)
        {
            var list = new List<SKBitmap>();
            try
            {
                using var stream = File.OpenRead(path);
                using var codec = SKCodec.Create(stream);
                if (codec != null && codec.FrameCount > 1)
                {
                    var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
                    for (int i = 0; i < codec.FrameCount; i++)
                    {
                        var bm = new SKBitmap(info);
                        codec.GetPixels(info, bm.GetPixels(), new SKCodecOptions(i));
                        list.Add(bm);
                    }
                }
                else
                {
                    var bm = SKBitmap.Decode(path);
                    if (bm != null) list.Add(bm);
                }
            }
            catch
            {
                var bm = SKBitmap.Decode(path);
                if (bm != null) list.Add(bm);
            }
            return list;
        }

        private static int MapleVersionToIndex(WzMapleVersion v) => v switch
        {
            WzMapleVersion.GMS => 0,
            WzMapleVersion.EMS => 1,
            WzMapleVersion.BMS => 2,
            WzMapleVersion.CUSTOM => 3,
            WzMapleVersion.GENERATE => 4,
            _ => 2
        };

        private static WzMapleVersion IndexToMapleVersion(int idx) => idx switch
        {
            0 => WzMapleVersion.GMS,
            1 => WzMapleVersion.EMS,
            3 => WzMapleVersion.CUSTOM,
            4 => WzMapleVersion.GENERATE,
            _ => WzMapleVersion.BMS
        };
    }
}
