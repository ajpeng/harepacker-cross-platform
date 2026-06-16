using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MapleLib.Img;
using System;
using System.IO;

namespace HaRepacker.GUI.HotSwap
{
    public class FileModificationInfo
    {
        public string FilePath { get; set; }
        public ImgChangeType ChangeType { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool HasLocalChanges { get; set; }
        public string OldPath { get; set; }

        public string FileName => Path.GetFileName(FilePath);

        public string DisplayMessage => ChangeType switch
        {
            ImgChangeType.ContentChanged or ImgChangeType.SizeChanged => $"{FileName} reloaded",
            ImgChangeType.Deleted => $"{FileName} removed",
            ImgChangeType.Added => $"{FileName} added",
            ImgChangeType.Renamed => $"{Path.GetFileName(OldPath)} renamed to {FileName}",
            _ => $"{FileName} updated"
        };
    }

    public enum NotificationResponse { Reload, Ignore, IgnoreAll, KeepLocal, AddToTree }

    public class NotificationResponseEventArgs : EventArgs
    {
        public FileModificationInfo Modification { get; }
        public NotificationResponse Response { get; }
        public NotificationResponseEventArgs(FileModificationInfo modification, NotificationResponse response)
        {
            Modification = modification;
            Response = response;
        }
    }

    /// <summary>
    /// Avalonia notification bar control for hot-swap status messages.
    /// Embed as the first child of a DockPanel in the main window.
    /// </summary>
    public class HotSwapNotificationBar : IDisposable
    {
        private readonly Border _border;
        private readonly TextBlock _label;
        private DispatcherTimer _hideTimer;
        private const int DisplayDurationMs = 3000;

        public Border Control => _border;

        public event EventHandler<NotificationResponseEventArgs> UserResponse;

        public HotSwapNotificationBar()
        {
            _label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(8, 0)
            };

            _border = new Border
            {
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(230, 245, 230)),
                Child = _label,
                IsVisible = false
            };

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DisplayDurationMs) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                _border.IsVisible = false;
            };
        }

        public void ShowMessage(string message, bool isError = false)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => ShowMessage(message, isError));
                return;
            }

            _label.Text = message;
            _border.Background = new SolidColorBrush(isError
                ? Color.FromRgb(255, 230, 230)
                : Color.FromRgb(230, 245, 230));
            _label.Foreground = new SolidColorBrush(isError
                ? Color.FromRgb(120, 40, 40)
                : Color.FromRgb(40, 80, 40));

            _border.IsVisible = true;
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void QueueNotification(FileModificationInfo modification)
        {
            if (modification == null) return;
            ShowMessage(modification.DisplayMessage);
        }

        public void ClearAll()
        {
            _hideTimer.Stop();
            _border.IsVisible = false;
        }

        public void ResetIgnoreAllSession() { }
        public int PendingCount => 0;

        public void Dispose()
        {
            _hideTimer?.Stop();
            _hideTimer = null;
        }
    }
}
