using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDCore.Components;
using TypeDitor.ViewModel.Dialogs.Project;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for CreateEntityTypeDialog.xaml
    /// </summary>
    public partial class CreateEntityTypeDialog : Window
    {
        // ViewModel
        internal CreateEntityTypeViewModel ViewModel { get; set; }

        // Constructors
        /// <summary>Creates a dialog for defining an entity component in the specified project and namespace.</summary>
        public CreateEntityTypeDialog(TypeD.Models.Data.Project project, string @namespace)
        {
            InitializeComponent();
            ViewModel = new CreateEntityTypeViewModel(this, project, @namespace, CoreComponent.EntityComponent());
            this.DataContext = ViewModel;
        }

        // Event Handlers
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;
                
            Close(true);
        }

        private void btnOpenNamespace_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenNamespace();
        }

        private async void btnOpenInherit_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenComponents();
        }

        private async void btnOpenIcon_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenIcons();
        }
    }
}
