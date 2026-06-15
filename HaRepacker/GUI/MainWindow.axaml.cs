using Avalonia.Controls;
using Avalonia.Interactivity;

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

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
