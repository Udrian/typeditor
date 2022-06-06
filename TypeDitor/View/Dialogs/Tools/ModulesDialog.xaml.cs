using System;
using System.Windows;
using TypeDitor.ViewModel.Dialogs.Tools;

namespace TypeDitor.View.Dialogs.Tools
{
    /// <summary>
    /// Interaction logic for ModulesDialog.xaml
    /// </summary>
    public partial class ModulesDialog : Window
    {
        // ViewModel
        ModulesDialogViewModel ModulesDialogViewModel { get; set; }

        // Constructors
        public ModulesDialog(TypeD.Models.Data.Project loadedProject)
        {
            DataContext = ModulesDialogViewModel = new ModulesDialogViewModel(this, loadedProject);
            InitializeComponent();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            ModulesDialogViewModel.InstallSelectedModule();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            ModulesDialogViewModel.UninstallSelectedModule();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            await ModulesDialogViewModel.ListModules();
        }

        private void ModuleList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ModulesDialogViewModel.SelectedChanged(ModuleList.SelectedItem as ModulesDialogViewModel.Module);
        }
    }
}
