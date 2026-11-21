using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TypeD.Models.Data;
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
            NewModuleDialog dialog = new NewModuleDialog();
            if(await dialog.ShowDialog<bool>(this) == true)
            {
                var name = dialog.ViewModel.ModuleName?.Trim();
                var path = dialog.ViewModel.ModuleLocation?.Trim();
                var author = dialog.ViewModel.ModuleAuthor?.Trim();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(author))
                {
                    var selectedModules = dialog.ViewModel.GetSelectedModules();
                    var modulesToAdd = new List<DependencyReference>(
                        selectedModules.Select(m => new DependencyReference(m.Author, m.Name, m.Version, m.LocalPath))
                    );
                    await ModulesDialogViewModel.CreateModule(author, name, path, modulesToAdd);
                }
            }
        }
        
        private async void Branches_Click(object sender, RoutedEventArgs e)
        {
            ModuleBranchesDialog moduleBranchesDialog = new ModuleBranchesDialog();
            if(await moduleBranchesDialog.ShowDialog<bool>(this) == true)
            {
                await ModulesDialogViewModel.ListModules();
            }
        }
        
        private async void Repositories_Click(object sender, RoutedEventArgs e)
        {
            ModuleRepositoriesDialog moduleRepositoriesDialog = new ModuleRepositoriesDialog();
            if (await moduleRepositoriesDialog.ShowDialog<bool>(this) == true)
            {
                await ModulesDialogViewModel.ListModules();
            }
        }

        protected async override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
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
