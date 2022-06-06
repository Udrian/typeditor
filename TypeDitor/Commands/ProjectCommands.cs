using System.IO;
using System.Windows;
using TypeD.Commands;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeDitor.View;

namespace TypeDitor.Commands
{
    internal class ProjectCommands : CustomCommand
    {
        // Constructors
        public ProjectCommands(FrameworkElement element = null) : base(element?.FindResource("ResourceModel") as IResourceModel) { }

        // Internals
        protected bool IsDirectory(string filePath)
        {
            return Path.GetFileName(filePath) == Path.GetFileNameWithoutExtension(filePath);
        }

        protected void OpenMainWindow(Project loadedProject)
        {
            var currentMainWindow = Application.Current.MainWindow;
            var mainWindow = new MainWindow(loadedProject);
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            currentMainWindow.Close();
        }

        protected void ShowError(string error)
        {
            MessageBox.Show(error);
        }
    }
}
