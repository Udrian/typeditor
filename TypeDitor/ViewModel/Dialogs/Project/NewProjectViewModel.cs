using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Models;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class NewProjectViewModel : ViewModelBase
    {
        public class Module : ViewModelBase
        {
            internal NewProjectViewModel Owner { get; set; }

            public string Name { get; set; }
            public string Author { get; set; }
            public List<string> Versions { get; set; } = new List<string>();
            public bool IsLocal { get; private set; }
            public string LocalPath { get; private set; }
            internal ModuleTypeO LocalTypeO { get; private set; }

            private bool enabled;
            public bool Enabled
            {
                get => enabled;
                set
                {
                    if (enabled == value) return;
                    enabled = value;
                    OnPropertyChanged();
                    Owner?.SelectionChanged();
                }
            }

            private string version;
            public string Version
            {
                get => version;
                set
                {
                    version = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayVersion));
                }
            }

            private string warning;
            public string Warning
            {
                get => warning;
                set
                {
                    if (warning == value) return;
                    warning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasWarning));
                }
            }
            public bool HasWarning { get { return !string.IsNullOrEmpty(Warning); } }

            public string DisplayVersion { get { return IsLocal ? $"local: {LocalPath}" : Version; } }

            internal void SetEnabledSilently(bool value)
            {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(Enabled));
            }

            internal void MakeLocal(string path, ModuleTypeO typeO)
            {
                IsLocal = true;
                LocalPath = path;
                LocalTypeO = typeO;
                Version = typeO?.Version ?? "0.1.0";
            }
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

        private string projectLocation;
        public string ProjectLocation
        {
            get => projectLocation;
            set
            {
                if (projectLocation == value) return;
                projectLocation = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> ProjectFolders { get; } = new ObservableCollection<string>();
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

        // Fetched product descriptions by author@name-version, so toggling modules does not fetch again
        private readonly Dictionary<string, ModuleTypeO> infoCache = new Dictionary<string, ModuleTypeO>();
        private bool refreshingSelection;
        private bool refreshPending;

        // Constructors
        public NewProjectViewModel() : base()
        {
            AvailableModules = new ObservableCollection<Module>();

            ModuleModel = ResourceModel.Get<IModuleModel>();
            TyperModel = ResourceModel.Get<ITyperModel>();
            ModuleProvider = ResourceModel.Get<IModuleProvider>();

            foreach (var folder in ProjectScanSettings.GetFolders(ResourceModel.Get<ISettingModel>()))
                ProjectFolders.Add(folder);
            ProjectLocation = ProjectFolders.FirstOrDefault();
        }

        // Functions
        public async Task OpenLocation(IStorageProvider storageProvider)
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Project Folder",
                SuggestedStartLocation = string.IsNullOrEmpty(ProjectLocation)
                    ? null
                    : await storageProvider.TryGetFolderFromPathAsync(ProjectLocation),
                AllowMultiple = false
            });
            if (folders.Count < 1) return;

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

            var existing = ProjectFolders.FirstOrDefault(folder => ProjectScanSettings.PathsEqual(folder, path));
            if (existing == null)
            {
                ProjectFolders.Add(path);
                existing = path;
            }
            ProjectLocation = existing;
        }

        public async Task LoadAvailableModules()
        {
            ModulesLoading = true;
            try
            {
                var moduleList = (await ModuleProvider.List(null)).ToList();

                AvailableModules.Clear();
                foreach (var entry in moduleList)
                {
                    var versions = entry.Versions.Select(v => v.Version).ToList();
                    AvailableModules.Add(new Module()
                    {
                        Owner = this,
                        Name = entry.Name,
                        Author = entry.Author,
                        Version = ModuleSelection.LatestVersion(versions) ?? versions.FirstOrDefault() ?? "",
                        Versions = versions
                    });
                }

                // TypeOCore is the foundation of every project, so it starts selected at its latest version
                var core = AvailableModules.FirstOrDefault(m => m.Name == "TypeOCore");
                if (core != null) core.Enabled = true;
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

        public async void AddLocalModule()
        {
            var folders = await MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Local Module Folder",
                AllowMultiple = false
            });
            if (folders.Count < 1) return;

            var path = folders[0].Path.LocalPath;
            var local = ModuleProvider.ReadLocal(path);
            if (local == null)
            {
                ResourceModel.Get<ILogModel>()?.Log($"'{path}' is not a module folder, it has no module typeo.json");
                return;
            }

            var module = AvailableModules.FirstOrDefault(m => m.Name == local.Name);
            if (module == null)
            {
                module = new Module() { Owner = this, Name = local.Name };
                AvailableModules.Insert(0, module);
            }
            module.Author = local.Author;
            module.MakeLocal(path, local.Versions.FirstOrDefault());
            if (!module.Enabled)
            {
                module.Enabled = true;
            }
            else
            {
                SelectionChanged();
            }
        }

        // Enables and raises the versions of the modules the selected modules require, then recomputes the warnings
        internal async void SelectionChanged()
        {
            if (refreshingSelection)
            {
                refreshPending = true;
                return;
            }

            refreshingSelection = true;
            ModulesLoading = true;
            try
            {
                do
                {
                    refreshPending = false;
                    await RefreshSelection();
                } while (refreshPending);
            }
            catch (Exception e)
            {
                ResourceModel.Get<ILogModel>()?.Log($"Could not resolve the module selection: {e}");
            }
            finally
            {
                refreshingSelection = false;
                ModulesLoading = false;
            }
        }

        private async Task RefreshSelection()
        {
            var selected = new Dictionary<string, ModuleTypeO>(StringComparer.OrdinalIgnoreCase);

            var changed = true;
            while (changed)
            {
                changed = false;
                selected.Clear();
                foreach (var module in AvailableModules.Where(m => m.Enabled).ToList())
                {
                    selected[module.Name] = await Info(module);
                }

                foreach (var requirement in ModuleSelection.RequiredVersions(selected.Select(s => (s.Key, s.Value))).Values)
                {
                    var module = AvailableModules.FirstOrDefault(m => m.Name == requirement.Name);
                    if (module == null)
                    {
                        // A required module missing from the registry is still listed so it installs with the project
                        module = new Module()
                        {
                            Owner = this,
                            Name = requirement.Name,
                            Author = requirement.Author,
                            Version = requirement.Version,
                            Versions = new List<string>() { requirement.Version }
                        };
                        AvailableModules.Add(module);
                    }
                    if (!module.Enabled)
                    {
                        module.SetEnabledSilently(true);
                        changed = true;
                    }
                    if (!module.IsLocal && ModuleSelection.CompareVersions(module.Version, requirement.Version) < 0)
                    {
                        module.Version = requirement.Version;
                        changed = true;
                    }
                }
            }

            var required = ModuleSelection.RequiredVersions(selected.Select(s => (s.Key, s.Value)));
            foreach (var module in AvailableModules)
            {
                module.Warning = module.Enabled
                    ? ModuleSelection.Warning(module.Name, module.Version, selected.GetValueOrDefault(module.Name), required,
                        name => AvailableModules.FirstOrDefault(m => m.Enabled && m.Name == name)?.Version)
                    : null;
            }
        }

        private async Task<ModuleTypeO> Info(Module module)
        {
            if (module.IsLocal) return module.LocalTypeO;
            if (string.IsNullOrEmpty(module.Author) || string.IsNullOrEmpty(module.Version)) return null;

            var key = $"{module.Author}@{module.Name}-{module.Version}";
            if (!infoCache.TryGetValue(key, out var typeO))
            {
                infoCache[key] = typeO = await ModuleProvider.Info(module.Author, module.Name, module.Version);
            }
            return typeO;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ProjectLocation))
                return false;

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
