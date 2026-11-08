using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels
{
    /// <summary>Displays the editor command history and provides undo and redo controls.</summary>
    public partial class CommandHistoryPanel : UserControl
    {
        // ViewModel
        CommandHistoryViewModel ViewModel { get; set; }

        /// <summary>Initializes a command history panel.</summary>
        public CommandHistoryPanel()
        {
            InitializeComponent();
            DataContext = ViewModel = new CommandHistoryViewModel(this);
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Redo();
        }
    }
}
