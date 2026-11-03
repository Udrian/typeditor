using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class NewProjectViewModel : ViewModelBase
    {
        public class Module : ViewModelBase
        {
            public string Name { get; set; }
            public string Author { get; set; }
            public bool Enabled { get; set; }
            public string InstalledVersion { get; set; }
            public string Version { get; set; }
            public string DisplayVersion { get { return InstalledVersion; } }
        }

        // Models
        private IModuleModel ModuleModel { get; set; }
        private ITyperModel TyperModel { get; set; }
        private IModuleProvider ModuleProvider { get; set; }

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

        public ObservableCollection<Module> AvailableModules { get; set; }

        private bool modulesLoading;
        public bool ModulesLoading
        {
            get => modulesLoading;
            set
            {
                modulesLoading = value;
                OnPropertyChanged();
            }
        }

        // Constructors
        public NewProjectViewModel() : base()
        {
            ProjectLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD");
            AvailableModules = new ObservableCollection<Module>();

            ModuleModel = ResourceModel.Get<IModuleModel>();
            TyperModel = ResourceModel.Get<ITyperModel>();
            ModuleProvider = ResourceModel.Get<IModuleProvider>();
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

        public async Task LoadAvailableModules()
        {
            ModulesLoading = true;
            try
            {
                // Create a temporary project-like object to query available modules
                var moduleList = (await ModuleProvider.List(null))
                    .Select(m => (m.Name, m.Author, Versions: m.Versions.Select(v => v.Version).ToList())).ToList();

                AvailableModules.Clear();
                foreach (var entry in moduleList)
                {
                    AvailableModules.Add(new Module()
                    {
                        Name = entry.Name,
                        Author = entry.Author,
                        Enabled = false, // All unchecked by default (only required modules will be added later)
                        InstalledVersion = entry.Versions.FirstOrDefault() ?? "",
                        Version = entry.Versions.FirstOrDefault() ?? ""
                    });
                }
            }
            catch
            {
                // If module listing fails, continue without module selection
            }
            finally
            {
                ModulesLoading = false;
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

        public ObservableCollection<Module> GetSelectedModules()
        {
            return new ObservableCollection<Module>(AvailableModules.Where(m => m.Enabled));
        }
    }
}
