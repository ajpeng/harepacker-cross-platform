using Avalonia.Controls;
using Avalonia.Input;
using HaRepacker.GUI.Input;
using HaRepacker.Models;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
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

            // Wire sub-panel save events back to WzObjects
            textEditor.SaveButtonClicked += (_, _) =>
            {
                if (SelectedNode?.WzObject is WzStringProperty strProp)
                {
                    strProp.Value = textEditor.GetText();
                    strProp.ParentImage.Changed = true;
                }
            };
            xyPanel.ButtonClicked += (_, _) =>
            {
                if (SelectedNode?.WzObject is WzVectorProperty vecProp)
                {
                    vecProp.X.Value = xyPanel.X;
                    vecProp.Y.Value = xyPanel.Y;
                    vecProp.ParentImage.Changed = true;
                }
            };
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
            ShowObjectValue(node.WzObject);
        }

        private void ShowObjectValue(WzObject? obj)
        {
            imageViewer.IsVisible = false;
            textEditor.IsVisible = false;
            xyPanel.IsVisible = false;
            valuePanel.IsVisible = false;
            valueBox.IsEnabled = true;
            if (obj == null) return;

            if (obj is WzCanvasProperty canvas)
            {
                var bmp = canvas.GetLinkedWzCanvasBitmap();
                imageViewer.SetImage(bmp);
                imageViewer.IsVisible = true;
            }
            else if (obj is WzPngProperty png)
            {
                imageViewer.SetImage(png.GetImage(false));
                imageViewer.IsVisible = true;
            }
            else if (obj is WzStringProperty strProp)
            {
                textEditor.SetText(strProp.Value);
                textEditor.IsVisible = true;
            }
            else if (obj is WzVectorProperty vecProp)
            {
                xyPanel.X = vecProp.X.Value;
                xyPanel.Y = vecProp.Y.Value;
                xyPanel.IsVisible = true;
            }
            else if (obj is WzUOLProperty uol)
            {
                valueTypeLabel.Text = $"UOL Link (resolves to: {uol.LinkValue?.GetType().Name ?? "null"})";
                valueBox.Text = uol.Value;
                valuePanel.IsVisible = true;
            }
            else if (obj is WzIntProperty intProp)
            {
                valueTypeLabel.Text = "Int (32-bit)";
                valueBox.Text = intProp.Value.ToString();
                valuePanel.IsVisible = true;
            }
            else if (obj is WzLongProperty longProp)
            {
                valueTypeLabel.Text = "Long (64-bit)";
                valueBox.Text = longProp.Value.ToString();
                valuePanel.IsVisible = true;
            }
            else if (obj is WzShortProperty shortProp)
            {
                valueTypeLabel.Text = "Short (16-bit)";
                valueBox.Text = shortProp.Value.ToString();
                valuePanel.IsVisible = true;
            }
            else if (obj is WzFloatProperty floatProp)
            {
                valueTypeLabel.Text = "Float";
                valueBox.Text = floatProp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                valuePanel.IsVisible = true;
            }
            else if (obj is WzDoubleProperty doubleProp)
            {
                valueTypeLabel.Text = "Double";
                valueBox.Text = doubleProp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                valuePanel.IsVisible = true;
            }
            else if (obj is WzNullProperty)
            {
                valueTypeLabel.Text = "Null";
                valueBox.Text = "(null)";
                valueBox.IsEnabled = false;
                valuePanel.IsVisible = true;
            }
        }

        private void OnValueApplyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var obj = SelectedNode?.WzObject;
            if (obj == null) return;
            string text = valueBox.Text ?? "";
            try
            {
                if (obj is WzUOLProperty uol)
                { uol.Value = text; }
                else if (obj is WzIntProperty ip)
                { ip.Value = int.Parse(text, System.Globalization.CultureInfo.InvariantCulture); }
                else if (obj is WzLongProperty lp)
                { lp.Value = long.Parse(text, System.Globalization.CultureInfo.InvariantCulture); }
                else if (obj is WzShortProperty sp)
                { sp.Value = short.Parse(text, System.Globalization.CultureInfo.InvariantCulture); }
                else if (obj is WzFloatProperty fp)
                { fp.Value = float.Parse(text, System.Globalization.CultureInfo.InvariantCulture); }
                else if (obj is WzDoubleProperty dp)
                { dp.Value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture); }

                if (obj is WzImageProperty imgProp && imgProp.ParentImage != null)
                    imgProp.ParentImage.Changed = true;
            }
            catch (FormatException)
            {
                Warning.Error($"Invalid value '{text}' for {obj.GetType().Name}.");
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

        // ── Copy / Paste ─────────────────────────────────────────────────────

        private static readonly List<WzObject> _clipboard = new();
        private bool _pasteTaskActive = false;

        public void DoCopy()
        {
            if (!Warning.Warn("Copy the selected node(s) to the clipboard?")) return;
            if (_pasteTaskActive) return;

            foreach (var obj in _clipboard) obj.Dispose();
            _clipboard.Clear();

            if (SelectedNode?.WzObject is WzObject wzObj)
            {
                var clone = CloneWzObject(wzObj);
                if (clone != null) _clipboard.Add(clone);
            }
        }

        public async System.Threading.Tasks.Task DoPasteAsync(Avalonia.Controls.Window owner)
        {
            if (!Warning.Warn("Paste clipboard contents into the selected node?")) return;
            if (_pasteTaskActive || _clipboard.Count == 0) return;

            var parentNode = SelectedNode;
            if (parentNode == null) return;
            var parentObj = parentNode.WzObject is WzFile wf ? wf.WzDirectory : parentNode.WzObject;

            _pasteTaskActive = true;
            var replaceAll = ReplaceResult.NoneSelectedYet;
            try
            {
                foreach (var obj in _clipboard)
                {
                    if (!((obj is WzDirectory || obj is WzImage) && parentObj is WzDirectory)
                        && !(obj is WzImageProperty && parentObj is IPropertyContainer))
                        continue;

                    var clone = CloneWzObject(obj);
                    if (clone == null) continue;

                    var existing = WzNode.GetChildNode(parentNode, clone.Name);
                    if (existing != null)
                    {
                        bool replace;
                        if (replaceAll == ReplaceResult.YesToAll) replace = true;
                        else if (replaceAll == ReplaceResult.NoToAll) break;
                        else
                        {
                            var (_, result) = await InputDialogs.ShowReplaceAsync(owner, clone.Name);
                            if (result == ReplaceResult.YesToAll) replaceAll = ReplaceResult.YesToAll;
                            else if (result == ReplaceResult.NoToAll) { replaceAll = ReplaceResult.NoToAll; break; }
                            replace = result == ReplaceResult.Yes || result == ReplaceResult.YesToAll;
                        }
                        if (!replace) continue;
                        existing.DeleteNode();
                        parentNode.Nodes.Remove(existing);
                    }
                    parentNode.AddObject(clone, _undoMan);
                }
            }
            finally { _pasteTaskActive = false; }
        }

        private static WzObject? CloneWzObject(WzObject obj)
        {
            if (obj is WzDirectory)
            { Warning.Error("WZ directories cannot be copied."); return null; }
            if (obj is WzImage img) return img.DeepClone();
            if (obj is WzImageProperty prop) return prop.DeepClone();
            ErrorLogger.Log(ErrorLevel.MissingFeature, $"Cannot clone WzObject type: {obj.GetType().Name}");
            return null;
        }

        public void SortNodesRecursively(WzNode node, bool viewOnly)
        {
            var sorted = node.Nodes.OrderBy(n => n.Name).ToList();
            node.Nodes.Clear();
            foreach (var n in sorted) node.Nodes.Add(n);
        }

        public void SortNodeProperties(WzNode node) => SortNodesRecursively(node, true);

        public void StartAnimateSelectedCanvas()
        {
            var nodes = GetSelectedNodesForAnimation();
            if (nodes.Count == 0)
            { Warning.Error("Please select one or more canvas/animation nodes."); return; }
            var win = new ImageAnimationPreviewWindow(nodes, nodes[0].Name);
            win.Run();
        }

        private List<WzNode> GetSelectedNodesForAnimation()
        {
            var result = new List<WzNode>();
            if (SelectedNode is WzNode sel) result.Add(sel);
            return result;
        }
        public void SaveImageAnimation_Click() => Warning.Error("Save animation not yet implemented.");
        public void FixLinkForOldMapleStory_OnClick() => Warning.Error("Fix inlink not yet implemented.");
        public void AiBatchImageUpscaleEdit(float factor) => Warning.Error("AI upscale not yet implemented.");
    }
}
