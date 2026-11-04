using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.IO;
using TypeD.Commands;
using TypeD.Models.Data;
using TypeD.ViewModel;
using TypeDitor.Models;
using TypeDitor.View;
using TypeDitor.View.Dialogs.Project;

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
            ThemeManager.ApplyProject(loadedProject.Location, loadedProject.ProjectName);
            var currentMainWindow = ViewModelBase.MainWindow;
            var mainWindow = new MainWindow(loadedProject);
            ViewModelBase.MainWindow = mainWindow;
            mainWindow.Show();
            currentMainWindow.Close();
        }

        protected async void ShowErrorDetails(string title, string summary, string hint, string details)
        {
            var dialog = new ErrorDetailsDialog(title, summary, hint, details);
            await dialog.ShowDialog(ViewModelBase.MainWindow);
        }

        protected async void ShowError(string error)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Error", error, ButtonEnum.Ok, Icon.Error);
            await box.ShowAsPopupAsync(ViewModelBase.MainWindow);
        }
    }
}
