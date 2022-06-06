using Microsoft.Win32;
using System.IO;
using System.Windows;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Commands
{
    internal class ImportProjectCommand : ProjectCommands
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }
        private IProjectProvider ProjectProvider { get; set; }

        public ImportProjectCommand(FrameworkElement element) : base(element)
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            ProjectProvider = ResourceModel.Get<IProjectProvider>();
        }

        public override void Execute(object param)
        {
            var path = "";
            if (param is Recent)
            {
                path = (param as Recent).Path;
            }
            else
            {
                var openFileDialog = new OpenFileDialog();
                openFileDialog.DefaultExt = ".sln";
                openFileDialog.Filter = "Solution Files (*.sln)|*.sln";
                if (openFileDialog.ShowDialog() == true)
                {
                    path = openFileDialog.FileName;
                }
            }

            //Open project
            if (!string.IsNullOrEmpty(path))
            {
                var newProjectDialog = new NewProjectDialog();
                newProjectDialog.ViewModel.ProjectName = Path.GetFileNameWithoutExtension(path);

                var projectLocation = Path.GetDirectoryName(path);
                projectLocation = projectLocation.Substring(0, projectLocation.LastIndexOf(newProjectDialog.ViewModel.ProjectName)).TrimEnd('\\').TrimEnd('/');
                newProjectDialog.ViewModel.ProjectLocation = projectLocation;

                if (newProjectDialog.ShowDialog() == true)
                {
                    var name = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectName);
                    var location = this.IsDirectory(newProjectDialog.ViewModel.ProjectLocation) ? newProjectDialog.ViewModel.ProjectLocation : Path.GetDirectoryName(newProjectDialog.ViewModel.ProjectLocation);
                    var solution = @$".\{newProjectDialog.ViewModel.ProjectCSSolutionName}.sln";
                    var project = Path.GetFileNameWithoutExtension(newProjectDialog.ViewModel.ProjectCSProjectName);

                    //New project
                    var progressDialog = new ProjectCreationProgressDialog();
                    var newProjectTask = ProjectProvider.Create(name,
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
                    progressDialog.ShowDialog();
                    var newProject = newProjectTask.Result;
                    RecentProvider.Add(newProject.ProjectFilePath, newProject.ProjectName);
                    this.OpenMainWindow(newProject);
                }
            }
        }
    }
}
