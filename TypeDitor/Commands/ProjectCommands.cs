using System.IO;
using Avalonia.Controls;
using TypeD.Commands;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View;

namespace TypeDitor.Commands
{
    internal class ProjectCommands : CustomCommand
    {
        // Constructors
        public ProjectCommands(Control element = null) : base(element?.FindResource("ResourceModel") as IResourceModel) { }

        // Internals
        protected bool IsDirectory(string filePath)
        {
            return Path.GetFileName(filePath) == Path.GetFileNameWithoutExtension(filePath);
        }

        protected void OpenMainWindow(Project loadedProject)
        {
            var currentMainWindow = ViewModelBase.MainWindow;
            var mainWindow = new MainWindow(loadedProject);
            ViewModelBase.MainWindow = mainWindow;
            mainWindow.Show();
            currentMainWindow.Close();
        }

        protected void ShowError(string error)
        {
            //MessageBox.Show(error);
        }
    }
}
