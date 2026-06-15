using Avalonia.Controls;
using HaRepacker.GUI.Input;
using HaRepacker.GUI.Panels;
using HaRepacker.Models;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HaRepacker
{
    public class ContextMenuManager
    {
        private readonly MainPanel _panel;
        private readonly UndoRedoManager _undoMan;
        private WzNode? _currNode;

        public ContextMenuManager(MainPanel panel, UndoRedoManager undoMan)
        {
            _panel = panel;
            _undoMan = undoMan;
        }

        private Window? OwnerWindow =>
            Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime dt
                ? dt.MainWindow : null;

        public ContextMenu CreateMenu(WzNode node, WzObject tag)
        {
            _currNode = node;
            var menu = new ContextMenu();

            void Add(MenuItem mi) => menu.Items.Add(mi);
            void Sep() => menu.Items.Add(new Separator());

            if (tag is WzImage || tag is IPropertyContainer)
            {
                Add(MakeAddPropsSubmenu());
                Add(MakeItem("Rename", () => RenameAsync(node)));
                if (IsFromVirtualWzDirectory(tag))
                {
                    Add(MakeItem("Save to IMG", () => SaveImgNodeAsync(node)));
                    if (tag is WzImage) Add(MakeItem("Delete IMG File", () => DeleteImgFileAsync(node)));
                }
                else Add(MakeItem("Remove", () => RemoveNode(node)));
            }
            else if (tag is WzImageProperty)
            {
                Add(MakeItem("Rename", () => RenameAsync(node)));
                if (IsFromVirtualWzDirectory(tag)) Add(MakeItem("Save to IMG", () => SaveImgNodeAsync(node)));
                Add(MakeItem("Remove", () => RemoveNode(node)));
            }
            else if (tag is VirtualWzDirectory)
            {
                Add(MakeItem("Create New IMG File", () => CreateNewImgFileAsync(node)));
                Add(MakeAddDirsSubmenu());
                Add(MakeItem("Rename", () => RenameAsync(node)));
                Add(MakeItem("Save to IMG", () => SaveImgNodeAsync(node)));
                Add(MakeItem("Unload", () => UnloadNode(node)));
            }
            else if (tag is WzDirectory)
            {
                Add(MakeAddDirsSubmenu());
                Add(MakeItem("Rename", () => RenameAsync(node)));
                Add(MakeItem("Remove", () => RemoveNode(node)));
            }
            else if (tag is WzFile)
            {
                Add(MakeAddDirsSubmenu());
                Add(MakeItem("Rename", () => RenameAsync(node)));
                Add(MakeItem("Save", () => Warning.Error("Save file: not yet implemented.")));
                Add(MakeItem("Unload", () => UnloadNode(node)));
                Add(MakeItem("Reload", () => ReloadNode(node)));
            }

            Sep();
            Add(MakeSortSubmenu(tag));
            Add(MakeBatchSubmenu());

            if (tag is WzCanvasProperty)
                Add(MakeItem("Animate", () => _panel.StartAnimateSelectedCanvas()));
            if (tag.GetType() == typeof(WzSubProperty))
                Add(MakeItem("Save Animation", () => _panel.SaveImageAnimation_Click()));

            return menu;
        }

        // ── Item factories ────────────────────────────────────────────────

        private MenuItem MakeItem(string header, Action action)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => action();
            return mi;
        }

        private MenuItem MakeItem(string header, Func<Task> asyncAction)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += async (_, _) => await asyncAction();
            return mi;
        }

        private MenuItem MakeAddPropsSubmenu()
        {
            var sub = new MenuItem { Header = "Add" };
            sub.Items.Add(MakeItem("Canvas",  () => AddCanvasAsync(_currNode!)));
            sub.Items.Add(MakeItem("Convex",  () => AddNamedAsync(_currNode!, name => new WzConvexProperty(name))));
            sub.Items.Add(MakeItem("Double",  () => AddFloatAsync(_currNode!, (n, v) => new WzDoubleProperty(n, v))));
            sub.Items.Add(MakeItem("Float",   () => AddByteFloatAsync(_currNode!)));
            sub.Items.Add(MakeItem("Long",    () => AddLongAsync(_currNode!)));
            sub.Items.Add(MakeItem("Int",     () => AddIntAsync(_currNode!)));
            sub.Items.Add(MakeItem("Null",    () => AddNamedAsync(_currNode!, name => new WzNullProperty(name))));
            sub.Items.Add(MakeItem("Short",   () => AddShortAsync(_currNode!)));
            sub.Items.Add(MakeItem("Sound",   () => AddSoundAsync(_currNode!)));
            sub.Items.Add(MakeItem("String",  () => AddStringAsync(_currNode!)));
            sub.Items.Add(MakeItem("Sub",     () => AddNamedAsync(_currNode!, name => new WzSubProperty(name))));
            sub.Items.Add(MakeItem("UOL",     () => AddUolAsync(_currNode!)));
            sub.Items.Add(MakeItem("Vector",  () => AddVectorAsync(_currNode!)));
            return sub;
        }

        private MenuItem MakeAddDirsSubmenu()
        {
            var sub = new MenuItem { Header = "Add" };
            sub.Items.Add(MakeItem("Directory", () => AddDirectoryAsync(_currNode!)));
            sub.Items.Add(MakeItem("Image",     () => AddImageAsync(_currNode!)));
            return sub;
        }

        private MenuItem MakeSortSubmenu(WzObject tag)
        {
            var sub = new MenuItem { Header = "Sort" };
            sub.Items.Add(MakeItem("Sort child nodes", () => _panel.SortNodesRecursively(_currNode!, true)));
            if (tag.GetType() == typeof(WzSubProperty))
                sub.Items.Add(MakeItem("Sort properties by name", () => _panel.SortNodeProperties(_currNode!)));
            return sub;
        }

        private MenuItem MakeBatchSubmenu()
        {
            var aiSub = new MenuItem { Header = "AI Upscale Image" };
            aiSub.Items.Add(MakeItem("Quality only (0.25x)", () => _panel.AiBatchImageUpscaleEdit(0.25f)));
            aiSub.Items.Add(MakeItem("1.5x", () => _panel.AiBatchImageUpscaleEdit(0.375f)));
            aiSub.Items.Add(MakeItem("2x",   () => _panel.AiBatchImageUpscaleEdit(0.5f)));
            aiSub.Items.Add(MakeItem("4x",   () => _panel.AiBatchImageUpscaleEdit(1f)));

            var batch = new MenuItem { Header = "Batch Edit" };
            batch.Items.Add(MakeItem("Fix Inlink", () => _panel.FixLinkForOldMapleStory_OnClick()));
            batch.Items.Add(aiSub);
            return batch;
        }

        // ── Node operations ───────────────────────────────────────────────

        private async Task RenameAsync(WzNode node)
        {
            if (OwnerWindow is not Window owner) return;
            await _panel.PromptRenameWzTreeNode(node, owner);
        }

        private void RemoveNode(WzNode node)
        {
            _panel.PromptRemoveSelectedTreeNodes(_undoMan);
        }

        private void UnloadNode(WzNode node)
        {
            if (!Warning.Warn("Are you sure you want to unload this?")) return;
            var obj = node.WzObject;
            if (obj is VirtualWzDirectory vd) { vd.Dispose(); node.DeleteNode(); }
            else if (obj is WzFile wz) _panel.UnloadWzFile(wz);
        }

        private void ReloadNode(WzNode node)
        {
            if (!Warning.Warn("Are you sure you want to reload this file?")) return;
            _panel.ReloadWzFile(node.WzObject as WzFile);
        }

        // ── Add operations ────────────────────────────────────────────────

        private async Task AddNamedAsync(WzNode target, Func<string, WzObject> factory)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name) = await InputDialogs.ShowNameAsync(owner, "Add Property");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(factory(name!), _undoMan);
        }

        private async Task AddByteFloatAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowFloatAsync(owner, "Add Float");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzFloatProperty(name!, (float)(val ?? 0)), _undoMan);
        }

        private async Task AddFloatAsync(WzNode target, Func<string, double, WzObject> factory)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowFloatAsync(owner, "Add Property");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(factory(name!, val ?? 0), _undoMan);
        }

        private async Task AddIntAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowIntAsync(owner, "Add Int");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzIntProperty(name!, val ?? 0), _undoMan);
        }

        private async Task AddShortAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowIntAsync(owner, "Add Short");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzShortProperty(name!, (short)(val ?? 0)), _undoMan);
        }

        private async Task AddLongAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowLongAsync(owner, "Add Long");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzLongProperty(name!, val ?? 0), _undoMan);
        }

        private async Task AddSoundAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, path) = await InputDialogs.ShowSoundAsync(owner, "Add Sound");
            if (ok && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path))
                target.AddObject(new WzBinaryProperty(name!, path!), _undoMan);
        }

        private async Task AddStringAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowNameValueAsync(owner, "Add String");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzStringProperty(name!, val ?? ""), _undoMan);
        }

        private async Task AddUolAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, val) = await InputDialogs.ShowNameValueAsync(owner, "Add UOL Link");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzUOLProperty(name!, val ?? ""), _undoMan);
        }

        private async Task AddVectorAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, pt) = await InputDialogs.ShowVectorAsync(owner, "Add Vector");
            if (ok && !string.IsNullOrEmpty(name))
                target.AddObject(new WzVectorProperty(name!,
                    new WzIntProperty("X", pt?.X ?? 0),
                    new WzIntProperty("Y", pt?.Y ?? 0)), _undoMan);
        }

        private async Task AddCanvasAsync(WzNode target)
        {
            if (!RequirePropertyContainer(target)) return;
            if (OwnerWindow is not Window owner) return;
            var (ok, name, bitmaps) = await InputDialogs.ShowBitmapAsync(owner, "Add Canvas");
            if (!ok || bitmaps == null) return;
            int i = 0;
            foreach (var bm in bitmaps)
            {
                string propName = bitmaps.Count == 1 ? (name ?? "canvas") : $"{name}{i}";
                if (WzNode.GetChildNode(target, propName) != null) { Warning.Error($"Node '{propName}' already exists."); i++; continue; }
                var png = new WzPngProperty { PNG = bm };
                var canvas = new WzCanvasProperty(propName) { PngProperty = png };
                var newNode = target.AddObject(canvas, _undoMan);
                newNode.AddObject(new WzVectorProperty(WzCanvasProperty.OriginPropertyName,
                    new WzIntProperty("X", 0), new WzIntProperty("Y", 0)), _undoMan);
                i++;
            }
        }

        private async Task AddDirectoryAsync(WzNode target)
        {
            if (target.WzObject is not WzDirectory && target.WzObject is not WzFile)
            { Warning.Error("Cannot insert into this node type."); return; }
            if (OwnerWindow is not Window owner) return;
            var (ok, name) = await InputDialogs.ShowNameAsync(owner, "Add Directory");
            if (!ok || string.IsNullOrEmpty(name)) return;
            WzObject obj = target.WzObject;
            WzFile? topFile = obj as WzFile;
            if (topFile == null)
            {
                WzObject? cur = obj.Parent;
                while (cur != null) { if (cur is WzFile f) { topFile = f; break; } cur = cur.Parent; }
            }
            if (topFile == null) { Warning.Error("Cannot find top-level WZ file."); return; }
            target.AddObject(new WzDirectory(name, topFile), _undoMan);
        }

        private async Task AddImageAsync(WzNode target)
        {
            if (target.WzObject is not WzDirectory && target.WzObject is not WzFile)
            { Warning.Error("Cannot insert into this node type."); return; }
            if (OwnerWindow is not Window owner) return;
            var (ok, name) = await InputDialogs.ShowNameAsync(owner, "Add Image");
            if (ok && !string.IsNullOrEmpty(name)) target.AddObject(new WzImage(name) { Changed = true }, _undoMan);
        }

        // ── IMG filesystem operations ──────────────────────────────────────

        private async Task SaveImgNodeAsync(WzNode node)
        {
            var tag = node.WzObject;
            VirtualWzDirectory? vd = FindVirtualParent(tag);
            if (vd == null && tag is VirtualWzDirectory v) vd = v;
            if (vd == null) { Warning.Error("This item is not from an IMG filesystem directory."); return; }

            try
            {
                if (tag is ImgFileWzImageReference imgRef)
                {
                    var resolved = imgRef.Resolve();
                    if (resolved != null) tag = resolved;
                }
                if (tag is WzImage image)
                {
                    Warning.Error(vd.SaveImage(image)
                        ? $"Saved {image.Name} successfully."
                        : $"Failed to save {image.Name}.");
                }
                else if (tag is VirtualWzDirectory vd2)
                {
                    int n = vd2.SaveAllChangedImages();
                    Warning.Error(n > 0 ? $"Saved {n} image(s)." : "No changed images.");
                }
                else if (tag is WzImageProperty prop && prop.ParentImage != null)
                {
                    Warning.Error(vd.SaveImage(prop.ParentImage)
                        ? $"Saved {prop.ParentImage.Name}." : $"Failed to save {prop.ParentImage.Name}.");
                }
            }
            catch (Exception ex) { Warning.Error($"Error saving: {ex.Message}"); }
            await Task.CompletedTask;
        }

        private async Task CreateNewImgFileAsync(WzNode node)
        {
            if (node.WzObject is not VirtualWzDirectory vd) { Warning.Error("Please select a VirtualWzDirectory."); return; }
            if (OwnerWindow is not Window owner) return;
            var (ok, name) = await InputDialogs.ShowNameAsync(owner, "Create New IMG File");
            if (!ok || string.IsNullOrEmpty(name)) return;
            if (!name!.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) name += ".img";
            if (vd.ImageExists(name)) { Warning.Error($"File '{name}' already exists."); return; }
            try
            {
                string rel = string.IsNullOrEmpty(vd.RelativePath) ? name : Path.Combine(vd.RelativePath, name);
                var img = vd.Manager.CreateImage(vd.CategoryName, rel);
                if (img != null) node.Nodes.Add(new WzNode(img, true));
                else Warning.Error($"Failed to create '{name}'.");
            }
            catch (Exception ex) { Warning.Error($"Error: {ex.Message}"); }
        }

        private async Task DeleteImgFileAsync(WzNode node)
        {
            var tag = node.WzObject;
            WzImage? image = tag as WzImage ?? (tag is ImgFileWzImageReference r ? new WzImage(r.FileName) { Changed = false } : null);
            if (image == null) { Warning.Error("Please select an IMG file."); return; }
            var vd = FindVirtualParent(tag);
            if (vd == null) { Warning.Error("This file is not from an IMG filesystem."); return; }
            if (!Warning.Warn($"Delete '{image.Name}'? This permanently deletes the file from disk.")) return;
            try
            {
                string rel = string.IsNullOrEmpty(vd.RelativePath) ? image.Name : Path.Combine(vd.RelativePath, image.Name);
                if (vd.Manager.DeleteImage(vd.CategoryName, rel)) node.DeleteNode();
                else Warning.Error($"Failed to delete '{image.Name}'.");
            }
            catch (Exception ex) { Warning.Error($"Error: {ex.Message}"); }
            await Task.CompletedTask;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static bool IsFromVirtualWzDirectory(WzObject obj)
        {
            if (obj is VirtualWzDirectory) return true;
            WzObject? cur = obj;
            while (cur != null) { if (cur.Parent is VirtualWzDirectory) return true; cur = cur.Parent; }
            return false;
        }

        private static VirtualWzDirectory? FindVirtualParent(WzObject obj)
        {
            WzObject? cur = obj;
            while (cur != null) { if (cur.Parent is VirtualWzDirectory vd) return vd; cur = cur.Parent; }
            return null;
        }

        private static bool RequirePropertyContainer(WzNode node)
        {
            if (node.WzObject is IPropertyContainer || node.WzObject is WzImage) return true;
            Warning.Error("Cannot insert property into this node type.");
            return false;
        }
    }
}
