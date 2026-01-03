using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TypeD.ViewModel;
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

        private async void AddLocal_Click(object sender, RoutedEventArgs e)
        {

            var files = await ViewModelBase.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Folder",
                AllowMultiple = false
            
            });
            
            if (files.Count >= 1)
            {
                bool found = false;
                var name = "";
                var folder = files[0].Path.AbsolutePath;
                foreach (var file in Directory.GetFiles(folder))
                {
                    if (file.EndsWith(".sln"))
                    {
                        found = true;
                        name = Path.GetFileNameWithoutExtension(file);
                        break;
                    }
                }
            
                if (!found)
                    //TODO: Display Error Popup
                    return;
                ModulesDialogViewModel.AddLocal(name, folder);
            }
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            await ModulesDialogViewModel.ListModules();
        }

        private void ModuleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ModulesDialogViewModel.SelectedChanged(ModuleList.SelectedItem as ModulesDialogViewModel.Module);
        }
    }
}
