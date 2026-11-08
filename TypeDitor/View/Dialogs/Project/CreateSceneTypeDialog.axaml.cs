using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDCore.Components;
using TypeDitor.ViewModel.Dialogs.Project;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for CreateSceneDialog.xaml
    /// </summary>
    public partial class CreateSceneTypeDialog : Window
    {
        // ViewModel
        internal CreateComponentTypeBaseViewModel ViewModel { get; set; }

        // Constructors
        /// <summary>Creates a dialog for defining a scene component in the specified project and namespace.</summary>
        public CreateSceneTypeDialog(TypeD.Models.Data.Project project, string @namespace)
        {
            InitializeComponent();
            ViewModel = new CreateComponentTypeBaseViewModel(this, project, @namespace, CoreComponent.SceneComponent());
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
