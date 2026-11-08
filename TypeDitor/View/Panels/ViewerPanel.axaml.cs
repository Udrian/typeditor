using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeD.Models.Data;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels
{
    /// <summary>
    /// Interaction logic for ViewerPanel.xaml
    /// </summary>
    public partial class ViewerPanel : UserControl
    {
        ViewerViewModel ViewerViewModel { get; set; }

        /// <summary>Creates a viewer panel for the specified project.</summary>
        public ViewerPanel(Project project)
        {
            InitializeComponent();

            DataContext = ViewerViewModel = new ViewerViewModel(project, this);
        }

        private void ViewerPanelUnloaded(object sender, RoutedEventArgs e)
        {
            ViewerViewModel.Unload();
        }

        private void ViewerPanelLoaded(object sender, RoutedEventArgs e)
        {
            ViewerViewModel.Load();
        }
    }
}
