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
                    progressDialog = new OpenProjectProgressDialog();
                    progressDialog.Show();
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
