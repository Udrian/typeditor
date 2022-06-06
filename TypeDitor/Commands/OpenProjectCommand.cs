using Microsoft.Win32;
using System;
using System.Windows;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Commands
{
    internal class OpenProjectCommand : ProjectCommands
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }
        private IProjectProvider ProjectProvider { get; set; }

        public OpenProjectCommand(FrameworkElement element) : base(element)
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();
            ProjectProvider = ResourceModel.Get<IProjectProvider>();
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
                var openFileDialog = new OpenFileDialog();
                openFileDialog.DefaultExt = ".typeo";
                openFileDialog.Filter = "TypeO Projects (*.typeo)|*.typeo";
                if (openFileDialog.ShowDialog() == true)
                {
                    path = openFileDialog.FileName;
                }
            }

            OpenProjectProgressDialog progressDialog = null;
            //Open project
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    progressDialog = new OpenProjectProgressDialog();
                    progressDialog.Show();
                    var loadedProject = await ProjectProvider.Load(path, (progress) => {
                        progressDialog.Progress = progress;
                        if (progress >= 100)
                        {
                            progressDialog.Close();
                        }
                    });
                    if (loadedProject != null)
                    {
                        RecentProvider.Add(loadedProject.ProjectFilePath, loadedProject.ProjectName);

                        OpenMainWindow(loadedProject);
                    }
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
