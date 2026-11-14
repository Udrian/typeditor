using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.ComponentModel;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Models;
using TypeDitor.View.Dialogs.Project;
using TypeDitor.View.Dialogs.Tools;
using TypeDitor.ViewModel;

namespace TypeDitor.View
{
    public partial class ProjectWindow : Window
    {
        private ProjectWindowViewModel ViewModel { get; }

        public ProjectWindow()
        {
            InitializeComponent();
            ViewModel = new ProjectWindowViewModel(this);
            DataContext = ViewModel;
            ProjectList.AddHandler(PointerPressedEvent, ProjectList_PointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private async void Window_Opened(object sender, System.EventArgs e)
        {
            await ViewModel.RefreshProjectsAsync();
        }

        private void ProjectList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (ProjectList.SelectedItem is not DiscoveredProject project) return;
            ViewModel.OpenProjectCommand.Execute(project.ProjectFilePath);
        }

        private void ProjectList_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(ProjectList).Properties.IsRightButtonPressed
                || e.Source is not Visual source)
                return;
            if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is DiscoveredProject project)
                ProjectList.SelectedItem = project;
        }

        private void ProjectContextMenu_Opening(object sender, CancelEventArgs e)
        {
            e.Cancel = ProjectList.SelectedItem is not DiscoveredProject;
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is DiscoveredProject project)
                ViewModel.OpenProjectCommand.Execute(project.ProjectFilePath);
        }

        private async void RenameProject_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is not DiscoveredProject project) return;
            try
            {
                var plan = ViewModel.CreateRenamePlan(project);
                var dialog = new ProjectRenameDialog(plan);
                if (await dialog.ShowDialog<bool>(this))
                    await ViewModel.RenameProjectAsync(plan, dialog.NewName);
            }
            catch (Exception exception)
            {
                await ViewModel.ShowErrorAsync("Could not prepare project rename", exception.Message);
            }
        }

        private async void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is DiscoveredProject project)
                await ViewModel.DeleteProjectAsync(project);
        }

        private async void UpdateModules_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is DiscoveredProject project)
                await ViewModel.UpdateModulesAsync(project);
        }

        private async void RefreshProjects_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshProjectsAsync();
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            var options = new OptionsDialog(null);
            options.Closed += async (_, _) => await ViewModel.RefreshProjectsAsync();
            options.Show(this);
        }

        private void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (!ReferenceEquals(ViewModelBase.MainWindow, this))
                return;
            ViewModelBase.ResourceModel.Get<IHookModel>().Shoot(new ExitHook());
        }
    }
}
