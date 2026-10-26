using Avalonia.Controls;
using Avalonia.Threading;
using System;
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
        private List<TypeD.View.Panel> DelayedPanels { get; set; } = new List<TypeD.View.Panel>();
        private List<BuildIssue> BuildWarnings { get; set; } = new();
        private List<BuildIssue> BuildErrors { get; set; } = new();
        private List<BuildIssueDisplay> WarningIssues { get; set; } = new();
        private List<BuildIssueDisplay> ErrorIssues { get; set; } = new();
        private bool _isBuilding;
        private bool _isBuildFailed;
        private string _buildStatus = "Build status unavailable";

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
            
            BuildProjectCommand = new BuildProjectCommand();
            ExitProjectCommand = new ExitProjectCommand();
            NewProjectCommand = new NewProjectCommand();
            ImportProjectCommand = new ImportProjectCommand();
            OpenProjectCommand = new OpenProjectCommand();
            RunProjectCommand = new RunProjectCommand();
            SaveProjectCommand = new SaveProjectCommand();
            OpenPanelCommand = new OpenPanelCommand();
            ClosePanelCommand = new ClosePanelCommand();
            HookModel.AddHook<BuildStatusChangedHook>(OnBuildStatusChanged);
        }

        // Functions
        public void InitUI(MainWindow mainWindow)
        {
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
            
            foreach(var p in DelayedPanels)
            {
                OnAddElement(p);
            }
            DelayedPanels.Clear();
        }

        private List<TypeD.View.MenuItem> RebuildMainMenuViewItems()
        {
            var items = new List<TypeD.View.MenuItem>();
            foreach (var panel in PanelModel.GetPanels())
            {
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

            TypeDInit.ProjectUnload(LoadedProject, ResourceModel);
            HookModel.Shoot(new ExitHook() { Project = LoadedProject });
            HookModel.RemoveHook<BuildStatusChangedHook>(OnBuildStatusChanged);
            return false;
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

        public override void OnAddElement(object element)
        {
            if(element is TypeD.View.Panel)
            {
                var panel = element as TypeD.View.Panel;
                if (MainWindow == null)
                {
                    DelayedPanels.Add(panel);
                    return;
                }
            
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                var panelSetting = setting.Panels.Value.Find(p => p.ID == panel.ID);
                if (panelSetting == null)
                    return;

                MainWindow.DockRoot.AddPanel(panel, panelSetting);
            }
        }

        public override void OnRemoveElement(object element)
        {
            //TODO: Find a way to remove panels through Avalonia Dock
            //if (element is TypeD.View.Panel)
            //{
            //    //TODO: This is not the most efficent way to do this...
            //    MainWindow.DockPanelRoot.Children.Remove(MainWindow.DockRoot);
            //    MainWindow.DockRoot = new View.TypeDock.TypeDockRoot();
            //    MainWindow.DockRoot.Margin = new Thickness(5);
            //    MainWindow.DockPanelRoot.Children.Add(MainWindow.DockRoot);
            //
            //    foreach (var panel in PanelModel.GetPanels())
            //    {
            //        var parent = VisualTreeHelper.GetParent(panel.PanelView);
            //        if(parent!= null)
            //            (parent as Avalonia.Controls.Panel).Children.Remove(panel.PanelView);
            //    }
            //    foreach (var panel in PanelModel.GetPanels())
            //    {
            //        if (panel.Open)
            //            OnAddElement(panel);
            //    }
            //}
        }
    }
}
