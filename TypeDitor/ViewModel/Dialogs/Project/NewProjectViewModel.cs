using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class NewProjectViewModel : ViewModelBase
    {
        // Properties
        private string projectName;
        public string ProjectName
        {
            get => projectName;
            set
            {
                if (ProjectName == ProjectCSSolutionName)
                {
                    ProjectCSSolutionName = value;
                    OnPropertyChanged(nameof(ProjectCSSolutionName));
                }
                if (ProjectName == ProjectCSProjectName)
                {
                    ProjectCSProjectName = value;
                    OnPropertyChanged(nameof(ProjectCSProjectName));
                }
                projectName = value;
            }
        }

        public string ProjectLocation { get; set; }
        public string ProjectCSSolutionName { get; set; }
        public string ProjectCSProjectName { get; set; }

        // Constructors
        public NewProjectViewModel(FrameworkElement element) : base(element)
        {
            ProjectLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD");
        }

        // Functions
        public void OpenLocation()
        {
            var folderBrowserDialog = new VistaFolderBrowserDialog();
            folderBrowserDialog.SelectedPath = ProjectLocation;
            if (folderBrowserDialog.ShowDialog() == true)
            {
                ProjectLocation = folderBrowserDialog.SelectedPath;
                OnPropertyChanged(nameof(ProjectLocation));
            }
        }

        public bool Validate()
        {
            bool isValid = !string.IsNullOrEmpty(ProjectName) &&
                ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
                !ProjectName.Contains(" ") &&
                (char.IsLetter(ProjectName.FirstOrDefault()) || ProjectName.StartsWith("_"));
            if (!isValid)
            {
                MessageBox.Show($"Invalid name '{ProjectName}'");
                return false;
            }

            return true;
        }
    }
}
