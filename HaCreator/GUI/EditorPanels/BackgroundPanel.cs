/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.GUI.EditorPanels
{
    public class BackgroundPanel : UserControl
    {
        private HaCreatorStateManager? _hcsm;
        private HotSwapRefreshService? _hotSwapService;

        private readonly ListBox _setListBox;
        private readonly RadioButton _radioBack;
        private readonly RadioButton _radioAni;
        private readonly RadioButton _radioSpine;
        private readonly WrapPanel _thumbGrid;
        private readonly Button _addImageBtn;

        public BackgroundPanel()
        {
            _setListBox = new ListBox { Height = 140 };

            _radioBack  = new RadioButton { Content = "Back",   GroupName = "BgType", IsChecked = true };
            _radioAni   = new RadioButton { Content = "Ani",    GroupName = "BgType" };
            _radioSpine = new RadioButton { Content = "Spine",  GroupName = "BgType" };

            _thumbGrid = new WrapPanel { Orientation = Orientation.Horizontal };

            _addImageBtn = new Button
            {
                Content = "Add Image",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(2)
            };

            var radioRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(2, 2, 2, 4),
                Children = { _radioBack, _radioAni, _radioSpine }
            };

            var scroll = new ScrollViewer
            {
                Content = _thumbGrid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetRow(radioRow,    0);
            Grid.SetRow(_setListBox, 1);
            Grid.SetRow(scroll,      2);
            Grid.SetRow(_addImageBtn, 3);

            root.Children.Add(radioRow);
            root.Children.Add(_setListBox);
            root.Children.Add(scroll);
            root.Children.Add(_addImageBtn);

            Content = root;

            _setListBox.SelectionChanged     += OnSetSelectionChanged;
            _radioBack.IsCheckedChanged      += OnTypeChanged;
            _radioAni.IsCheckedChanged       += OnTypeChanged;
            _radioSpine.IsCheckedChanged     += OnTypeChanged;
            _addImageBtn.Click               += OnAddImageClick;
        }

        private BackgroundInfoType SelectedType =>
            _radioSpine.IsChecked == true ? BackgroundInfoType.Spine :
            _radioAni.IsChecked   == true ? BackgroundInfoType.Animation :
            BackgroundInfoType.Background;

        public void Initialize(HaCreatorStateManager hcsm)
        {
            _hcsm = hcsm;
            hcsm.SetBackgroundPanel(this);

            foreach (string bS in Program.InfoManager.BackgroundSets.Keys.OrderBy(k => k))
                _setListBox.Items.Add(bS);
        }

        public void SubscribeToHotSwap(HotSwapRefreshService svc)
        {
            if (_hotSwapService != null)
                _hotSwapService.BackgroundSetChanged -= OnBackgroundSetChanged;

            _hotSwapService = svc;

            if (_hotSwapService != null)
                _hotSwapService.BackgroundSetChanged += OnBackgroundSetChanged;
        }

        private void OnTypeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if ((sender as RadioButton)?.IsChecked == true)
                ReloadThumbnails();
        }

        private void OnSetSelectionChanged(object? sender, SelectionChangedEventArgs e) => ReloadThumbnails();

        private void ReloadThumbnails()
        {
            if (_hcsm == null) return;
            if (_setListBox.SelectedItem is not string setName) return;

            BackgroundInfoType infoType = SelectedType;
            var buttons = new List<Button>();

            lock (_hcsm.MultiBoard)
            {
                WzImage? bgSetImage = Program.InfoManager.GetBackgroundSet(setName);
                if (bgSetImage == null) return;

                WzImageProperty? parentProp = bgSetImage[infoType.ToPropertyString()];
                if (parentProp?.WzProperties == null) return;

                foreach (WzImageProperty prop in parentProp.WzProperties)
                {
                    try
                    {
                        BackgroundInfo? bgInfo = BackgroundInfo.Get(_hcsm.MultiBoard.GraphicsDevice, setName, infoType, prop.Name);
                        if (bgInfo == null) continue;

                        SKBitmap? sk = bgInfo.Image;
                        var bmp = SkBitmapToAvalonia(sk);

                        Button btn = MakeThumb(bmp, prop.Name, bgInfo);
                        WireItemEvents(btn, bgInfo, parentProp, infoType);
                        buttons.Add(btn);
                    }
                    catch { }
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                _thumbGrid.Children.Clear();
                foreach (var b in buttons)
                    _thumbGrid.Children.Add(b);
            });
        }

        private void WireItemEvents(Button btn, BackgroundInfo bgInfo, WzImageProperty parentProp, BackgroundInfoType infoType)
        {
            btn.Click += (_, _) =>
            {
                if (_hcsm == null) return;
                lock (_hcsm.MultiBoard)
                {
                    _hcsm.EnterEditMode(ItemTypes.Backgrounds);
                    _hcsm.MultiBoard.SelectedBoard?.Mouse.SetHeldInfo(bgInfo);
                    _hcsm.MultiBoard.Focus();
                }
            };

            var ctx = new ContextMenu();

            if (infoType == BackgroundInfoType.Spine)
            {
                var previewItem = new MenuItem { Header = "Preview (not supported)" };
                previewItem.Click += (_, _) => { };
                var deleteItem2 = new MenuItem { Header = "Delete" };
                deleteItem2.Click += async (_, _) => await DeleteBg(btn, bgInfo, parentProp);
                ctx.Items.Add(previewItem);
                ctx.Items.Add(deleteItem2);
            }
            else
            {
                var saveItem = new MenuItem { Header = "Save" };
                saveItem.Click += async (_, _) => await SaveBgImage(bgInfo, infoType);
                var deleteItem = new MenuItem { Header = "Delete" };
                deleteItem.Click += async (_, _) => await DeleteBg(btn, bgInfo, parentProp);
                ctx.Items.Add(saveItem);
                ctx.Items.Add(deleteItem);
            }

            btn.ContextMenu = ctx;
        }

        private async System.Threading.Tasks.Task SaveBgImage(BackgroundInfo bgInfo, BackgroundInfoType infoType)
        {
            if (bgInfo.Image == null) return;

            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save image",
                SuggestedFileName = $"{bgInfo.bS}.{infoType.ToPropertyString()}.{bgInfo.no}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG") { Patterns = ["*.png"] }
                }
            });

            if (file == null) return;
            try
            {
                await using var stream = await file.OpenWriteAsync();
                using var encoded = bgInfo.Image.Encode(SKEncodedImageFormat.Png, 100);
                encoded.SaveTo(stream);
            }
            catch { }
        }

        private async System.Threading.Tasks.Task DeleteBg(Button btn, BackgroundInfo bgInfo, WzImageProperty parentProp)
        {
            if (!await ShowConfirmAsync("Delete this background?")) return;

            WzImageProperty? removeProp = parentProp[bgInfo.no];
            if (removeProp != null && parentProp.WzProperties.Contains(removeProp))
            {
                parentProp.WzProperties.Remove(removeProp);

                WzObject? topDir = parentProp.GetTopMostWzDirectory();
                WzObject? topImg = parentProp.Parent as WzImage;
                if (topDir != null && topImg is WzImage wzImg)
                    Program.WzManager.SetWzFileUpdated(topDir.Name, wzImg);

                Dispatcher.UIThread.Post(() => _thumbGrid.Children.Remove(btn));
            }
        }

        private async void OnAddImageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_setListBox.SelectedItem is not string setName) return;

            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select image to add",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }
                }
            });

            if (files.Count == 0) return;
            string filePath = files[0].TryGetLocalPath() ?? string.Empty;
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                SKBitmap? newImage = SKBitmap.Decode(filePath);
                if (newImage == null) return;

                BackgroundInfoType infoType = BackgroundInfoType.Background;

                WzImage? bgSetImage = Program.InfoManager.GetBackgroundSet(setName);
                if (bgSetImage == null) return;

                WzSubProperty? parentProp = bgSetImage[infoType.ToPropertyString()] as WzSubProperty;
                if (parentProp == null) return;

                string newName = GenerateUniqueBgName(setName, infoType.ToPropertyString());

                WzCanvasProperty newBgProp = new WzCanvasProperty(newName);
                newBgProp.PngProperty = new WzPngProperty();
                newBgProp.PngProperty.PNG = newImage;
                newBgProp.AddProperty(new WzIntProperty("z", 0));
                newBgProp.AddProperty(new WzVectorProperty("origin", 0, 0));

                parentProp.AddProperty(newBgProp);

                System.Drawing.Point origin = new System.Drawing.Point(newImage.Width / 2, newImage.Height);
                BackgroundInfo newBgInfo = new BackgroundInfo(newBgProp, newImage, origin, setName, infoType, newName, newBgProp, null);

                if (bgSetImage.WzFileParent != null)
                    Program.WzManager.SetWzFileUpdated(bgSetImage.WzFileParent.Name, bgSetImage);

                var bmp = SkBitmapToAvalonia(newImage);
                Button btn = MakeThumb(bmp, newName, newBgInfo);
                WireItemEvents(btn, newBgInfo, parentProp, infoType);
                _thumbGrid.Children.Add(btn);
            }
            catch (Exception ex)
            {
                await ShowAlertAsync($"Error adding image: {ex.Message}");
            }
        }

        private string GenerateUniqueBgName(string setName, string infoTypeName)
        {
            WzImage? bgSetImage = Program.InfoManager.GetBackgroundSet(setName);
            WzImageProperty? parent = bgSetImage?[infoTypeName];
            int counter = 1;
            while (parent?.WzProperties.Any(p => p.Name == counter.ToString()) == true)
                counter++;
            return counter.ToString();
        }

        private async System.Threading.Tasks.Task<bool> ShowConfirmAsync(string message)
        {
            Window? owner = GetOwner();
            bool result = false;

            var dlg = new Window
            {
                Title = "Confirm",
                Width = 320, Height = 130,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var yes = new Button { Content = "Yes", Width = 70, Margin = new Thickness(4) };
            var no  = new Button { Content = "No",  Width = 70, Margin = new Thickness(4) };
            yes.Click += (_, _) => { result = true;  dlg.Close(); };
            no.Click  += (_, _) => { result = false; dlg.Close(); };
            dlg.Content = new StackPanel
            {
                Margin = new Thickness(12), Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4, Children = { yes, no } }
                }
            };
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();
            return result;
        }

        private async System.Threading.Tasks.Task ShowAlertAsync(string message)
        {
            Window? owner = GetOwner();
            var dlg = new Window
            {
                Title = "Error", Width = 360, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false
            };
            var ok = new Button { Content = "OK", Width = 70, Margin = new Thickness(4) };
            ok.Click += (_, _) => dlg.Close();
            dlg.Content = new StackPanel
            {
                Margin = new Thickness(12), Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok } }
                }
            };
            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();
        }

        private Window? GetOwner() =>
            _hcsm?.OwnerWindow ??
            (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null);

        private void OnBackgroundSetChanged(object? sender, BackgroundSetChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => HandleSetChange(e));
        }

        private void HandleSetChange(BackgroundSetChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case AssetChangeType.Added:
                    if (!_setListBox.Items.Contains(e.SetName))
                    {
                        _setListBox.Items.Add(e.SetName);
                        SortSetList();
                    }
                    break;

                case AssetChangeType.Removed:
                    if (_setListBox.SelectedItem is string sel && sel == e.SetName)
                    {
                        _thumbGrid.Children.Clear();
                        _setListBox.Items.Remove(e.SetName);
                        if (_setListBox.Items.Count > 0)
                            _setListBox.SelectedIndex = 0;
                    }
                    else
                    {
                        _setListBox.Items.Remove(e.SetName);
                    }
                    break;

                case AssetChangeType.Modified:
                    if (!_setListBox.Items.Contains(e.SetName))
                    {
                        _setListBox.Items.Add(e.SetName);
                        SortSetList();
                    }
                    else if (_setListBox.SelectedItem is string cur && cur == e.SetName)
                    {
                        Program.InfoManager.RefreshBackgroundSet(e.SetName);
                        ReloadThumbnails();
                    }
                    break;
            }
        }

        private void SortSetList()
        {
            var items = _setListBox.Items.Cast<string>().OrderBy(s => s).ToList();
            string? sel = _setListBox.SelectedItem as string;
            _setListBox.Items.Clear();
            foreach (var s in items) _setListBox.Items.Add(s);
            if (sel != null) _setListBox.SelectedItem = sel;
        }

        private static Avalonia.Media.Imaging.Bitmap? SkBitmapToAvalonia(SKBitmap? sk)
        {
            if (sk == null || sk.Width <= 0 || sk.Height <= 0) return null;
            try
            {
                using var data = sk.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream();
                data.SaveTo(ms);
                ms.Position = 0;
                return new Avalonia.Media.Imaging.Bitmap(ms);
            }
            catch { return null; }
        }

        private static Button MakeThumb(Avalonia.Media.Imaging.Bitmap? bmp, string label, object? tag)
        {
            Control imgCtrl = bmp != null
                ? (Control)new Image { Source = bmp, Width = 64, Height = 64, Stretch = Stretch.Uniform }
                : new Border { Width = 64, Height = 64, Background = Brushes.LightGray };

            return new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Width = 72,
                    Children =
                    {
                        imgCtrl,
                        new TextBlock
                        {
                            Text = label, FontSize = 9,
                            TextWrapping = TextWrapping.Wrap, MaxWidth = 72,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                },
                Padding = new Thickness(2),
                Margin = new Thickness(2),
                Tag = tag
            };
        }
    }
}
