using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class XYPanel : UserControl
    {
        public XYPanel()
        {
            InitializeComponent();
            button_ApplyChanges.IsEnabled = false;
        }

        public int X
        {
            get { int.TryParse(xBox.Text, out int val); return val; }
            set { xBox.Text = value.ToString(); }
        }

        public int Y
        {
            get { int.TryParse(yBox.Text, out int val); return val; }
            set { yBox.Text = value.ToString(); }
        }

        public event EventHandler? ButtonClicked;

        private void button_ApplyChanges_Click(object? sender, RoutedEventArgs e)
        {
            int.TryParse(yBox.Text, out int yVal);
            yBox.Text = yVal.ToString();
            int.TryParse(xBox.Text, out int xVal);
            xBox.Text = xVal.ToString();

            ButtonClicked?.Invoke(sender, e);
            button_ApplyChanges.IsEnabled = false;
        }

        private void yBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (button_ApplyChanges != null)
                button_ApplyChanges.IsEnabled = true;
        }

        private void xBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (button_ApplyChanges != null)
                button_ApplyChanges.IsEnabled = true;
        }
    }
}
