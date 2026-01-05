using System;
using Avalonia.Platform.Storage;
using TypeD.Helpers;
using TypeD.Models.Data;
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

        public OpenProjectCommand() : base()
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
