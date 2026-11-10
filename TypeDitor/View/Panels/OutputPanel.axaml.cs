using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Collections.Specialized;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels
{
    /// <summary>
    /// Interaction logic for OutputPanel.xaml
    /// </summary>
    public partial class OutputPanel : UserControl
    {
        // ViewModel
        OutputViewModel OutputViewModel { get; set; }

        // Constructors
        /// <summary>Initializes an output panel.</summary>
        public OutputPanel()
        {
            InitializeComponent();

            OutputViewModel = new OutputViewModel(this);
            DataContext = OutputViewModel;

            OutputViewModel.GameLogLines.CollectionChanged += GameLogLines_CollectionChanged;
        }

        private void GameLogLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Keep the newest game log lines visible, scrolled after the layout pass has run
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.UIThread.Post(() => svGameLog.ScrollToEnd(), DispatcherPriority.Background);
        }

        private void btnClearGameLog_Click(object sender, RoutedEventArgs e)
        {
            OutputViewModel.ClearGameLog();
        }

        private void tbOutputText_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbOutputText.CaretIndex = int.MaxValue;
        }

        private void Panel_Unloaded(object sender, RoutedEventArgs e)
        {
            OutputViewModel.Unload();
        }
    }
}
