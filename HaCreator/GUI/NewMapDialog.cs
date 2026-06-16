/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HaCreator.MapEditor;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using System;
using System.Diagnostics;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.GUI
{
    public class NewMapDialog : Window
    {
        private readonly MultiBoard _multiBoard;
        private readonly TabControl _tabs;
        private readonly EventHandler[] _rightClickHandlers;

        private readonly TextBox _widthBox;
        private readonly TextBox _heightBox;
        private readonly TextBox _mapIdBox;
        private readonly Button _btnCreateClone;

        public NewMapDialog(MultiBoard multiBoard, TabControl tabs, EventHandler[] rightClickHandlers)
        {
            _multiBoard = multiBoard;
            _tabs = tabs;
            _rightClickHandlers = rightClickHandlers;

            Title = "New Map";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Padding = new Thickness(16);

            _widthBox  = new TextBox { Text = "800", Width = 100 };
            _heightBox = new TextBox { Text = "600", Width = 100 };
            _mapIdBox  = new TextBox { Watermark = "Enter map ID...", Width = 180 };

            _btnCreateClone = new Button
            {
                Content = "Create from Clone",
                IsEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _mapIdBox.TextChanged  += (_, _) => _btnCreateClone.IsEnabled = int.TryParse(_mapIdBox.Text, out _);
            _btnCreateClone.Click  += BtnCreateClone_Click;

            var btnCreate = new Button { Content = "Create", HorizontalAlignment = HorizontalAlignment.Right };
            btnCreate.Click += BtnCreate_Click;

            var blankSection = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Create blank map", FontWeight = FontWeight.Bold },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 12,
                            Children =
                            {
                                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Width:" }, _widthBox } },
                                new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Height:" }, _heightBox } }
                            }
                        },
                        btnCreate
                    }
                }
            };

            var cloneSection = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 12, 0, 0),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = "Clone existing map", FontWeight = FontWeight.Bold },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock { Text = "Map ID:", VerticalAlignment = VerticalAlignment.Center },
                                _mapIdBox
                            }
                        },
                        _btnCreateClone
                    }
                }
            };

            Content = new StackPanel { Children = { blankSection, cloneSection } };

            Opened += (_, _) =>
            {
                _widthBox.Text  = ApplicationSettings.LastMapSize.Width.ToString();
                _heightBox.Text = ApplicationSettings.LastMapSize.Height.ToString();
            };
        }

        private void BtnCreate_Click(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_widthBox.Text, out int w) || !int.TryParse(_heightBox.Text, out int h) || w <= 0 || h <= 0)
            {
                Debug.WriteLine("[NewMapDialog] Invalid map dimensions");
                return;
            }
            MapLoader.CreateMap("", "<Untitled>", -1, "", true,
                MapLoader.CreateStandardMapMenu(_rightClickHandlers),
                new XNA.Point(w, h), new XNA.Point(w / 2, h / 2),
                _tabs, _multiBoard);
            Close();
        }

        private void BtnCreateClone_Click(object? sender, RoutedEventArgs e)
        {
            if (!long.TryParse(_mapIdBox.Text, out long mapId)) return;

            string mapIdStr = mapId.ToString();
            WzImage? mapImage = WzInfoTools.FindMapImage(mapIdStr, Program.WzManager);
            if (mapImage == null)
            {
                Debug.WriteLine($"[NewMapDialog] Map image null for id {mapIdStr}");
                return;
            }

            string cloneMapName = "NO NAME", cloneStreetName = "NO NAME", cloneCategoryName = "NO NAME";
            if (Program.InfoManager.MapsNameCache.ContainsKey(mapIdStr))
            {
                var names = Program.InfoManager.MapsNameCache[mapIdStr];
                cloneMapName      = names.Item1;
                cloneStreetName   = names.Item2;
                cloneCategoryName = names.Item3;
            }

            MapInfo info = new MapInfo(mapImage, cloneMapName, cloneStreetName, cloneCategoryName);
            MapLoader.CreateMapFromImage(-1, mapImage.DeepClone(), info,
                cloneMapName, cloneStreetName, cloneCategoryName,
                _tabs, _multiBoard, _rightClickHandlers);
            Close();
        }
    }
}
