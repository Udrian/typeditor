using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Model.Avalonia.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using System;
using System.Diagnostics;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TypeD;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Commands;
using TypeDitor.View;
using TypeDitor.View.Dialogs.Tools;
using TypeDitor.View.TypeDock;

namespace TypeDitor.ViewModel
{
    internal sealed class BuildIssueDisplay
    {
        public string Severity { get; }
        public string Code { get; }
        public string Summary { get; }
        public string LocationSummary { get; }
        public string File { get; }
        public string Line { get; }
        public string Column { get; }
        public string LineDisplay { get; }
        public string ColumnDisplay { get; }
        public string Message { get; }
        public bool CanOpen => !string.IsNullOrWhiteSpace(File);

        public BuildIssueDisplay(BuildIssue issue)
        {
            Severity = issue.Severity?.ToUpperInvariant() ?? "ISSUE";
            Code = issue.Code;
            Message = issue.Message;
            Summary = string.IsNullOrWhiteSpace(Message)
                ? "No message provided"
                : Message.Length > 140 ? Message.Substring(0, 137) + "..." : Message;
            File = issue.File;
            Line = issue.Line?.ToString() ?? "Not provided";
            Column = issue.Column?.ToString() ?? "Not provided";
            LineDisplay = $"Line: {Line}";
            ColumnDisplay = $"Column: {Column}";
            LocationSummary = string.IsNullOrWhiteSpace(File)
                ? "Location unavailable"
                : $"{Path.GetFileName(File)}{(issue.Line.HasValue ? $":{issue.Line}" : string.Empty)}";
        }
    }

    internal class MainWindowViewModel : ViewModelBase
    {
        // Models
        IHookModel HookModel { get; set; }
        ISaveModel SaveModel { get; set; }
        ISettingModel SettingModel { get; set; }
        IPanelModel PanelModel { get; set; }
        IEditorCommandRegistry EditorCommandRegistry { get; set; }

        // Commands
        public BuildProjectCommand BuildProjectCommand { get; set; }
        public ExitProjectCommand ExitProjectCommand { get; set; }
        public NewProjectCommand NewProjectCommand { get; set; }
        public ImportProjectCommand ImportProjectCommand { get; set; }
        public OpenProjectCommand OpenProjectCommand { get; set; }
        public RunProjectCommand RunProjectCommand { get; set; }
        public SaveProjectCommand SaveProjectCommand { get; set; }
        public OpenPanelCommand OpenPanelCommand { get; set; }
        public ClosePanelCommand ClosePanelCommand { get; set; }
        // Data
        public Project LoadedProject { get; private set; }
        public TypeDockFactory DockFactory { get; }
        private IRootDock _layout;
        public IRootDock Layout
        {
            get => _layout;
            private set
            {
                _layout = value;
                OnPropertyChanged();
            }
        }
        //private List<TypeD.View.Panel> DelayedPanels { get; set; } = new List<TypeD.View.Panel>();
        private List<BuildIssue> BuildWarnings { get; set; } = new();
        private List<BuildIssue> BuildErrors { get; set; } = new();
        private List<BuildIssueDisplay> WarningIssues { get; set; } = new();
        private List<BuildIssueDisplay> ErrorIssues { get; set; } = new();
        private bool _isBuilding;
        private bool _isBuildFailed;
        private string _buildStatus = "Build status unavailable";
        private List<IDisposable> CommandRegistrations { get; set; } = new();

        public bool IsBuilding
        {
            get => _isBuilding;
            private set
            {
                _isBuilding = value;
                OnPropertyChanged();
            }
        }

        public string BuildStatus
        {
            get => _buildStatus;
            private set
            {
                _buildStatus = value;
                OnPropertyChanged();
            }
        }

        public bool IsBuildFailed
        {
            get => _isBuildFailed;
            private set
            {
                _isBuildFailed = value;
                OnPropertyChanged();
            }
        }

        public int WarningCount => BuildWarnings.Count;
        public bool HasWarnings => WarningCount > 0;
        public int ErrorCount => BuildErrors.Count;
        public IReadOnlyList<BuildIssueDisplay> WarningIssueItems => WarningIssues;
        public IReadOnlyList<BuildIssueDisplay> ErrorIssueItems => ErrorIssues;

        public async Task OpenBuildIssueInExternalEditor(BuildIssueDisplay issue)
        {
            if (issue == null || !issue.CanOpen)
                return;

            var filePath = Path.IsPathRooted(issue.File)
                ? issue.File
                : Path.GetFullPath(issue.File, LoadedProject.Location);
            var editorCommand = SettingModel.GetContext<MainWindowSettingContext>().ExternalEditor.Value;
            var quotedPath = QuotePowerShellArgument(filePath);
            var location = issue.Line == "Not provided"
                ? filePath
                : $"{filePath}:{issue.Line}{(issue.Column == "Not provided" ? string.Empty : $":{issue.Column}")}";
            var quotedLocation = QuotePowerShellArgument(location);

            var commandSupportsLocation = editorCommand.Contains("{location}", StringComparison.Ordinal);
            var commandExecutable = editorCommand.TrimStart().Split(' ', '\t')[0];
            var isVisualStudioCode = Path.GetFileNameWithoutExtension(commandExecutable)
                .StartsWith("code", StringComparison.OrdinalIgnoreCase);

            if (commandSupportsLocation)
            {
                editorCommand = editorCommand.Replace("{location}", quotedLocation, StringComparison.Ordinal);
            }
            else if (isVisualStudioCode && issue.Line != "Not provided")
            {
                editorCommand = editorCommand.Replace("{path}", $"--goto {quotedLocation}", StringComparison.Ordinal);
            }
            else
            {
                editorCommand = editorCommand.Replace("{path}", quotedPath, StringComparison.Ordinal);
            }

            editorCommand = editorCommand
                .Replace("{line}", issue.Line, StringComparison.Ordinal)
                .Replace("{column}", issue.Column, StringComparison.Ordinal);

            await CMD.Run(editorCommand);
        }

        private static string QuotePowerShellArgument(string value)
        {
            return $"'{value.Replace("'", "''")}'";
        }

        // Properties
        public int SizeX
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.SizeX?.Value ?? 1024;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.SizeX.Value = value;
                SettingModel.SetContext(setting);
            }
        }

        public int SizeY
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.SizeY?.Value ?? 768;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.SizeY.Value = value;
                SettingModel.SetContext(setting);
            }
        }

        public WindowState Maximized
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.Fullscreen.Value == true ? WindowState.Maximized : WindowState.Normal;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.Fullscreen.Value = value == WindowState.Maximized;
                SettingModel.SetContext(setting);
            }
        }

        // View
        public MainWindow MainWindow { get; set; }

        // Constructors
        public MainWindowViewModel(MainWindow mainWindow, Project loadedProject) : base(mainWindow)
        {
            LoadedProject = loadedProject;
            MainWindow = mainWindow;

            HookModel = ResourceModel.Get<IHookModel>();
            SaveModel = ResourceModel.Get<ISaveModel>();
            SettingModel = ResourceModel.Get<ISettingModel>();
            PanelModel = ResourceModel.Get<IPanelModel>();
            EditorCommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            DockFactory = new TypeDockFactory(ResourceModel);
            DockFactory.DockableClosed += (_, args) =>
            {
                var panel = PanelModel.GetPanels().Find(p => p.ID == args.Dockable?.Id);
                if (panel?.Open == true)
                    PanelModel.ClosePanel(panel.ID);
                SaveDockLayout();
            };
            
            BuildProjectCommand = new BuildProjectCommand();
            ExitProjectCommand = new ExitProjectCommand();
            NewProjectCommand = new NewProjectCommand();
            ImportProjectCommand = new ImportProjectCommand();
            OpenProjectCommand = new OpenProjectCommand();
            RunProjectCommand = new RunProjectCommand();
            SaveProjectCommand = new SaveProjectCommand();
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.save",
                "Save project",
                "Save pending project changes.",
                "global",
                "Ctrl+S",
                _ => SaveProjectCommand.Execute(LoadedProject),
                _ => SaveProjectCommand.CanExecute(LoadedProject)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.build",
                "Build project",
                "Save pending changes and build the loaded project.",
                "global",
                string.Empty,
                _ => BuildProjectCommand.Execute(LoadedProject)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.run",
                "Run project",
                "Run the most recently built project.",
                "global",
                string.Empty,
                _ => RunProjectCommand.Execute(LoadedProject)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.new",
                "Create project",
                "Create a new project.",
                "global",
                string.Empty,
                _ => NewProjectCommand.Execute(null)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.import",
                "Import project",
                "Import an existing solution as a project.",
                "global",
                string.Empty,
                _ => ImportProjectCommand.Execute(null)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.open",
                "Open project",
                "Open an existing project.",
                "global",
                string.Empty,
                _ => OpenProjectCommand.Execute(null)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "project.exit",
                "Exit project",
                "Close the current project.",
                "global",
                string.Empty,
                _ => ExitProjectCommand.Execute(null)));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "tools.modules",
                "Open modules",
                "Browse and manage project modules.",
                "global",
                string.Empty,
                _ => OpenModulesWindow()));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "tools.options",
                "Open options",
                "Open editor and project options.",
                "global",
                string.Empty,
                _ => OpenOptionsWindow()));
            CommandRegistrations.Add(EditorCommandRegistry.Register(
                "command-palette.open",
                "Open command palette",
                "Search and execute an available command.",
                "global",
                "Ctrl+P",
                _ => OpenCommandPalette()));
            OpenPanelCommand = new OpenPanelCommand();
            ClosePanelCommand = new ClosePanelCommand();
            HookModel.AddHook<BuildStatusChangedHook>(OnBuildStatusChanged);
        }

        // Functions
        public void InitUI(MainWindow mainWindow)
        {
            var dockLayoutSetting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
            var savedDockLayout = dockLayoutSetting.DockLayout.Value;
            IRootDock layout;
            if (string.IsNullOrWhiteSpace(savedDockLayout))
            {
                layout = DockFactory.CreateLayout();
            }
            else
            {
                try
                {
                    layout = new AvaloniaDockSerializer().Deserialize<IRootDock>(savedDockLayout);
                    if (layout == null)
                        throw new System.Text.Json.JsonException("The saved Dock layout was empty.");
                }
                catch (System.Text.Json.JsonException exception)
                {
                    Trace.TraceError($"Could not restore the saved Dock layout: {exception}");
                    layout = DockFactory.CreateLayout();
                }
            }

            DockFactory.InitializeLayout(layout);
            Layout = layout;
            DockFactory.DockableAdded += (_, _) => SaveDockLayout();
            DockFactory.DockableRemoved += (_, _) => SaveDockLayout();
            DockFactory.DockableMoved += (_, _) => SaveDockLayout();
            DockFactory.DockableDocked += (_, _) => SaveDockLayout();
            DockFactory.DockableUndocked += (_, _) => SaveDockLayout();
            DockFactory.DockableSwapped += (_, _) => SaveDockLayout();
            DockFactory.DockablePinned += (_, _) => SaveDockLayout();
            DockFactory.DockableUnpinned += (_, _) => SaveDockLayout();

            var initUIHook = HookModel.Shoot(new InitUIHook(LoadedProject));

            var menu = initUIHook.Menu;
            var viewMenuItem = new TypeD.View.MenuItem()
            {
                Name = "_View",
                Items = new List<TypeD.View.MenuItem>(RebuildMainMenuViewItems())
            };
            menu.Items.Add(viewMenuItem);
            
            foreach (var menuItem in menu.Items)
            {
                ViewHelper.InitMenu(mainWindow.TopMenu, menuItem, this);
            }
        }

        private List<TypeD.View.MenuItem> RebuildMainMenuViewItems()
        {
            var items = new List<TypeD.View.MenuItem>();
            foreach (var panel in PanelModel.GetPanels())
            {
                CommandRegistrations.Add(EditorCommandRegistry.Register(
                    $"panel.toggle.{panel.ID}",
                    $"Toggle {panel.Title} panel",
                    $"Open or close the {panel.Title} panel.",
                    "global",
                    string.Empty,
                    _ =>
                    {
                        if (!panel.Open)
                            OpenPanelCommand.Execute(panel.ID);
                        else
                            ClosePanelCommand.Execute(panel.ID);
                    }));

                items.Add(new TypeD.View.MenuItem()
                {
                    Name = panel.Title,
                    Click = (param) =>
                    {
                        if (!panel.Open)
                            OpenPanelCommand.Execute(panel.ID);
                        else
                            ClosePanelCommand.Execute(panel.ID);
                    }
                });
            }
            return items;
        }

        public void OpenModulesWindow()
        {
            var dialog = new ModulesDialog(LoadedProject);
            dialog.Show();
        }

        public void OpenOptionsWindow()
        {
            var dialog = new OptionsDialog(LoadedProject);
            dialog.Show();
        }

        public void OpenCommandPalette()
        {
            var palette = new CommandPaletteWindow(EditorCommandRegistry);
            palette.Show(MainWindow);
        }

        public void ExecuteCommand(string commandId)
        {
            EditorCommandRegistry.Execute(commandId);
        }

        public async Task<bool> OnClose()
        {
            LoadedProject.IsClosing = true;
            if (SaveModel.AnythingToSave)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Closing...", "Save before closing?", ButtonEnum.YesNoCancel, Icon.Question);
                var result = await box.ShowWindowDialogAsync(MainWindow);

                if (result == ButtonResult.Yes)
                {
                    await SaveModel.Save(LoadedProject);
                }
                else if (result == ButtonResult.Cancel)
                {
                    LoadedProject.IsClosing = false;
                    return true;
                }
            }

            SaveDockLayout();
            HookModel.Shoot(new ExitHook() { Project = LoadedProject });
            TypeDInit.ProjectUnload(LoadedProject, ResourceModel);
            HookModel.RemoveHook<BuildStatusChangedHook>(OnBuildStatusChanged);
            foreach (var registration in CommandRegistrations)
                registration.Dispose();
            CommandRegistrations.Clear();
            return false;
        }

        private void SaveDockLayout()
        {
            if (Layout == null)
                return;

            var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
            setting.DockLayout.Value = new AvaloniaDockSerializer().Serialize(Layout);
            SettingModel.SetContext(setting);
        }

        public bool TryExecuteShortcut(string shortcut, string scope)
        {
            return EditorCommandRegistry.TryExecuteShortcut(shortcut, scope);
        }

        private void OnBuildStatusChanged(BuildStatusChangedHook hook)
        {
            if (hook.Project != LoadedProject)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                IsBuilding = hook.IsBuilding;
                BuildStatus = hook.Status;
                if (hook.IsBuilding)
                    IsBuildFailed = false;
                if (hook.IsComplete)
                {
                    IsBuildFailed = !hook.Succeeded;
                    BuildWarnings = hook.Warnings;
                    BuildErrors = hook.Errors;
                    WarningIssues = BuildWarnings.ConvertAll(issue => new BuildIssueDisplay(issue));
                    ErrorIssues = BuildErrors.ConvertAll(issue => new BuildIssueDisplay(issue));
                    OnPropertyChanged(nameof(WarningCount));
                    OnPropertyChanged(nameof(HasWarnings));
                    OnPropertyChanged(nameof(ErrorCount));
                    OnPropertyChanged(nameof(WarningIssueItems));
                    OnPropertyChanged(nameof(ErrorIssueItems));
                }
            });
        }
    }
}
