using System;
using System.IO;
using System.Windows;
using TypeD.Models.Providers.Interfaces;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Commands
{
    internal class NewProjectCommand : ProjectCommands
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }
        private IProjectProvider ProjectProvider { get; set; }

        public NewProjectCommand(FrameworkElement element) : base(element)
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            ProjectProvider = ResourceModel.Get<IProjectProvider>();
        }

        public async override void Execute(object param)
        {
            var newProjectDialog = new NewProjectDialog();
            ProjectCreationProgressDialog progressDialog = null;
            if (newProjectDialog.ShowDialog() == true)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectName);
                    var location = this.IsDirectory(newProjectDialog.ViewModel.ProjectLocation) ? newProjectDialog.ViewModel.ProjectLocation : Path.GetDirectoryName(newProjectDialog.ViewModel.ProjectLocation);
                    var solution = @$".\{newProjectDialog.ViewModel.ProjectCSSolutionName}.sln";
                    var project = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectCSProjectName);

                    //New project
                    progressDialog = new ProjectCreationProgressDialog();
                    progressDialog.Show();
                    var newProject = await ProjectProvider.Create(name,
                                                                location,
                                                                solution,
                                                                project,
                                                                (progress) =>
                                                                {
                                                                    progressDialog.Progress = progress;
                                                                    if (progress >= 100)
                                                                    {
                                                                        progressDialog.Close();
                                                                    }
                                                                });
                    RecentProvider.Add(newProject.ProjectFilePath, newProject.ProjectName);
                    this.OpenMainWindow(newProject);
                }
                catch (Exception e)
                {
                    progressDialog?.Close();
                    ShowError($"Error loading project:{Environment.NewLine}{e.Message}");
                }
            }
        }
    }
}
