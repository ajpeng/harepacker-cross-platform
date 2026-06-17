/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using HaCreator.Wz;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HaCreator.MapEditor
{
    public class BackupManager
    {
        private const string userObjsFileName = "userobjs.ham";

        private readonly MultiBoard _multiBoard;
        private readonly HaCreatorStateManager _hcsm;
        private readonly TabControl _tabs;
        private bool _enabled = false;
        private System.Threading.Timer? _timer;

        private DateTime _lastBackupTime = DateTime.Now;
        private DateTime _lastInteractionTime = DateTime.Now;

        public BackupManager(MultiBoard multiBoard, HaCreatorStateManager hcsm, TabControl tabs)
        {
            _multiBoard = multiBoard;
            _hcsm = hcsm;
            _tabs = tabs;
        }

        private string GetBasePath() =>
            Path.Combine(Program.GetLocalSettingsFolder(), "Backups");

        public void Start()
        {
            _enabled = true;
            _timer = new System.Threading.Timer(_ =>
            {
                try { BackupCheck(); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine($"Backup failed: {e.Message}"); }
            }, null, 30_000, 30_000);
        }

        public void Stop() { _timer?.Dispose(); _timer = null; _enabled = false; }

        public void OnUserInteraction() => _lastInteractionTime = DateTime.Now;

        public void OnBackup() => _lastBackupTime = DateTime.Now;

        public bool IsUserIdleFor(int milliseconds) =>
            (DateTime.Now - _lastInteractionTime).TotalMilliseconds >= milliseconds;

        public bool IsBackupDelayedFor(int milliseconds) =>
            (DateTime.Now - _lastBackupTime).TotalMilliseconds >= milliseconds;

        public void BackupCheck()
        {
            if (!_enabled || !UserSettings.BackupEnabled) return;
            if (!IsUserIdleFor(UserSettings.BackupIdleTime) && !IsBackupDelayedFor(UserSettings.BackupMaxTime))
                return;

            lock (_multiBoard)
            {
                if (_multiBoard.SelectedBoard == null ||
                    _multiBoard.SelectedBoard.Mouse == null ||
                    _multiBoard.SelectedBoard.Mouse.State != Input.MouseState.Selection ||
                    _multiBoard.SelectedBoard.Mouse.BoundItems.Count > 0)
                    return;
            }

            OnBackup();
            Dictionary<string, string> ioQueue = new();
            Dictionary<string, SerializationManager> serQueue = new();

            lock (this)
            {
                lock (_multiBoard)
                {
                    if (_multiBoard.UserObjects?.Dirty == true)
                    {
                        _multiBoard.UserObjects.Dirty = false;
                        ioQueue[userObjsFileName] = _multiBoard.UserObjects.SerializedForm;
                    }
                    foreach (Board board in _multiBoard.Boards)
                    {
                        if (board.Dirty)
                        {
                            board.Dirty = false;
                            serQueue[board.UniqueID.ToString() + ".ham"] = board.SerializationManager;
                        }
                    }
                }

                foreach (var kv in serQueue)
                    ioQueue[kv.Key] = kv.Value.SerializeBoard(false);

                if (ioQueue.Count > 0)
                {
                    string basePath = GetBasePath();
                    if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                    foreach (var kv in ioQueue)
                        File.WriteAllText(Path.Combine(basePath, kv.Key), kv.Value);
                }
            }
        }

        public void DeleteBackup(int uid)
        {
            lock (this)
            {
                string backup = Path.Combine(GetBasePath(), uid + ".ham");
                if (File.Exists(backup)) File.Delete(backup);
            }
        }

        public void ClearBackups()
        {
            lock (this)
            {
                string basePath = GetBasePath();
                if (Directory.Exists(basePath)) Directory.Delete(basePath, true);
            }
        }

        public async Task<bool> AttemptRestoreAsync(Window? owner)
        {
            Dictionary<string, string> loadedFiles = new();
            lock (this)
            {
                string basePath = GetBasePath();
                if (!Directory.Exists(basePath)) return false;

                foreach (string file in Directory.GetFiles(basePath, "*.ham"))
                    loadedFiles[Path.GetFileName(file)] = File.ReadAllText(file);
            }

            if (loadedFiles.Count == 0) return false;

            // Ask user whether to restore
            bool restore = await ConfirmAsync(
                owner,
                "Restore Backup",
                $"HaCreator found {loadedFiles.Count} backup file(s) from a previous session.\n\nRestore them?");

            if (!restore)
            {
                ClearBackups();
                return false;
            }

            // Load each backup map
            lock (_multiBoard)
            {
                foreach (var kv in loadedFiles)
                {
                    if (kv.Key == userObjsFileName)
                    {
                        _multiBoard.UserObjects?.DeserializeObjects(kv.Value);
                        continue;
                    }
                    _hcsm.LoadMap();
                    if (_multiBoard.SelectedBoard != null)
                        _multiBoard.SelectedBoard.SerializationManager.DeserializeBoard(kv.Value);
                }
            }
            ClearBackups();
            return loadedFiles.Count > 0;
        }

        private static async Task<bool> ConfirmAsync(Window? owner, string title, string message)
        {
            bool result = false;
            var btnYes = new Button { Content = "Yes", IsDefault = true };
            var btnNo  = new Button { Content = "No" };
            var win = new Window
            {
                Title = title, Width = 360, Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 10,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 6,
                            Children = { btnYes, btnNo }
                        }
                    }
                }
            };
            btnYes.Click += (_, _) => { result = true;  win.Close(); };
            btnNo.Click  += (_, _) => { result = false; win.Close(); };
            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
            return result;
        }
    }
}
