using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Commands
{
    internal class NewProjectCommand : ProjectCommands
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }
        private IProjectProvider ProjectProvider { get; set; }
        private ILogModel LogModel { get; set; }

        public NewProjectCommand() : base()
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            ProjectProvider = ResourceModel.Get<IProjectProvider>();
            LogModel = ResourceModel.Get<ILogModel>();
        }

        public async override void Execute(object param)
        {
            var newProjectDialog = new NewProjectDialog();
            ProjectCreationProgressDialog progressDialog = null;
            if (await newProjectDialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true)
            {
                var logLines = new ConcurrentQueue<string>();
                var currentStep = "Starting";
                const string LogName = "NewProjectErrorLog";
                try
                {
                    LogModel.AttachLogOutput(LogName, line => logLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] {line}"));

                    var name = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectName);
                    var location = this.IsDirectory(newProjectDialog.ViewModel.ProjectLocation) ? newProjectDialog.ViewModel.ProjectLocation : Path.GetDirectoryName(newProjectDialog.ViewModel.ProjectLocation);
                    var solution = $"{newProjectDialog.ViewModel.ProjectCSSolutionName}.sln";
                    var project = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectCSProjectName);

                    // Collect selected modules with author information
                    currentStep = "Collecting selected modules";
                    var selectedModules = newProjectDialog.ViewModel.GetSelectedModules();
                    var modulesToAdd = new ObservableCollection<(string Author, string Name, string Version)>(
                        selectedModules.Select(m => (m.Author, m.Name, m.Version))
                    );
                    logLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] > Selected {modulesToAdd.Count} modules");

                    //New project
                    currentStep = "Creating project";
                    progressDialog = new ProjectCreationProgressDialog();
                    await progressDialog.ShowAndWaitForFirstFrameAsync();
                    
                    var newProject = await ProjectProvider.Create(name,
                                                                location,
                                                                solution,
                                                                project,
                                                                modulesToAdd,
                                                                (progress) =>
                                                                {
                                                                    if (progress >= 100)
                                                                    {
                                                                        progressDialog.Close();
                                                                    }
                                                                },
                                                                (update) =>
                                                                {
                                                                    currentStep = string.IsNullOrEmpty(update.SubTask) ? update.Message : $"{update.Message} ({update.SubTask})";
                                                                    progressDialog.Report(update);
                                                                });
                    
                    currentStep = "Adding to recent projects";
                    RecentProvider.Add(newProject.ProjectFilePath, newProject.ProjectName);
                    currentStep = "Opening project";
                    this.OpenMainWindow(newProject);
                }
                catch (Exception e)
                {
                    progressDialog?.Close();
                    var projectPath = Path.Combine(
                        this.IsDirectory(newProjectDialog.ViewModel.ProjectLocation) ? newProjectDialog.ViewModel.ProjectLocation : Path.GetDirectoryName(newProjectDialog.ViewModel.ProjectLocation),
                        Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectName)
                    );
                    var details = new StringBuilder();
                    details.AppendLine($"Project: {projectPath}");
                    details.AppendLine($"Failed during: {currentStep}");
                    details.AppendLine();
                    details.AppendLine("Log:");
                    foreach (var line in logLines) details.AppendLine(line);
                    details.AppendLine();
                    details.AppendLine("CMD Output:");
                    if (progressDialog != null)
                    {
                        var cmdOutput = progressDialog.GetCmdOutput();
                        if (!string.IsNullOrEmpty(cmdOutput))
                        {
                            details.AppendLine(cmdOutput);
                        }
                        else
                        {
                            details.AppendLine("(No command output captured)");
                        }
                    }
                    details.AppendLine();
                    details.AppendLine("Exception:");
                    details.AppendLine(e.ToString());

                    ShowErrorDetails(
                        "Could not create the project",
                        $"Project creation failed while: {currentStep}.{Environment.NewLine}{Environment.NewLine}{e.Message}",
                        "Check that the project location is valid and accessible. If modules were selected, verify they are available. Show the details for the log of what happened before the error.",
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
