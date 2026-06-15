using HaRepacker;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace HaRepacker.Models
{
    public class WzNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isChanged;

        public WzObject? WzObject { get; }
        public bool IsPlaceholder { get; }
        public ObservableCollection<WzNode> Nodes { get; } = new ObservableCollection<WzNode>();

        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public bool IsChanged
        {
            get => _isChanged;
            set { _isChanged = value; OnPropertyChanged(nameof(IsChanged)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public bool IsWzObjectAddedManually { get; private set; }

        public WzNode(WzObject sourceObject, bool isWzObjectAddedManually = false)
        {
            WzObject = sourceObject ?? throw new System.ArgumentNullException(nameof(sourceObject));
            IsWzObjectAddedManually = isWzObjectAddedManually;
            IsChanged = isWzObjectAddedManually;
            ParseChilds(sourceObject);
        }

        private WzNode(bool placeholder)
        {
            IsPlaceholder = true;
            Name = "...";
        }

        public static WzNode CreatePlaceholder() => new WzNode(true);

        private void ParseChilds(WzObject sourceObject)
        {
            Name = sourceObject.Name;
            sourceObject.HRTag = this;

            WzObject obj = sourceObject;
            if (obj is WzFile wzFile)
                obj = wzFile.WzDirectory;

            if (obj is VirtualWzDirectory virtualDir)
            {
                if (VirtualDirectoryMayHaveChildren(virtualDir))
                    Nodes.Add(WzNode.CreatePlaceholder());
            }
            else if (obj is WzDirectory wzDir)
            {
                foreach (WzDirectory dir in wzDir.WzDirectories)
                    Nodes.Add(new WzNode(dir));
                foreach (WzImage img in wzDir.WzImages)
                    Nodes.Add(new WzNode(img));
            }
            else if (obj is WzImage image && image.Parsed)
            {
                foreach (WzImageProperty prop in image.WzProperties)
                    Nodes.Add(new WzNode(prop));
            }
            else if (obj is IPropertyContainer container)
            {
                foreach (WzImageProperty prop in container.WzProperties)
                    Nodes.Add(new WzNode(prop));
            }
        }

        private static bool VirtualDirectoryMayHaveChildren(VirtualWzDirectory virtualDir)
        {
            try
            {
                string? path = virtualDir.FilesystemPath;
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    return false;
                if (Directory.EnumerateDirectories(path).Take(1).Any()) return true;
                if (Directory.EnumerateFiles(path, "*.img").Take(1).Any()) return true;
                return false;
            }
            catch { return true; }
        }

        public void DeleteNode()
        {
            if (WzObject is WzImageProperty property && property.ParentImage != null)
                property.ParentImage.Changed = true;
            WzObject?.Remove();
        }

        public void ChangeName(string name)
        {
            Name = name;
            if (WzObject != null) WzObject.Name = name;
            ChangedNodeProperty();
        }

        public void ChangedNodeProperty()
        {
            if (WzObject is WzImageProperty property)
                property.ParentImage.Changed = true;
            IsWzObjectAddedManually = true;
            IsChanged = true;
        }

        public bool CanHaveChildren =>
            WzObject is WzFile || WzObject is WzDirectory || WzObject is WzImage || WzObject is IPropertyContainer;

        public static WzNode? GetChildNode(WzNode parentNode, string name)
        {
            foreach (WzNode node in parentNode.Nodes)
                if (node.Name == name)
                    return node;
            return null;
        }

        public void Reparse()
        {
            Nodes.Clear();
            ParseChilds(WzObject);
        }

        public string GetTypeName() => WzObject.GetType().Name;

        public WzNode AddObject(WzObject wzObject, UndoRedoManager? undoMan)
        {
            // Attach the WzObject to our WzObject hierarchy
            WzObject target = WzObject is WzFile wf ? wf.WzDirectory : WzObject;
            if (target is WzDirectory dir)
            {
                if (wzObject is WzImage img) dir.AddImage(img);
                else if (wzObject is WzDirectory childDir) dir.AddDirectory(childDir);
            }
            else if (target is WzImage image && wzObject is WzImageProperty imgProp)
                image.AddProperty(imgProp);
            else if (target is IPropertyContainer container && wzObject is WzImageProperty prop)
                container.AddProperty(prop);

            var newNode = new WzNode(wzObject, true);
            Nodes.Add(newNode);
            undoMan?.AddUndoBatch(new System.Collections.Generic.List<UndoRedoAction>
                { UndoRedoManager.ObjectAdded(this, newNode) });
            return newNode;
        }

        public void AddChildNode(WzNode child)
        {
            if (child.WzObject != null && WzObject != null)
            {
                WzObject target = WzObject is WzFile wf ? wf.WzDirectory : WzObject;
                if (target is WzDirectory dir)
                {
                    if (child.WzObject is WzImage img) dir.AddImage(img);
                    else if (child.WzObject is WzDirectory childDir) dir.AddDirectory(childDir);
                }
                else if (target is WzImage image && child.WzObject is WzImageProperty imgProp)
                    image.AddProperty(imgProp);
                else if (target is IPropertyContainer container && child.WzObject is WzImageProperty prop)
                    container.AddProperty(prop);
            }
            Nodes.Add(child);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

