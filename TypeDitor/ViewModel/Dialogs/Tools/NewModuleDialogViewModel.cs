using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal partial class NewModuleDialogViewModel : ViewModelBase
    {
        public class Module : ViewModelBase
        {
            internal NewModuleDialogViewModel Owner { get; set; }

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
                Version = typeO?.Version ?? "0.0.0";
            }
        }

        // Models
        private IModuleProvider ModuleProvider { get; set; }

        // Properties
        [ObservableProperty]
        private string _moduleAuthor;

        [ObservableProperty]
        private string _moduleName;

        [ObservableProperty]
        private string _moduleLocation;

        public ObservableCollection<Module> AvailableModules { get; set; }

        [ObservableProperty]
        private bool _modulesLoading;

        // Fetched product descriptions by author@name-version, so toggling modules does not fetch again
        private readonly Dictionary<string, ModuleTypeO> infoCache = new Dictionary<string, ModuleTypeO>();
        private bool refreshingSelection;
        private bool refreshPending;

        // Constructors
        public NewModuleDialogViewModel() : base()
        {
            AvailableModules = new ObservableCollection<Module>();
            ModuleProvider = ResourceModel.Get<IModuleProvider>();

            ModuleLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD");
        }

        // Functions
        public async Task OpenLocation(IStorageProvider storageProvider)
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Project Folder",
                SuggestedStartLocation = string.IsNullOrEmpty(ModuleLocation)
                    ? await storageProvider.TryGetFolderFromPathAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD"))
                    : await storageProvider.TryGetFolderFromPathAsync(ModuleLocation),
                AllowMultiple = false
            });
            if (folders.Count < 1) return;

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            ModuleLocation = path;
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
            if (string.IsNullOrWhiteSpace(ModuleLocation) || string.IsNullOrWhiteSpace(ModuleAuthor))
                return false;

            bool isValid = !string.IsNullOrEmpty(ModuleName) &&
                ModuleName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
                !ModuleName.Contains(" ") &&
                (char.IsLetter(ModuleName.FirstOrDefault()) || ModuleName.StartsWith("_"));
            if (!isValid)
            {
                return false;
            }
            isValid = !string.IsNullOrEmpty(ModuleAuthor) &&
                ModuleAuthor.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
                !ModuleAuthor.Contains(" ") &&
                (char.IsLetter(ModuleAuthor.FirstOrDefault()) || ModuleAuthor.StartsWith("_"));

            if (!isValid)
            {
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
