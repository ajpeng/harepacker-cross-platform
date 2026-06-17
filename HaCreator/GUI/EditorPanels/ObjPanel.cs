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
    public class ObjPanel : UserControl
    {
        private HaCreatorStateManager? _hcsm;
        private HotSwapRefreshService? _hotSwapService;

        private readonly ListBox _setListBox;
        private readonly ListBox _l0ListBox;
        private readonly ListBox _l1ListBox;
        private readonly WrapPanel _thumbGrid;
        private readonly Button _addImageBtn;

        public ObjPanel()
        {
            _setListBox = new ListBox { Height = 140 };
            _l0ListBox  = new ListBox { Height = 140 };
            _l1ListBox  = new ListBox { Height = 140 };

            _thumbGrid = new WrapPanel { Orientation = Orientation.Horizontal };

            _addImageBtn = new Button
            {
                Content = "Add Image",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(2)
            };

            var listRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            listRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            listRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            listRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            var setCol = MakeLabeledList("Set", _setListBox);
            var l0Col  = MakeLabeledList("L0", _l0ListBox);
            var l1Col  = MakeLabeledList("L1", _l1ListBox);

            Grid.SetColumn(setCol, 0);
            Grid.SetColumn(l0Col,  1);
            Grid.SetColumn(l1Col,  2);

            listRow.Children.Add(setCol);
            listRow.Children.Add(l0Col);
            listRow.Children.Add(l1Col);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
            root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetRow(listRow,    0);
            Grid.SetRow(new ScrollViewer
            {
                Content = _thumbGrid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            }, 1);
            Grid.SetRow(_addImageBtn, 2);

            var scroll = new ScrollViewer
            {
                Content = _thumbGrid,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);

            root.Children.Add(listRow);
            root.Children.Add(scroll);
            root.Children.Add(_addImageBtn);

            Content = root;
            _addImageBtn.IsEnabled = false;

            _setListBox.SelectionChanged += OnSetSelectionChanged;
            _l0ListBox.SelectionChanged  += OnL0SelectionChanged;
            _l1ListBox.SelectionChanged  += OnL1SelectionChanged;
            _addImageBtn.Click           += OnAddImageClick;
        }

        private static StackPanel MakeLabeledList(string header, ListBox list)
        {
            return new StackPanel
            {
                Margin = new Thickness(1),
                Children =
                {
                    new TextBlock { Text = header, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) },
                    list
                }
            };
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            _hcsm = hcsm;
            hcsm.SetObjPanel(this);

            foreach (string oS in Program.InfoManager.ObjectSets.Keys.OrderBy(k => k))
                _setListBox.Items.Add(oS);
        }

        public void OnL1Changed(string l1)
        {
            if (_l1ListBox.SelectedItem is string cur && cur == l1)
                LoadThumbnails();
        }

        public void SubscribeToHotSwap(HotSwapRefreshService svc)
        {
            if (_hotSwapService != null)
                _hotSwapService.ObjectSetChanged -= OnObjectSetChanged;

            _hotSwapService = svc;

            if (_hotSwapService != null)
                _hotSwapService.ObjectSetChanged += OnObjectSetChanged;
        }

        private void OnSetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_setListBox.SelectedItem is not string) return;

            _l0ListBox.Items.Clear();
            _l1ListBox.Items.Clear();
            _thumbGrid.Children.Clear();
            _addImageBtn.IsEnabled = false;

            WzImage? oSImage = Program.InfoManager.GetObjectSet((string)_setListBox.SelectedItem);
            if (oSImage == null) return;

            foreach (WzImageProperty l0Prop in oSImage.WzProperties)
                _l0ListBox.Items.Add(l0Prop.Name);

            if (_l0ListBox.Items.Count > 0)
                _l0ListBox.SelectedIndex = 0;
        }

        private void OnL0SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_l0ListBox.SelectedItem is not string || _setListBox.SelectedItem is not string) return;

            _l1ListBox.Items.Clear();
            _thumbGrid.Children.Clear();
            _addImageBtn.IsEnabled = false;

            WzImage? oSImage = Program.InfoManager.GetObjectSet((string)_setListBox.SelectedItem);
            if (oSImage == null) return;

            WzImageProperty? l0Prop = oSImage[(string)_l0ListBox.SelectedItem];
            if (l0Prop == null) return;

            foreach (WzImageProperty l1Prop in l0Prop.WzProperties)
                _l1ListBox.Items.Add(l1Prop.Name);

            if (_l1ListBox.Items.Count > 0)
                _l1ListBox.SelectedIndex = 0;
        }

        private void OnL1SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            LoadThumbnails();
        }

        private void LoadThumbnails()
        {
            if (_hcsm == null) return;
            if (_setListBox.SelectedItem is not string setName) return;
            if (_l0ListBox.SelectedItem is not string l0Name) return;
            if (_l1ListBox.SelectedItem is not string l1Name) return;

            var buttons = new List<Button>();

            lock (_hcsm.MultiBoard)
            {
                WzImage? oSImage = Program.InfoManager.GetObjectSet(setName);
                if (oSImage == null) return;

                WzImageProperty? l1Prop = oSImage[l0Name]?[l1Name];
                if (l1Prop == null) return;

                foreach (WzSubProperty l2Prop in l1Prop.WzProperties.OfType<WzSubProperty>())
                {
                    try
                    {
                        ObjectInfo info = ObjectInfo.Get(setName, l0Name, l1Name, l2Prop.Name);
                        SKBitmap? sk = info.Image;
                        var bmp = SkBitmapToAvalonia(sk);

                        Button btn = MakeThumb(bmp, l2Prop.Name, info);
                        WireItemEvents(btn, info, l1Prop);
                        buttons.Add(btn);
                    }
                    catch (InvalidCastException) { break; }
                    catch { }
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                _thumbGrid.Children.Clear();
                foreach (var b in buttons)
                    _thumbGrid.Children.Add(b);
                _addImageBtn.IsEnabled = true;
            });
        }

        private void WireItemEvents(Button btn, ObjectInfo info, WzImageProperty l1Prop)
        {
            btn.Click += (_, _) =>
            {
                if (_hcsm == null) return;
                lock (_hcsm.MultiBoard)
                {
                    if (!_hcsm.MultiBoard.AssertLayerSelected()) return;
                    _hcsm.EnterEditMode(ItemTypes.Objects);
                    _hcsm.MultiBoard.SelectedBoard!.Mouse.SetHeldInfo(info);
                    _hcsm.MultiBoard.Focus();
                }
            };

            var ctx = new ContextMenu();

            var saveItem = new MenuItem { Header = "Save" };
            saveItem.Click += async (_, _) => await SaveObjectImage(info);

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += async (_, _) => await DeleteObject(btn, info, l1Prop);

            ctx.Items.Add(saveItem);
            ctx.Items.Add(deleteItem);
            btn.ContextMenu = ctx;
        }

        private async System.Threading.Tasks.Task SaveObjectImage(ObjectInfo info)
        {
            if (info.Image == null) return;

            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save image",
                SuggestedFileName = $"{info.oS}.{info.l0}.{info.l1}.{info.l2}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG") { Patterns = ["*.png"] }
                }
            });

            if (file == null) return;

            try
            {
                await using var stream = await file.OpenWriteAsync();
                using var encoded = info.Image.Encode(SKEncodedImageFormat.Png, 100);
                encoded.SaveTo(stream);
            }
            catch { }
        }

        private async System.Threading.Tasks.Task DeleteObject(Button btn, ObjectInfo info, WzImageProperty l1Prop)
        {
            if (!await ShowConfirmAsync("Delete this object?")) return;

            WzImageProperty? removeL2 = l1Prop[info.l2];
            if (removeL2 != null && l1Prop.WzProperties.Contains(removeL2))
            {
                l1Prop.WzProperties.Remove(removeL2);

                WzObject? topDir = l1Prop.GetTopMostWzDirectory();
                WzObject? topImg = l1Prop.GetTopMostWzImage();
                if (topDir != null && topImg is WzImage wzImg)
                    Program.WzManager.SetWzFileUpdated(topDir.Name, wzImg);

                Dispatcher.UIThread.Post(() => _thumbGrid.Children.Remove(btn));
            }
        }

        private async void OnAddImageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_setListBox.SelectedItem is not string setName) return;
            if (_l0ListBox.SelectedItem is not string l0Name) return;
            if (_l1ListBox.SelectedItem is not string l1Name) return;

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

                WzImage? oSImage = Program.InfoManager.GetObjectSet(setName);
                if (oSImage == null) return;
                WzImageProperty? l1Prop = oSImage[l0Name]?[l1Name];
                if (l1Prop == null) return;

                string newName = GenerateUniqueObjName(setName, l0Name, l1Name);

                WzSubProperty newL2 = new WzSubProperty(newName);
                newL2["z"] = new WzIntProperty("z", 0);
                WzCanvasProperty canvas = new WzCanvasProperty("0");
                canvas.PngProperty = new WzPngProperty();
                canvas.PngProperty.PNG = newImage;
                newL2["0"] = canvas;
                l1Prop.WzProperties.Add(newL2);

                System.Drawing.Point origin = new System.Drawing.Point(newImage.Width / 2, newImage.Height);
                ObjectInfo newInfo = new ObjectInfo(newImage, origin, setName, l0Name, l1Name, newName, newL2);

                WzObject? topDir = l1Prop.GetTopMostWzDirectory();
                WzObject? topImg = l1Prop.GetTopMostWzImage();
                if (topDir != null && topImg is WzImage wzImg)
                    Program.WzManager.SetWzFileUpdated(topDir.Name, wzImg);

                var bmp = SkBitmapToAvalonia(newImage);
                Button btn = MakeThumb(bmp, newName, newInfo);
                WireItemEvents(btn, newInfo, l1Prop);
                _thumbGrid.Children.Add(btn);
            }
            catch (Exception ex)
            {
                await ShowAlertAsync($"Error adding image: {ex.Message}");
            }
        }

        private string GenerateUniqueObjName(string setName, string l0, string l1)
        {
            WzImage? oSImage = Program.InfoManager.GetObjectSet(setName);
            WzImageProperty? l1Prop = oSImage?[l0]?[l1];
            int counter = 1;
            while (l1Prop?.WzProperties.Any(p => p.Name == counter.ToString()) == true)
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

        private void OnObjectSetChanged(object? sender, ObjectSetChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => HandleSetChange(e));
        }

        private void HandleSetChange(ObjectSetChangedEventArgs e)
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
                        ClearAll();
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
                        Program.InfoManager.RefreshObjectSet(e.SetName);
                        RefreshCurrentSet();
                    }
                    break;
            }
        }

        private void RefreshCurrentSet()
        {
            if (_setListBox.SelectedItem is not string setName) return;
            _l0ListBox.Items.Clear();
            _l1ListBox.Items.Clear();
            _thumbGrid.Children.Clear();
            _addImageBtn.IsEnabled = false;

            WzImage? oSImage = Program.InfoManager.GetObjectSet(setName);
            if (oSImage == null) return;
            foreach (WzImageProperty l0 in oSImage.WzProperties)
                _l0ListBox.Items.Add(l0.Name);
            if (_l0ListBox.Items.Count > 0)
                _l0ListBox.SelectedIndex = 0;
        }

        private void ClearAll()
        {
            _l0ListBox.Items.Clear();
            _l1ListBox.Items.Clear();
            _thumbGrid.Children.Clear();
            _addImageBtn.IsEnabled = false;
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
