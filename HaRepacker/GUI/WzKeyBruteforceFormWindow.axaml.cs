using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HaRepacker.Converters;
using MapleLib.Helpers;
using MapleLib.PacketLib;
using MapleLib.WzLib.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class WzKeyBruteforceFormWindow : Window
    {
        private Task? _runningTask;
        private CancellationTokenSource _cts = new();

        private ulong _tries;
        private DateTime _startTime;
        private volatile bool _completed;

        private System.Timers.Timer? _uiTimer;

        public WzKeyBruteforceFormWindow()
        {
            InitializeComponent();
            Closed += (_, _) =>
            {
                _cts.Cancel();
                _completed = true;
                _uiTimer?.Stop();
            };
        }

        public static void ShowWindow(Window? owner)
        {
            var win = new WzKeyBruteforceFormWindow();
            if (owner != null) win.ShowDialog(owner);
            else win.Show();
        }

        private async void OnStartStopClick(object? sender, RoutedEventArgs e)
        {
            if (_runningTask != null && !_runningTask.IsCompleted)
            {
                _cts.Cancel();
                _completed = true;
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a WZ file for brute-forcing",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } } }
            });
            if (files == null || files.Count == 0) return;
            string wzPath = files[0].TryGetLocalPath() ?? files[0].Path.LocalPath;

            button_startStop.Content = "Stop";
            button_startStop.IsEnabled = false;

            _cts = new CancellationTokenSource();
            _tries = 0;
            _startTime = DateTime.Now;
            _completed = false;

            int processorCount = Math.Max(1, Environment.ProcessorCount * 3);

            _uiTimer = new System.Timers.Timer(2000);
            _uiTimer.Elapsed += (_, _) =>
            {
                if (_completed) { _uiTimer?.Stop(); return; }
                var elapsed = DateTime.Now - _startTime;
                string duration = $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                ulong tries = _tries;
                Dispatcher.UIThread.Post(() =>
                {
                    label_duration.Text = duration;
                    label_ivTries.Text = tries.ToString();
                });
            };
            _uiTimer.Start();

            var cpuIds = new List<int>();
            for (int i = 0; i < processorCount; i++) cpuIds.Add(i);

            _runningTask = Task.Run(() =>
            {
                Thread.Sleep(1000);
                var opts = new ParallelOptions { MaxDegreeOfParallelism = processorCount, CancellationToken = _cts.Token };
                try
                {
                    Parallel.ForEach(cpuIds, opts, cpuId => BruteforceTask(cpuId, processorCount, wzPath));
                }
                catch (OperationCanceledException) { }
            }, _cts.Token);

            await _runningTask.ContinueWith(_ => { }, TaskScheduler.Default);

            _uiTimer.Stop();
            Dispatcher.UIThread.Post(() =>
            {
                button_startStop.Content = "Start brute-forcing";
                button_startStop.IsEnabled = true;
            });
        }

        private void BruteforceTask(int cpuId, int processorCount, string wzPath)
        {
            const long start = int.MinValue;
            const long end = int.MaxValue;
            long range = (end - start) / processorCount;
            long rangeStart = start + range * cpuId;
            long rangeEnd = start + range * (cpuId + 1);

            for (long i = rangeStart; i < rangeEnd; i++)
            {
                if (_completed || _cts.IsCancellationRequested) break;

                var bytes = new byte[4];
                unsafe { fixed (byte* p = bytes) { *(int*)p = (int)i; } }

                if (WzTool.TryBruteforcingWzIVKey(wzPath, bytes))
                {
                    _completed = true;
                    var writer = new PacketWriter(4);
                    writer.WriteBytes(bytes);
                    string hexStr = HexTool.ToString(writer.ToArray());
                    Debug.WriteLine("WzKey found: " + hexStr);
                    ErrorLogger.Log(ErrorLevel.Info, $"[WzKeyBruteforce] Found key: {hexStr}");

                    Dispatcher.UIThread.Post(() =>
                    {
                        label_key.Text = hexStr;
                        Warning.Error($"Found the encryption key:\r\n{hexStr}");
                    });
                    _cts.Cancel();
                    break;
                }
                _tries++;
            }
        }
    }
}
