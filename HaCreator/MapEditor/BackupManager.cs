/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using HaCreator.Wz;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapEditor
{
    public class BackupManager
    {
        private const string userObjsFileName = "userobjs.ham";

        private readonly MultiBoard _multiBoard;
        private readonly HaCreatorStateManager _hcsm;
        private readonly TabControl _tabs;
        private bool _enabled = false;

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

        public void Start() => _enabled = true;

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

        public bool AttemptRestore()
        {
            Dictionary<string, string> loadedFiles = new();
            lock (this)
            {
                string basePath = GetBasePath();
                if (!Directory.Exists(basePath)) return false;

                // TODO: Show Avalonia dialog for recovery confirmation
                ClearBackups();
                return false;
            }
        }
    }
}
