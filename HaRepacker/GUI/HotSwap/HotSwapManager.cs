using Avalonia.Threading;
using HaRepacker.Models;
using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace HaRepacker.GUI.HotSwap
{
    public class HotSwapManager : IDisposable
    {
        private readonly Action<string, bool> _showNotificationAction;
        private readonly HotSwapNotificationBar _notificationBar;
        private ImgDirectoryWatcherService _watcherService;
        private readonly ConcurrentDictionary<string, WzNode> _watchedNodes = new();
        private readonly ConcurrentDictionary<string, bool> _unsavedChanges = new();
        private bool _disposed;
        private bool _isEnabled;

        public bool IsEnabled => _isEnabled;
        public HotSwapNotificationBar NotificationBar => _notificationBar;

        public event EventHandler<ImgFileReloadedEventArgs> ImgFileReloaded;
        public event EventHandler<ImgFileAddedEventArgs> ImgFileAddedToTree;

        public HotSwapManager()
        {
            _notificationBar = new HotSwapNotificationBar();
        }

        public void Enable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HotSwapManager));
            if (_isEnabled) return;
            if (!HotSwapConstants.EnableImgFileWatching) return;

            _watcherService = new ImgDirectoryWatcherService(
                HotSwapConstants.DebounceMs,
                HotSwapConstants.TrackContentHash);

            _watcherService.ImgFileModified += OnImgFileModified;
            _watcherService.ImgFileAdded += OnImgFileAdded;
            _watcherService.ImgFileDeleted += OnImgFileDeleted;
            _watcherService.ImgFileRenamed += OnImgFileRenamed;
            _watcherService.WatcherError += OnWatcherError;

            _isEnabled = true;
        }

        public void Disable()
        {
            if (!_isEnabled) return;
            _watcherService?.Dispose();
            _watcherService = null;
            _watchedNodes.Clear();
            _notificationBar.ClearAll();
            _isEnabled = false;
        }

        public void WatchDirectory(string directoryPath, WzNode node)
        {
            if (!_isEnabled || _watcherService == null) return;
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;
            _watcherService.WatchDirectory(directoryPath);
            _watchedNodes[directoryPath] = node;
        }

        public void UnwatchDirectory(string directoryPath)
        {
            if (!_isEnabled || _watcherService == null) return;
            _watcherService.UnwatchDirectory(directoryPath);
            _watchedNodes.TryRemove(directoryPath, out _);
        }

        public void RecordFileOpen(string filePath) => _watcherService?.RecordFileState(filePath);

        public void BeginSaveOperation(string filePath) => _watcherService?.IgnorePath(filePath);

        public void EndSaveOperation(string filePath)
        {
            if (_watcherService != null)
            {
                _ = _watcherService.UnignorePathDelayed(filePath, 500);
                _watcherService.RecordFileState(filePath);
            }
            _unsavedChanges.TryRemove(filePath, out _);
        }

        public void BeginDirectorySaveOperation(string directoryPath) => _watcherService?.IgnoreDirectory(directoryPath);

        public void EndDirectorySaveOperation(string directoryPath)
        {
            if (_watcherService != null)
                _ = _watcherService.UnignoreDirectoryDelayed(directoryPath, 500);
        }

        public void MarkFileAsModified(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath)) _unsavedChanges[filePath] = true;
        }

        public bool HasUnsavedChanges(string filePath) =>
            _unsavedChanges.TryGetValue(filePath, out var hasChanges) && hasChanges;

        public ImgChangeType CheckForExternalChanges(string filePath) =>
            _watcherService?.GetChangeType(filePath) ?? ImgChangeType.None;

        private void OnImgFileModified(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled) return;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadFile(e.FilePath);
                ShowNotification(e.FilePath, e.ChangeType);
            });
        }

        private void OnImgFileAdded(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled) return;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddFileToTree(e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Added);
            });
        }

        private void OnImgFileDeleted(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled) return;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                RemoveFileFromTree(e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Deleted);
            });
        }

        private void OnImgFileRenamed(object sender, ImgFileModifiedEventArgs e)
        {
            if (!_isEnabled) return;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                HandleRename(e.OldPath, e.FilePath);
                ShowNotification(e.FilePath, ImgChangeType.Renamed, e.OldPath);
            });
        }

        private void OnWatcherError(object sender, ErrorEventArgs e) =>
            System.Diagnostics.Debug.WriteLine($"FileSystemWatcher error: {e.GetException()?.Message}");

        private void ShowNotification(string filePath, ImgChangeType changeType, string oldPath = null)
        {
            if (!HotSwapConstants.ShowNotifications) return;
            _notificationBar.QueueNotification(new FileModificationInfo
            {
                FilePath = filePath,
                ChangeType = changeType,
                OldPath = oldPath,
                DetectedAt = DateTime.Now
            });
        }

        private void ReloadFile(string filePath)
        {
            try
            {
                var node = FindNodeByFilePath(filePath);
                if (node == null) return;

                if (node.WzObject is WzImage wzImage)
                {
                    wzImage.ParseImage();
                    node.Nodes.Clear();
                    ImgFileReloaded?.Invoke(this, new ImgFileReloadedEventArgs(filePath, node));
                }
                else if (node.WzObject is VirtualWzDirectory virtualDir)
                {
                    virtualDir.Refresh();
                    node.Reparse();
                    ImgFileReloaded?.Invoke(this, new ImgFileReloadedEventArgs(filePath, node));
                }

                _unsavedChanges.TryRemove(filePath, out _);
                _watcherService?.RecordFileState(filePath);
            }
            catch (Exception ex)
            {
                Warning.Error($"Error reloading {Path.GetFileName(filePath)}:\n{ex.Message}");
            }
        }

        private void RemoveFileFromTree(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            string directory = Path.GetDirectoryName(filePath);
            var parentNode = FindNodeByDirectoryPath(directory);

            if (parentNode?.WzObject is VirtualWzDirectory virtualDir)
            {
                virtualDir.RemoveImageByPath(filePath);
                for (int i = parentNode.Nodes.Count - 1; i >= 0; i--)
                {
                    var childNode = parentNode.Nodes[i];
                    if ((childNode.WzObject is WzImage img &&
                         img.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true) ||
                        (childNode.WzObject is ImgFileWzImageReference imgRef &&
                         imgRef.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        childNode.DeleteNode();
                        break;
                    }
                }
            }

            _unsavedChanges.TryRemove(filePath, out _);
        }

        private void AddFileToTree(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            var parentNode = FindNodeByDirectoryPath(directory);
            if (parentNode == null) return;

            try
            {
                if (parentNode.WzObject is VirtualWzDirectory virtualDir)
                {
                    virtualDir.Refresh();
                    string fileName = Path.GetFileName(filePath);
                    if (parentNode.Nodes.Count > 0 &&
                        !(parentNode.Nodes.Count == 1 && parentNode.Nodes[0].WzObject == null))
                    {
                        var newNode = new WzNode(new ImgFileWzImageReference(virtualDir, fileName));
                        parentNode.Nodes.Add(newNode);
                        ImgFileAddedToTree?.Invoke(this, new ImgFileAddedEventArgs(filePath, newNode));
                    }
                }
                _watcherService?.RecordFileState(filePath);
            }
            catch (Exception ex)
            {
                Warning.Error($"Error adding {Path.GetFileName(filePath)} to tree:\n{ex.Message}");
            }
        }

        private void HandleRename(string oldPath, string newPath)
        {
            var node = FindNodeByFilePath(oldPath);
            if (node != null)
            {
                node.ChangeName(Path.GetFileName(newPath));
                if (_unsavedChanges.TryRemove(oldPath, out var hasChanges))
                    _unsavedChanges[newPath] = hasChanges;
            }
        }

        private WzNode FindNodeByFilePath(string filePath)
        {
            foreach (var kvp in _watchedNodes)
            {
                var result = FindNodeRecursive(kvp.Value, filePath);
                if (result != null) return result;
            }
            return null;
        }

        private WzNode FindNodeRecursive(WzNode parent, string filePath)
        {
            if (parent == null) return null;

            if (parent.WzObject is VirtualWzDirectory virtualDir)
            {
                string fileName = Path.GetFileName(filePath);
                string expectedPath = virtualDir.GetImageFilePath(fileName);
                if (expectedPath?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    foreach (var child in parent.Nodes)
                    {
                        if (child.WzObject is WzImage img && img.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true)
                            return child;
                        if (child.WzObject is ImgFileWzImageReference imgRef && imgRef.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                            return child;
                    }
                }
            }

            if (parent.WzObject is WzImage wzImg)
            {
                string fileName = Path.GetFileName(filePath);
                if (wzImg.Name?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true) return parent;
            }

            foreach (var child in parent.Nodes)
            {
                var result = FindNodeRecursive(child, filePath);
                if (result != null) return result;
            }
            return null;
        }

        private WzNode FindNodeByDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return null;
            string normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);
            if (_watchedNodes.TryGetValue(normalizedPath, out var node)) return node;
            foreach (var kvp in _watchedNodes)
            {
                var result = FindDirectoryNodeRecursive(kvp.Value, normalizedPath);
                if (result != null) return result;
            }
            return null;
        }

        private WzNode FindDirectoryNodeRecursive(WzNode parent, string directoryPath)
        {
            if (parent == null) return null;
            if (parent.WzObject is VirtualWzDirectory virtualDir)
            {
                string dirPath = Path.GetFullPath(virtualDir.FilesystemPath).TrimEnd(Path.DirectorySeparatorChar);
                if (dirPath.Equals(directoryPath, StringComparison.OrdinalIgnoreCase)) return parent;
            }
            foreach (var child in parent.Nodes)
            {
                var result = FindDirectoryNodeRecursive(child, directoryPath);
                if (result != null) return result;
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _notificationBar.Dispose();
            Disable();
        }
    }

    public class ImgFileReloadedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public WzNode Node { get; }
        public ImgFileReloadedEventArgs(string filePath, WzNode node) { FilePath = filePath; Node = node; }
    }

    public class ImgFileAddedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public WzNode Node { get; }
        public ImgFileAddedEventArgs(string filePath, WzNode node) { FilePath = filePath; Node = node; }
    }
}
