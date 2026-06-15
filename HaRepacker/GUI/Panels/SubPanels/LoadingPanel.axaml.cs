using Avalonia.Controls;
using System.ComponentModel;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class LoadingPanel : UserControl, INotifyPropertyChanged
    {
        public LoadingPanel()
        {
            InitializeComponent();
        }

        public void OnStartAnimate() { }

        public void OnPauseAnimate() { }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
