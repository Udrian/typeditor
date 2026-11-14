using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
            Title = $"Modules - {loadedProject.ProjectName}";
        }

        private async Task PickFolder(string title, Func<string, Task> action)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders.Count >= 1)
            {
                await Reselect(action(folders[0].Path.LocalPath));
            }
        }

        // The list is rebuilt after every change, so restore the selection afterwards
        private async Task Reselect(Task task)
        {
            await task;
            ModuleList.SelectedItem = ModulesDialogViewModel.SelectedModule;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            await Reselect(ModulesDialogViewModel.InstallSelectedModule());
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await Reselect(ModulesDialogViewModel.UpdateSelectedModule());
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            await Reselect(ModulesDialogViewModel.UninstallSelectedModule());
        }

        private async void UseLocal_Click(object sender, RoutedEventArgs e)
        {
            if (ModulesDialogViewModel.SelectedModule == null) return;
            await PickFolder("Select the local module folder", ModulesDialogViewModel.UseLocal);
        }

        private async void UseReleased_Click(object sender, RoutedEventArgs e)
        {
            await Reselect(ModulesDialogViewModel.UseReleased());
        }

        private async void UpdateLocalDependencies_Click(object sender, RoutedEventArgs e)
        {
            await Reselect(ModulesDialogViewModel.UpdateLocalDependencies());
        }

        private async void NewModule_Click(object sender, RoutedEventArgs e)
        {
            var name = NewModuleName.Text?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            await PickFolder("Select the folder to create the module in", path => ModulesDialogViewModel.CreateModule(name, path));
            NewModuleName.Text = "";
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            await ModulesDialogViewModel.ListModules();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (ModulesDialogViewModel.Busy)
                e.Cancel = true;
        }

        private void ModuleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ModulesDialogViewModel.SelectedChanged(ModuleList.SelectedItem as ModulesDialogViewModel.Module);
        }
    }
}
