using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HaRepacker.GUI.Input;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class MainWindow : Window
    {
        public MainWindow(string? wzToLoad = null, bool firstRun = false)
        {
            InitializeComponent();

            if (wzToLoad != null)
                mainPanel.OpenFile(wzToLoad);
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open WZ File",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("WZ Files") { Patterns = new[] { "*.wz" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count == 0) return;

            var (ok, version) = await InputDialogs.ShowWzMapleVersionAsync(this, "Select Encryption Type");
            if (!ok) return;

            foreach (var file in files)
            {
                string path = file.TryGetLocalPath() ?? file.Path.LocalPath;
                mainPanel.OpenFile(path, version);
            }
        }

        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => mainPanel.UndoMan?.Undo();

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => mainPanel.UndoMan?.Redo();

        private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
    }
}
