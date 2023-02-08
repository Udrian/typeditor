using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.IO;
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

        private void AddLocal_Click(object sender, RoutedEventArgs e)
        {
            //var folderBrowserDialog = new VistaFolderBrowserDialog();
            //if (folderBrowserDialog.ShowDialog() == true)
            //{
            //    bool found = false;
            //    var name = "";
            //    foreach (var file in Directory.GetFiles(folderBrowserDialog.SelectedPath))
            //    {
            //        if (file.EndsWith(".sln"))
            //        {
            //            found = true;
            //            name = Path.GetFileNameWithoutExtension(file);
            //            break;
            //        }
            //    }
            //
            //    if (!found)
            //        //TODO: Display Error Popup
            //        return;
            //    ModulesDialogViewModel.AddLocal(name, folderBrowserDialog.SelectedPath);
            //}


            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            dialog.Filter = "TypeD DLL (*.dll)|*.dll";
            if (dialog.ShowDialog() == true)
            {
                ModulesDialogViewModel.AddLocal(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName);
            }
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
