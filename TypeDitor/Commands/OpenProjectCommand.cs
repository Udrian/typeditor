using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Models;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Commands
{
    internal class OpenProjectCommand : ProjectCommands
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }
        private IProjectProvider ProjectProvider { get; set; }
        private IProjectModel ProjectModel { get; set; }
        private ILogModel LogModel { get; set; }

        public OpenProjectCommand() : base()
        {
            LogModel = ResourceModel.Get<ILogModel>();
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            ProjectProvider = ResourceModel.Get<IProjectProvider>();
            ProjectModel = ResourceModel.Get<IProjectModel>();
        }

        public async override void Execute(object param)
        {
            var path = "";
            if (param is Recent)
            {
                path = (param as Recent).Path;
            }
            else if (param is string projectFilePath)
            {
                path = projectFilePath;
            }
            else
            {
                var files = await ViewModelBase.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Open TypeO Project file",
                    AllowMultiple = false,
                    FileTypeFilter = [FileHelper.TypeOProjectFileType],
                });

                if (files.Count >= 1)
                {
                    path = files[0].Path.AbsolutePath;
                }
            }

            OpenProjectProgressDialog progressDialog = null;
            var logLines = new ConcurrentQueue<string>();
            var currentStep = "Starting";
            const string LogName = "OpenProjectErrorLog";
            //Open project
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    LogModel.AttachLogOutput(LogName, line => logLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] {line}"));
                    AutosaveManager.RecoverySnapshot recovery = null;
                    TypeD.Models.Data.Project recoveryProject = null;
                    try
                    {
                        recoveryProject = ProjectProvider.ReadConfiguration(path);
                        recovery = AutosaveManager.FindNewerSnapshot(recoveryProject);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                        System.Text.Json.JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
                    {
                        LogModel.Log($"Could not check autosave for {path}: {exception}");
                        await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                            "Autosave recovery unavailable",
                            $"The autosave could not be checked. The project will be opened without recovery.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                            MsBox.Avalonia.Enums.ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Warning).ShowWindowDialogAsync(ViewModelBase.MainWindow);
                    }

                    if (recovery != null)
                    {
                        var result = await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                            "Restore autosave",
                            $"A newer autosave from {recovery.CreatedAtUtc.ToLocalTime():g} is available for this project. Restore it?",
                            MsBox.Avalonia.Enums.ButtonEnum.YesNo,
                            MsBox.Avalonia.Enums.Icon.Question).ShowWindowDialogAsync(ViewModelBase.MainWindow);
                        if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
                        {
                            await Task.Run(() => AutosaveManager.RestoreSnapshot(
                                recoveryProject,
                                recovery,
                                ResourceModel.Get<ISettingModel>()));
                            LogModel.Log($"Restored autosave for {path} from {recovery.CreatedAtUtc:O}.");
                        }
                    }

                    // Switch to the theme of the project before showing its progress
                    ThemeManager.ApplyProject(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
                    progressDialog = new OpenProjectProgressDialog(path);
                    await progressDialog.ShowAndWaitForFirstFrameAsync();
                    var loadedProject = await ProjectProvider.Load(path, (progress) => {
                        if (progress >= 100)
                        {
                            Dispatcher.UIThread.Post(progressDialog.Close);
                        }
                    }, true, update =>
                    {
                        currentStep = string.IsNullOrEmpty(update.SubTask) ? update.Message : $"{update.Message} ({update.SubTask})";
                        logLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] > {currentStep}");
                        progressDialog.Report(update);
                    });
                    if (loadedProject != null)
                    {
                        RecentProvider.Add(loadedProject.ProjectFilePath, loadedProject.ProjectName);

                        OpenMainWindow(loadedProject);
                        await ProjectModel.Build(loadedProject);
                    }
                }
                catch (Exception e)
                {
                    progressDialog?.Close();
                    ThemeManager.ApplyGlobal();
                    var details = new StringBuilder();
                    details.AppendLine($"Project: {path}");
                    details.AppendLine($"Failed during: {currentStep}");
                    details.AppendLine();
                    details.AppendLine("Log:");
                    foreach (var line in logLines) details.AppendLine(line);
                    details.AppendLine();
                    details.AppendLine("Exception:");
                    details.AppendLine(e.ToString());

                    ShowErrorDetails(
                        "Could not open the project",
                        $"{Path.GetFileNameWithoutExtension(path)} failed to load while: {currentStep}.{Environment.NewLine}{Environment.NewLine}{e.Message}",
                        "Check that the project file and its modules are intact and that git, dotnet and typer are available. Show the details for the log of what happened before the error.",
                        details.ToString());
                }
                finally
                {
                    LogModel.DetachLogOutput(LogName);
                }
            }
        }
    }
}
