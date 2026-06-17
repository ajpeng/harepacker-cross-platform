/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    public static class ManageUserObjectsDialog
    {
        public static async Task ShowAsync(UserObjectsManager userObjs, MultiBoard multiBoard, Window? owner)
        {
            // Build item list: old objects first, new (editor-added) last
            var oldItems = new List<UserObjItem>();
            var newItems = new List<UserObjItem>();
            foreach (var prop in userObjs.L1Property.WzProperties)
            {
                bool isNew = userObjs.NewObjects.Any(o => o.l2 == prop.Name);
                (isNew ? newItems : oldItems).Add(new UserObjItem(prop, isNew));
            }
            var all = new ObservableCollection<UserObjItem>(oldItems.Concat(newItems));

            // UI
            var listBox = new ListBox
            {
                ItemsSource = all, Height = 300,
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<UserObjItem>((item, _) =>
                    new TextBlock
                    {
                        Text = item.Name,
                        Foreground = item.IsNew ? Brushes.ForestGreen : Brushes.Black
                    })
            };

            var btnRemove = new Button { Content = "Remove",  IsEnabled = false };
            var btnSearch = new Button { Content = "Search in Editor", IsEnabled = false };
            var lblStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0,4,0,0) };

            listBox.SelectionChanged += (_, _) =>
            {
                bool sel = listBox.SelectedItem != null;
                btnRemove.IsEnabled = sel;
                btnSearch.IsEnabled = sel;
                lblStatus.Text = "";
            };

            btnRemove.Click += async (_, _) =>
            {
                if (listBox.SelectedItem is not UserObjItem item) return;
                bool ok = await Confirm(owner, "Remove Object",
                    "This action CANNOT BE UNDONE. Remove the selected object?");
                if (!ok) return;
                userObjs.Remove(item.Name);
                all.Remove(item);
                lblStatus.Text = $"Removed: {item.Name}";
            };

            btnSearch.Click += (_, _) =>
            {
                if (listBox.SelectedItem is not UserObjItem item) return;
                lblStatus.Text = "Searching...";
                Task.Run(() =>
                {
                    var results = SearchEditorForObj(item.Name, multiBoard);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (results.Count == 0)
                            lblStatus.Text = $"'{item.Name}' is not used in any open maps.";
                        else
                            lblStatus.Text = $"Used in: {string.Join(", ", results)}";
                    });
                });
            };

            var win = new Window
            {
                Title = "Manage User Objects",
                Width = 420, Height = 440,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Star),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                    },
                    Children =
                    {
                        SetRow(listBox, 0),
                        SetRow(lblStatus, 1),
                        SetRow(new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Avalonia.Thickness(6), Spacing = 6,
                            Children = { btnRemove, btnSearch, new Button { Content = "Close", IsCancel = true } }
                        }, 2),
                    }
                }
            };
            ((Button)((StackPanel)((Grid)win.Content!).Children[2]).Children[2]).Click += (_, _) => win.Close();

            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
        }

        static List<string> SearchEditorForObj(string l2, MultiBoard multiBoard)
        {
            var result = new List<string>();
            lock (multiBoard)
            {
                foreach (var board in multiBoard.Boards)
                {
                    foreach (var li in board.BoardItems.TileObjs)
                    {
                        if (li is ObjectInstance && li.BaseInfo is ObjectInfo oi &&
                            oi.oS == UserObjectsManager.oS &&
                            oi.l0 == Program.APP_NAME &&
                            oi.l1 == UserObjectsManager.l1 &&
                            oi.l2 == l2)
                        {
                            result.Add(board.MapInfo.id.ToString());
                            break;
                        }
                    }
                }
            }
            return result;
        }

        static Control SetRow(Control c, int row) { Grid.SetRow(c, row); return c; }

        static async Task<bool> Confirm(Window? owner, string title, string message)
        {
            bool result = false;
            var btnYes = new Button { Content = "Yes", IsDefault = true };
            var btnNo  = new Button { Content = "No" };
            var win = new Window
            {
                Title = title, Width = 360, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 6, Children = { btnYes, btnNo }
                        }
                    }
                }
            };
            btnYes.Click += (_, _) => { result = true;  win.Close(); };
            btnNo.Click  += (_, _) => { result = false; win.Close(); };
            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
            return result;
        }

        private record UserObjItem(MapleLib.WzLib.WzImageProperty Prop, bool IsNew)
        {
            public string Name => Prop.Name;
        }
    }
}
