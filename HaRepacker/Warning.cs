using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace HaRepacker
{
    public static class Warning
    {
        public static bool Warn(string text)
        {
            if (Program.ConfigurationManager?.UserSettings?.SuppressWarnings == true)
                return true;
            return ShowYesNoDialog("Warning", text);
        }

        public static void Error(string text)
        {
            ShowOkDialog("Error", text);
        }

        private static bool ShowYesNoDialog(string title, string text)
        {
            try
            {
                bool result = false;
                if (Dispatcher.UIThread.CheckAccess())
                {
                    result = ShowYesNoAsync(title, text).GetAwaiter().GetResult();
                }
                else
                {
                    Dispatcher.UIThread.InvokeAsync(() => ShowYesNoAsync(title, text).ContinueWith(t => result = t.Result)).Wait();
                }
                return result;
            }
            catch
            {
                Console.Error.WriteLine($"[Warning] {text}");
                return false;
            }
        }

        private static void ShowOkDialog(string title, string text)
        {
            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                    ShowOkAsync(title, text).GetAwaiter().GetResult();
                else
                    Dispatcher.UIThread.InvokeAsync(() => ShowOkAsync(title, text)).Wait();
            }
            catch
            {
                Console.Error.WriteLine($"[{title}] {text}");
            }
        }

        private static async Task<bool> ShowYesNoAsync(string title, string text)
        {
            var dlg = new Window
            {
                Title = title,
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
            };
            bool answer = false;
            var panel = new StackPanel { Margin = new Avalonia.Thickness(15), Spacing = 15 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
            var yes = new Button { Content = "Yes", Width = 80 };
            var no = new Button { Content = "No", Width = 80 };
            yes.Click += (_, _) => { answer = true; dlg.Close(); };
            no.Click += (_, _) => { answer = false; dlg.Close(); };
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
            panel.Children.Add(buttons);
            dlg.Content = panel;
            await dlg.ShowDialog(GetMainWindow());
            return answer;
        }

        private static async Task ShowOkAsync(string title, string text)
        {
            var dlg = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
            };
            var panel = new StackPanel { Margin = new Avalonia.Thickness(15), Spacing = 15 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var ok = new Button { Content = "OK", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            ok.Click += (_, _) => dlg.Close();
            panel.Children.Add(ok);
            dlg.Content = panel;
            await dlg.ShowDialog(GetMainWindow());
        }

        public static async Task<(bool ok, bool answer)> AskYesNo(Window? owner, string text, string title = "Question")
        {
            var dlg = new Window
            {
                Title = title,
                Width = 420,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
            };
            bool? answer = null;
            var panel = new StackPanel { Margin = new Avalonia.Thickness(15), Spacing = 15 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
            var yes = new Button { Content = "Yes", Width = 80 };
            var no = new Button { Content = "No", Width = 80 };
            var cancel = new Button { Content = "Cancel", Width = 80 };
            yes.Click += (_, _) => { answer = true; dlg.Close(); };
            no.Click += (_, _) => { answer = false; dlg.Close(); };
            cancel.Click += (_, _) => dlg.Close();
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            dlg.Content = panel;
            await dlg.ShowDialog(owner ?? GetMainWindow());
            if (answer == null) return (false, false);
            return (true, answer.Value);
        }

        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }
}
