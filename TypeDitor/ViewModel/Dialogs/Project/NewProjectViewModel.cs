using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
        public NewProjectViewModel(Control element) : base(element)
        {
            ProjectLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD");
        }

        // Functions
        public async void OpenLocation()
        {
            var files = await MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Location Folder",
                SuggestedStartLocation = await MainWindow.StorageProvider.TryGetFolderFromPathAsync(ProjectLocation),
                AllowMultiple = false

            });

            if (files.Count >= 1)
            {
                ProjectLocation = files[0].Path.AbsolutePath;
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
                //MessageBox.Show($"Invalid name '{ProjectName}'");
                return false;
            }

            return true;
        }
    }
}
