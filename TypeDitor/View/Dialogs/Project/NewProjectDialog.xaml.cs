using System.Windows;
using TypeDitor.ViewModel.Dialogs.Project;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for NewProjectDialog.xaml
    /// </summary>
    public partial class NewProjectDialog : Window
    {
        // ViewModel
        internal NewProjectViewModel ViewModel { get; set; }

        // Constructors
        public NewProjectDialog()
        {
            InitializeComponent();
            ViewModel = new NewProjectViewModel(this);
            this.DataContext = ViewModel;
        }

        private void btnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenLocation();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;

            DialogResult = true;
            Close();
        }
    }
}
