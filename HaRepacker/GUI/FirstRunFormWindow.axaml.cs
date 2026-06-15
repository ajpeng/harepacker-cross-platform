using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace HaRepacker.GUI
{
    public partial class FirstRunFormWindow : Window
    {
        private bool _confirmed = false;

        public FirstRunFormWindow()
        {
            InitializeComponent();
            Closing += (_, e) => { if (!_confirmed) e.Cancel = true; };
        }

        public static Task ShowAsync(Window owner)
        {
            var dlg = new FirstRunFormWindow();
            return dlg.ShowDialog(owner);
        }

        private void OnContinueClick(object? sender, RoutedEventArgs e)
        {
            if (Program.ConfigurationManager != null)
                Program.ConfigurationManager.UserSettings.AutoAssociate = autoAssociateBox.IsChecked == true;
            _confirmed = true;
            Close();
        }
    }
}
