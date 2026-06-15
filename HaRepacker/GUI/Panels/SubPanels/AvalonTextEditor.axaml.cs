using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class AvalonTextEditor : UserControl, INotifyPropertyChanged
    {
        private IHighlightingDefinition? _highlightingDefinition;

        public AvalonTextEditor()
        {
            InitializeComponent();

            var defs = HighlightingManager.Instance.HighlightingDefinitions;
            comboBox_SyntaxHighlighting.ItemsSource = defs;
            comboBox_SyntaxHighlighting.DisplayMemberBinding = new Avalonia.Data.Binding("Name");

            if (defs.Count > 2)
            {
                comboBox_SyntaxHighlighting.SelectedIndex = 2; // default to JavaScript
                _highlightingDefinition = defs[2];
            }

            DataContext = this;
        }

        public void SetText(string text)
        {
            textEditor.Text = text;
            button_saveApply.IsEnabled = false;
        }

        public string GetText() => textEditor.Text;

        public IHighlightingDefinition? HighlightingDefinition
        {
            get => _highlightingDefinition;
            set
            {
                if (_highlightingDefinition != value)
                {
                    _highlightingDefinition = value;
                    textEditor.SyntaxHighlighting = value;
                    OnPropertyChanged(nameof(HighlightingDefinition));
                }
            }
        }

        public event EventHandler? SaveButtonClicked;

        private void comboBox_SyntaxHighlighting_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is IHighlightingDefinition def)
                HighlightingDefinition = def;
        }

        private void button_saveApply_Click(object? sender, RoutedEventArgs e)
        {
            SaveButtonClicked?.Invoke(sender, e);
            button_saveApply.IsEnabled = false;
        }

        private void textEditor_TextChanged(object? sender, EventArgs e)
        {
            if (button_saveApply != null && !button_saveApply.IsEnabled)
                button_saveApply.IsEnabled = true;
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
