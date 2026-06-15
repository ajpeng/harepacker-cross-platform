using Avalonia.Controls;
using Avalonia.Input;
using HaRepacker.Models;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace HaRepacker.GUI.Panels
{
    public partial class MainPanel : UserControl
    {
        private readonly ObservableCollection<WzNode> _rootNodes = new ObservableCollection<WzNode>();
        public UndoRedoManager? UndoMan => _undoMan;
        private UndoRedoManager? _undoMan;
        private ContextMenuManager? _contextMenuManager;

        public MainPanel()
        {
            InitializeComponent();
            wzTreeView.ItemsSource = _rootNodes;
            _undoMan = new UndoRedoManager(this);
            _contextMenuManager = new ContextMenuManager(this, _undoMan);
        }

        private void WzTreeView_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != Avalonia.Input.MouseButton.Right) return;
            if (SelectedNode is not WzNode node || node.WzObject == null) return;
            var menu = _contextMenuManager!.CreateMenu(node, node.WzObject);
            menu.Open(wzTreeView);
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

        public WzNode? SelectedNode => wzTreeView.SelectedItem as WzNode;

        public IEnumerable<WzNode> GetRootNodes() => _rootNodes;

        public void AddRootNode(WzObject wzObject)
        {
            _rootNodes.Add(new WzNode(wzObject));
        }

        public void RemoveRootNode(WzObject wzObject)
        {
            var node = _rootNodes.FirstOrDefault(n => n.WzObject == wzObject);
            if (node != null) _rootNodes.Remove(node);
        }

        public async System.Threading.Tasks.Task PromptRenameWzTreeNode(WzNode node, Avalonia.Controls.Window owner)
        {
            var (ok, newName) = await GUI.Input.InputDialogs.ShowRenameAsync(owner, "Rename", node.Name);
            if (ok && !string.IsNullOrEmpty(newName)) node.ChangeName(newName!);
        }

        public void PromptRemoveSelectedTreeNodes(UndoRedoManager undoMan)
        {
            if (!Warning.Warn("Are you sure you want to remove the selected node(s)?")) return;
            if (SelectedNode is not WzNode node) return;
            // Find parent node and remove
            WzNode? parent = FindParentOf(node, _rootNodes);
            if (parent != null)
            {
                node.DeleteNode();
                parent.Nodes.Remove(node);
                undoMan.AddUndoBatch(new System.Collections.Generic.List<UndoRedoAction>
                    { UndoRedoManager.ObjectRemoved(parent, node) });
            }
            else
            {
                // Root node — remove from root collection
                node.DeleteNode();
                _rootNodes.Remove(node);
            }
        }

        private static WzNode? FindParentOf(WzNode target, System.Collections.Generic.IEnumerable<WzNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Nodes.Contains(target)) return node;
                var found = FindParentOf(target, node.Nodes);
                if (found != null) return found;
            }
            return null;
        }

        public void UnloadWzFile(WzFile? wz)
        {
            if (wz == null) return;
            var node = _rootNodes.FirstOrDefault(n => n.WzObject == wz);
            if (node != null) _rootNodes.Remove(node);
            wz.Dispose();
        }

        public void ReloadWzFile(WzFile? wz)
        {
            if (wz == null) return;
            string? path = wz.FilePath;
            var version = wz.MapleVersion;
            UnloadWzFile(wz);
            if (path != null) OpenFile(path, version);
        }

        public void OpenFile(string path, WzMapleVersion version = WzMapleVersion.CLASSIC)
        {
            if (!File.Exists(path)) return;
            loadingPanel.IsVisible = true;
            loadingPanel.OnStartAnimate();
            try
            {
                var wz = new WzFile(path, version);
                wz.ParseWzFile();
                _rootNodes.Add(new WzNode(wz));
            }
            catch { }
            finally
            {
                loadingPanel.IsVisible = false;
                loadingPanel.OnPauseAnimate();
            }
        }

        public void ExpandAllNodes(bool expand)
        {
            foreach (var root in _rootNodes)
                SetExpanded(root, expand);
        }

        private static void SetExpanded(WzNode node, bool expand)
        {
            node.IsExpanded = expand;
            foreach (var child in node.Nodes)
                SetExpanded(child, expand);
        }

        public void SortNodesRecursively(WzNode node, bool viewOnly)
        {
            var sorted = node.Nodes.OrderBy(n => n.Name).ToList();
            node.Nodes.Clear();
            foreach (var n in sorted) node.Nodes.Add(n);
        }

        public void SortNodeProperties(WzNode node) => SortNodesRecursively(node, true);

        public void StartAnimateSelectedCanvas() => Warning.Error("Animation preview not yet implemented.");
        public void SaveImageAnimation_Click() => Warning.Error("Save animation not yet implemented.");
        public void FixLinkForOldMapleStory_OnClick() => Warning.Error("Fix inlink not yet implemented.");
        public void AiBatchImageUpscaleEdit(float factor) => Warning.Error("AI upscale not yet implemented.");
    }
}
