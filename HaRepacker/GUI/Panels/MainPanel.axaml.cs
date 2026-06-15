using Avalonia.Controls;
using Avalonia.Input;
using HaRepacker.Models;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.ObjectModel;
using System.IO;

namespace HaRepacker.GUI.Panels
{
    public partial class MainPanel : UserControl
    {
        private readonly ObservableCollection<WzNode> _rootNodes = new ObservableCollection<WzNode>();

        public MainPanel()
        {
            InitializeComponent();
            wzTreeView.ItemsSource = _rootNodes;
        }

        public void OpenFile(string path)
        {
            if (!File.Exists(path)) return;

            loadingPanel.IsVisible = true;
            loadingPanel.OnStartAnimate();

            try
            {
                WzFile wz = new WzFile(path, WzMapleVersion.CLASSIC);
                wz.ParseWzFile();

                var node = new WzNode(wz);
                _rootNodes.Add(node);
            }
            catch
            {
                // TODO: show error dialog
            }
            finally
            {
                loadingPanel.IsVisible = false;
                loadingPanel.OnPauseAnimate();
            }
        }

        private void WzTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (wzTreeView.SelectedItem is not WzNode node) return;

            var obj = node.WzObject;

            // Show the appropriate sub-panel for the selected node type
            imageViewer.IsVisible = false;
            textEditor.IsVisible = false;
            xyPanel.IsVisible = false;

            if (obj is MapleLib.WzLib.WzProperties.WzCanvasProperty canvas)
            {
                var bmp = canvas.GetLinkedWzCanvasBitmap();
                imageViewer.SetImage(bmp);
                imageViewer.IsVisible = true;
            }
            else if (obj is MapleLib.WzLib.WzProperties.WzStringProperty strProp)
            {
                textEditor.SetText(strProp.Value);
                textEditor.IsVisible = true;
            }
            else if (obj is MapleLib.WzLib.WzProperties.WzVectorProperty vecProp)
            {
                xyPanel.X = vecProp.X.Value;
                xyPanel.Y = vecProp.Y.Value;
                xyPanel.IsVisible = true;
            }
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            // TODO: implement search
        }

        private void ClearSearch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            searchBox.Text = string.Empty;
        }
    }
}
