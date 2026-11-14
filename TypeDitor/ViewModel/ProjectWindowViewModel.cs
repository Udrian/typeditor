using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Commands;
using TypeDitor.Models;
using TypeDitor.View.Dialogs.Tools;

namespace TypeDitor.ViewModel
{
    internal sealed class ProjectWindowViewModel : ViewModelBase
    {
        private readonly IRecentProvider RecentProvider;
        private readonly ISettingModel SettingModel;
        private readonly Window Owner;
        private DiscoveredProject _selectedProject;
        private string _scanStatus;
        private string _scanWarnings;
        private bool _isScanning;

        public ObservableCollection<DiscoveredProject> Projects { get; } = new();
        public OpenProjectCommand OpenProjectCommand { get; } = new();
        public ImportProjectCommand ImportProjectCommand { get; } = new();
        public NewProjectCommand NewProjectCommand { get; } = new();

        public DiscoveredProject SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (_selectedProject == value) return;
                _selectedProject = value;
                OnPropertyChanged(nameof(SelectedProject));
                OnPropertyChanged(nameof(HasSelectedProject));
            }
        }

        public bool HasSelectedProject => SelectedProject != null;

        public string ScanStatus
        {
            get => _scanStatus;
            private set
            {
                _scanStatus = value;
                OnPropertyChanged(nameof(ScanStatus));
            }
        }

        public string ScanWarnings
        {
            get => _scanWarnings;
            private set
            {
                _scanWarnings = value;
                OnPropertyChanged(nameof(ScanWarnings));
                OnPropertyChanged(nameof(HasScanWarnings));
            }
        }

        public bool HasScanWarnings => !string.IsNullOrEmpty(ScanWarnings);

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                _isScanning = value;
                OnPropertyChanged(nameof(IsScanning));
            }
        }

        public ProjectWindowViewModel(Window window)
            : base(window)
        {
            Owner = window;
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            SettingModel = ResourceModel.Get<ISettingModel>();
        }

        public ProjectRenamePlan CreateRenamePlan(DiscoveredProject project)
        {
            return ProjectRenameService.CreatePlan(project, RecentProvider);
        }

        public async Task RenameProjectAsync(ProjectRenamePlan plan, string newName)
        {
            try
            {
                if (IsOpenProject(plan.Location, plan.ProjectName))
                    throw new InvalidOperationException("Close this project before renaming it.");
                ProjectRenameService.Rename(plan, newName, RecentProvider);
                await RefreshProjectsAsync();
            }
            catch (Exception exception)
            {
                await ShowErrorAsync("Could not rename project", exception.Message);
            }
        }

        public async Task DeleteProjectAsync(DiscoveredProject project)
        {
            try
            {
                if (IsOpenProject(project.Location, project.Name))
                    throw new InvalidOperationException("Close this project before deleting it.");
                var location = Path.GetFullPath(project.Location);
                EnsureDeleteAllowed(location);
                var projectFilePath = Path.GetFullPath(project.ProjectFilePath);
                if (!File.Exists(projectFilePath))
                    throw new FileNotFoundException("The project file no longer exists.", projectFilePath);

                var userSettingsPath = ProjectRenameService.UserSettingsPath(project.Name);
                var prompt = $"Delete '{project.Name}' and all files in this project folder?\n\n{location}\n\nThe recent-project entry will also be removed.";
                if (Directory.Exists(userSettingsPath))
                    prompt += $"\n\nAlso delete this user's project settings and theme data:\n{userSettingsPath}";
                var result = await MessageBoxManager.GetMessageBoxStandard(
                    "Delete project", prompt, ButtonEnum.YesNo, Icon.Warning).ShowWindowDialogAsync(Owner);
                if (result != ButtonResult.Yes)
                    return;

                Directory.Delete(location, true);
                RecentProvider.Remove(projectFilePath);
                if (Directory.Exists(userSettingsPath))
                    Directory.Delete(userSettingsPath, true);
                await RefreshProjectsAsync();
            }
            catch (Exception exception)
            {
                await ShowErrorAsync("Could not delete project", exception.Message);
            }
        }

        public async Task UpdateModulesAsync(DiscoveredProject project)
        {
            try
            {
                if (ViewModelBase.MainWindow?.DataContext is MainWindowViewModel mainWindow
                    && PathsEqual(project.ProjectFilePath, mainWindow.LoadedProject.ProjectFilePath))
                {
                    mainWindow.OpenModulesWindow();
                    return;
                }

                var configuration = ResourceModel.Get<IProjectProvider>().ReadConfiguration(project.ProjectFilePath);
                await new ModulesDialog(configuration).ShowDialog(Owner);
            }
            catch (Exception exception)
            {
                await ShowErrorAsync("Could not manage project modules", exception.Message);
            }
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            await MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.Error)
                .ShowWindowDialogAsync(Owner);
        }

        private static void EnsureDeleteAllowed(string location)
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
            var root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty);
            if (string.Equals(fullPath, root, PathComparison))
                throw new InvalidOperationException("The project folder cannot be a filesystem root.");

            var protectedPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppContext.BaseDirectory,
                Environment.CurrentDirectory
            }.Where(path => !string.IsNullOrWhiteSpace(path));
            if (protectedPaths.Any(path => IsSameOrAncestor(fullPath, path)))
                throw new InvalidOperationException("The project folder contains a protected application or user directory.");

            if (Directory.Exists(Path.Combine(fullPath, ".git")) || File.Exists(Path.Combine(fullPath, ".git")))
                throw new InvalidOperationException("Projects that are Git repository roots cannot be deleted from this window.");
        }

        private static readonly StringComparison PathComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                PathComparison);
        }

        private static bool IsSameOrAncestor(string candidate, string protectedPath)
        {
            var fullProtectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedPath));
            if (PathsEqual(candidate, fullProtectedPath))
                return true;
            var prefix = Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;
            return fullProtectedPath.StartsWith(prefix, PathComparison);
        }

        private static bool IsOpenProject(string location, string projectName)
        {
            return ViewModelBase.MainWindow?.DataContext is MainWindowViewModel mainWindow
                && string.Equals(mainWindow.LoadedProject.ProjectName, projectName, StringComparison.Ordinal)
                && PathsEqual(mainWindow.LoadedProject.Location, location);
        }

        public async Task RefreshProjectsAsync()
        {
            if (IsScanning) return;

            IsScanning = true;
            Projects.Clear();
            SelectedProject = null;
            ScanWarnings = string.Empty;
            try
            {
                var scanSettings = SettingModel.GetContext<ProjectScanSettingContext>(SettingLevel.User);
                if (scanSettings == null || scanSettings.Folders.Value == null)
                    throw new InvalidDataException("Project scan settings are unavailable.");
                var folders = scanSettings.Folders.Value;
                if (folders.Count == 0)
                {
                    ScanStatus = "No project scan folders are configured. Open Options to add folders.";
                    return;
                }

                ScanStatus = "Scanning project folders...";
                var currentFormat = ResourceModel.Get<IFormatVersionModel>()
                    .GetCurrentVersions(FormatDocumentKind.Project).GetValueOrDefault("TypeD");
                var result = await Task.Run(() => ProjectDiscovery.Scan(folders, currentFormat));
                var recents = RecentProvider.Get().ToList();
                var orderedProjects = result.Projects
                    .Select(project => new
                    {
                        Project = project,
                        Recent = recents.FirstOrDefault(recent =>
                            ProjectScanSettings.PathsEqual(recent.Path, project.ProjectFilePath))
                    })
                    .OrderByDescending(item => item.Recent?.DateTime ?? DateTime.MinValue)
                    .ThenBy(item => item.Project.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var item in orderedProjects)
                    Projects.Add(item.Project);

                ScanStatus = $"Found {Projects.Count} project{(Projects.Count == 1 ? string.Empty : "s")}.";
                var errors = result.Errors.ToList();
                ScanWarnings = string.Join(Environment.NewLine, errors.Take(5));
                if (errors.Count > 5)
                    ScanWarnings += $"{Environment.NewLine}And {errors.Count - 5} more scan issue(s).";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Text.Json.JsonException)
            {
                ScanStatus = $"Could not scan project folders: {exception.Message}";
                ScanWarnings = ScanStatus;
            }
            finally
            {
                IsScanning = false;
            }
        }

    }
}
