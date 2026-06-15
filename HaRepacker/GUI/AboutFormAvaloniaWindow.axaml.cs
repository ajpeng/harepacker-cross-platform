using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class AboutFormWindow : Window
    {
        public AboutFormWindow() => InitializeComponent();

        public static Task ShowAsync(Window owner)
        {
            var dlg = new AboutFormWindow();
            return dlg.ShowDialog(owner);
        }

        private void OnLinkClick(object? sender, PointerPressedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/lastbattle/Harepacker-resurrected") { UseShellExecute = true }); }
            catch { }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    }
}
