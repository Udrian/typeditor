using System.IO;
using TypeD.Commands;
using TypeD.Models.Data;
using TypeD.ViewModel;
using TypeDitor.View;

namespace TypeDitor.Commands
{
    internal class ProjectCommands : CustomCommand
    {
        // Constructors
        public ProjectCommands() : base() { }

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
