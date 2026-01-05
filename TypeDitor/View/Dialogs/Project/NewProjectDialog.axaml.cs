using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDitor.ViewModel.Dialogs.Project;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for NewProjectDialog.xaml
    /// </summary>
    public partial class NewProjectDialog : Window
    {
        // ViewModel
        internal NewProjectViewModel ViewModel { get { return DataContext as NewProjectViewModel; } }

        // Constructors
        public NewProjectDialog()
        {
            InitializeComponent();
            DataContext = new NewProjectViewModel();
        }

        private void btnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenLocation();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;

            Close(true);
        }
    }
}
