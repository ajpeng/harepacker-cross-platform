using Avalonia.Controls;
using Avalonia.Interactivity;
using MapleLib.WzLib.Serializer;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class OptionsFormWindow : Window
    {
        public OptionsFormWindow()
        {
            InitializeComponent();
            var cfg = Program.ConfigurationManager;
            if (cfg != null)
            {
                sortBox.IsChecked = cfg.UserSettings.Sort;
                loadRelated.IsChecked = cfg.UserSettings.AutoloadRelatedWzFiles;
                suppressWarnings.IsChecked = cfg.UserSettings.SuppressWarnings;
                indentBox.Value = cfg.UserSettings.Indentation;
                lineBreakBox.SelectedIndex = (int)cfg.UserSettings.LineBreakType;
                if (!string.IsNullOrEmpty(cfg.UserSettings.DefaultXmlFolder))
                {
                    defXmlFolderEnable.IsChecked = true;
                    defXmlFolderBox.Text = cfg.UserSettings.DefaultXmlFolder;
                    defXmlFolderBox.IsEnabled = true;
                    browseBtn.IsEnabled = true;
                }
            }
        }

        public static Task ShowAsync(Window owner)
        {
            var dlg = new OptionsFormWindow();
            return dlg.ShowDialog(owner);
        }

        private void OnDefXmlFolderToggle(object? sender, RoutedEventArgs e)
        {
            bool enabled = defXmlFolderEnable.IsChecked == true;
            defXmlFolderBox.IsEnabled = enabled;
            browseBtn.IsEnabled = enabled;
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            string path = await SavedFolderBrowser.ShowAsync(this, "Select default XML export folder");
            if (!string.IsNullOrEmpty(path)) defXmlFolderBox.Text = path;
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (indentBox.Value < 0) { Warning.Error("Indentation must be >= 0."); return; }
            var cfg = Program.ConfigurationManager;
            if (cfg != null)
            {
                cfg.UserSettings.Sort = sortBox.IsChecked == true;
                cfg.UserSettings.AutoloadRelatedWzFiles = loadRelated.IsChecked == true;
                cfg.UserSettings.SuppressWarnings = suppressWarnings.IsChecked == true;
                cfg.UserSettings.DefaultXmlFolder = defXmlFolderEnable.IsChecked == true ? defXmlFolderBox.Text ?? "" : "";
                cfg.UserSettings.Indentation = (int)(indentBox.Value ?? 4);
                cfg.UserSettings.LineBreakType = (LineBreak)lineBreakBox.SelectedIndex;
                cfg.Save();
            }
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
    }
}
