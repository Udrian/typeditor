using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
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

        private async void btnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenLocation(StorageProvider);
        }

        private void AddLocalModule_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddLocalModule();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;

            Close(true);
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            await ViewModel.LoadAvailableModules();
        }
    }
}
