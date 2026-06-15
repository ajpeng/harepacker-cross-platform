using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MapleLib.WzLib;
using System.Linq;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class ListEditorWindow : Window
    {
        private readonly WzMapleVersion _version;

        public ListEditorWindow(string? path, WzMapleVersion version)
        {
            InitializeComponent();
            _version = version;

            if (path != null)
            {
                var entries = MapleLib.WzLib.ListFileParser.ParseListFile(path, version);
                textBox.Text = string.Join("\r\n", entries);
            }

            Closing += (_, e) =>
            {
                if (!Warning.Warn("Are you sure you want to close this file?"))
                    e.Cancel = true;
            };
        }

        public static void ShowWindow(Window? owner, string? path, WzMapleVersion version)
        {
            var window = new ListEditorWindow(path, version);
            if (owner != null)
                window.ShowDialog(owner);
            else
                window.Show();
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save List WZ File",
                FileTypeChoices = new[] { new FilePickerFileType("List WZ File") { Patterns = new[] { "*.wz" } } }
            });
            if (file == null) return;
            string outPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
            var entries = (textBox.Text ?? "").Replace("\r\n", "\n").Split('\n').ToList();
            MapleLib.WzLib.ListFileParser.SaveToDisk(outPath, _version, entries);
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    }
}
