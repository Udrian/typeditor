using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Dialogs.Tools;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal class ModulesDialogViewModel : ViewModelBase
    {
        public class Module : ViewModelBase
        {
            internal ModulesDialogViewModel Owner { get; set; }

            public string Name { get; set; }
            public string Author { get; set; }
            public bool Enabled { get; set; }
            public bool IsDirect { get; set; }
            public bool IsLocal { get; set; }
            public string LocalPath { get; set; }
            public string InstalledVersion { get; set; }
            public ObservableCollection<string> Versions { get; set; } = new ObservableCollection<string>();

            private string version;
            public string Version
            {
                get => version;
                set
                {
                    if (version == value) return;
                    version = value;
                    OnPropertyChanged();
                    Owner?.SelectionChanged();
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

            public string DisplayVersion { get { return IsLocal ? $"local: {LocalPath}" : InstalledVersion; } }
            public string DisplayName { get { return !string.IsNullOrEmpty(Author) ? $"{Author}@{Name}" : Name; } }
            public bool IsTransitive { get { return !IsDirect && !IsLocal && !string.IsNullOrEmpty(InstalledVersion); } }

            internal void SetVersionSilently(string value)
            {
                if (version == value) return;
                version = value;
                if (!string.IsNullOrEmpty(value) && !Versions.Contains(value)) Versions.Add(value);
                OnPropertyChanged(nameof(Version));
            }
        }

        // Models
        IModuleModel ModuleModel { get; set; }
        ITyperModel TyperModel { get; set; }

        // Providers
        IModuleProvider ModuleProvider { get; set; }

        // Data
        private readonly Window OwnerWindow;
        TypeD.Models.Data.Project LoadedProject { get; set; }
        public ObservableCollection<Module> Modules { get; set; }

        // Properties
        public Module SelectedModule { get; set; }
        public bool SelectedModuleVisibility { get { return SelectedModule == null ? false : true; } }
        public bool IsCurrentModuleInstalled { get { return SelectedModule != null && !string.IsNullOrEmpty(SelectedModule.InstalledVersion); } }
        public bool IsCurrentModuleLocal { get { return SelectedModule != null && SelectedModule.IsLocal; } }
        public bool IsCurrentModuleOnLatest { get { return SelectedModule != null && (ModuleSelection.CompareVersions(SelectedModule.Version, SelectedModule.InstalledVersion) > 0 ? SelectedModule.Version : null) != null; } }
        private bool busy;
        public bool Busy
        {
            get => busy;
            set
            {
                busy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Idle));
            }
        }
        public bool Idle { get { return !Busy; } }

        // Fetched product descriptions by author@name-version, so changing selections does not fetch again
        private readonly Dictionary<string, ModuleTypeO> infoCache = new Dictionary<string, ModuleTypeO>();
        private bool refreshingSelection;
        private bool refreshPending;

        // Constructors
        public ModulesDialogViewModel(Window element, TypeD.Models.Data.Project loadedProject) : base(element)
        {
            OwnerWindow = element;
            Modules = new ObservableCollection<Module>();
            LoadedProject = loadedProject;

            ModuleModel = ResourceModel.Get<IModuleModel>();
            TyperModel = ResourceModel.Get<ITyperModel>();
            ModuleProvider = ResourceModel.Get<IModuleProvider>();
        }

        // Functions
        public async Task ListModules()
        {
            Busy = true;
            refreshingSelection = true;
            try
            {
                var selectedName = SelectedModule?.Name;
                var moduleList = (await ModuleProvider.List(LoadedProject))
                    .Select(m => (m.Name, m.Author, Versions: m.Versions.Select(v => v.Version).ToList())).ToList();
                var dependencies = (await TyperModel.ListDependencies(LoadedProject.Location)).Where(d => !d.Nuget && d.Tags.Count == 0 && d.Name != "TypeD").ToList();

                // The project's own dependencies are always listed, even if typer could not list the available modules
                foreach (var dependency in dependencies.Where(d => !moduleList.Exists(m => m.Name == d.Name)))
                {
                    moduleList.Add((dependency.Name, dependency.Author, new List<string>() { dependency.Version }));
                }

                Modules.Clear();
                foreach (var entry in moduleList)
                {
                    var used = LoadedProject.Modules.Find(m => m.Name == entry.Name);
                    var dependency = dependencies.Find(d => d.Name == entry.Name);
                    var versions = entry.Versions;
                    var module = new Module()
                    {
                        Name = entry.Name,
                        Author = entry.Author ?? dependency?.Author,
                        Enabled = used != null || dependency != null,
                        IsDirect = dependency != null,
                        IsLocal = dependency?.IsLocal ?? used?.IsLocal ?? false,
                        LocalPath = dependency?.LocalPath,
                        InstalledVersion = dependency?.Version ?? used?.Version ?? "",
                        Versions = new ObservableCollection<string>(versions)
                    };
                    module.SetVersionSilently(dependency?.Version ?? ModuleSelection.LatestVersion(versions) ?? versions.FirstOrDefault() ?? "");
                    module.Owner = this;
                    Modules.Add(module);
                }
                SelectedModule = Modules.FirstOrDefault(m => m.Name == selectedName);
                OnPropertyChanged(nameof(SelectedModule));
                OnPropertyChanged(nameof(SelectedModuleVisibility));
                OnPropertyChanged(nameof(IsCurrentModuleInstalled));
                OnPropertyChanged(nameof(IsCurrentModuleLocal));
            }
            catch (System.Exception e)
            {
                ResourceModel.Get<ILogModel>()?.Log($"Could not list modules: {e}");
                await ShowModuleError(e.Message);
            }
            finally
            {
                refreshingSelection = false;
                Busy = false;
            }
            SelectionChanged();
        }

        // Raises the versions the selected modules require on their dependencies, then recomputes the update warnings
        internal async void SelectionChanged()
        {
            if (refreshingSelection)
            {
                refreshPending = true;
                return;
            }

            refreshingSelection = true;
            try
            {
                do
                {
                    refreshPending = false;
                    await RefreshSelection();
                } while (refreshPending);
            }
            catch (System.Exception e)
            {
                ResourceModel.Get<ILogModel>()?.Log($"Could not resolve the module selection: {e}");
            }
            finally
            {
                refreshingSelection = false;
            }
            OnPropertyChanged(nameof(IsCurrentModuleOnLatest));
        }

        private async Task RefreshSelection()
        {
            var selected = new Dictionary<string, ModuleTypeO>(StringComparer.OrdinalIgnoreCase);

            var changed = true;
            while (changed)
            {
                changed = false;
                selected.Clear();
                foreach (var module in Modules.Where(m => m.Enabled).ToList())
                {
                    selected[module.Name] = await Info(module);
                }

                foreach (var requirement in ModuleSelection.RequiredVersions(selected.Select(s => (s.Key, s.Value))).Values)
                {
                    var module = Modules.FirstOrDefault(m => m.Name == requirement.Name);
                    if (module == null || module.IsLocal || !module.Enabled) continue;
                    if (ModuleSelection.CompareVersions(module.Version, requirement.Version) < 0)
                    {
                        module.SetVersionSilently(requirement.Version);
                        changed = true;
                    }
                }
            }

            var required = ModuleSelection.RequiredVersions(selected.Select(s => (s.Key, s.Value)));
            foreach (var module in Modules)
            {
                // Installed modules warn on their installed version, so the warning stays until they are actually updated
                var version = module.Enabled && !string.IsNullOrEmpty(module.InstalledVersion) && !module.IsLocal
                    ? module.InstalledVersion : module.Version;
                module.Warning = module.Enabled
                    ? ModuleSelection.Warning(module.Name, version, selected.GetValueOrDefault(module.Name), required,
                        name => Modules.FirstOrDefault(m => m.Enabled && m.Name == name)?.Version)
                    : null;
            }
        }

        private async Task<ModuleTypeO> Info(Module module)
        {
            var used = LoadedProject.Modules.Find(m => m.Name == module.Name);
            if (module.IsLocal)
                return used != null ? used.TypeO
                    : ModuleProvider.ReadLocal(Path.GetFullPath(module.LocalPath, LoadedProject.Location))?.Versions.FirstOrDefault();
            if (used != null && used.Version == module.Version && used.TypeO != null) return used.TypeO;
            if (string.IsNullOrEmpty(module.Author) || string.IsNullOrEmpty(module.Version)) return used?.TypeO;

            var key = $"{module.Author}@{module.Name}-{module.Version}";
            if (!infoCache.TryGetValue(key, out var typeO))
            {
                infoCache[key] = typeO = await ModuleProvider.Info(module.Author, module.Name, module.Version) ?? used?.TypeO;
            }
            return typeO;
        }

        public Task InstallSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || string.IsNullOrEmpty(module.Version)) return Task.CompletedTask;
            return Run($"Installing {module.Name} {module.Version}",
                report => ModuleModel.Add(LoadedProject, module.Author, module.Name, module.Version, report));
        }

        public Task UpdateSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || !module.IsDirect || module.IsLocal) return Task.CompletedTask;
            // Updates to the selected version when one newer than the installed one is picked, otherwise to the latest
            var version = ModuleSelection.CompareVersions(module.Version, module.InstalledVersion) > 0 ? module.Version : null;
            return Run($"Updating {module.Name}",
                report => ModuleModel.Update(LoadedProject, module.Name, version, report));
        }

        public Task UninstallSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || !module.IsDirect) return Task.CompletedTask;
            return Run($"Uninstalling {module.Name}",
                report => ModuleModel.Remove(LoadedProject, module.Name, report));
        }

        public Task UseLocal(string path)
        {
            var module = SelectedModule;
            if (module == null) return Task.CompletedTask;
            return Run($"Using local {module.Name}", async report =>
            {
                if (!module.IsDirect && !await ModuleModel.Add(LoadedProject, module.Author, module.Name, module.Version, report)) return false;
                return await ModuleModel.UseLocal(LoadedProject, module.Name, path, report);
            });
        }

        public Task UseReleased()
        {
            var module = SelectedModule;
            if (module == null || !module.IsLocal) return Task.CompletedTask;
            return Run($"Using released {module.Name}",
                report => ModuleModel.UseReleased(LoadedProject, module.Name, report));
        }

        public Task UpdateLocalDependencies()
        {
            var module = SelectedModule;
            if (module == null || !module.IsLocal) return Task.CompletedTask;
            return Run($"Updating dependencies of {module.Name}",
                report => ModuleModel.UpdateLocal(LoadedProject, module.Name, report));
        }

        public Task CreateModule(string name, string outputPath)
        {
            return Run($"Creating module {name}",
                report => ModuleModel.CreateLocal(LoadedProject, name, outputPath, report));
        }

        public void SelectedChanged(Module module)
        {
            SelectedModule = module;
            OnPropertyChanged(nameof(SelectedModule));
            OnPropertyChanged(nameof(SelectedModuleVisibility));
            OnPropertyChanged(nameof(IsCurrentModuleInstalled));
            OnPropertyChanged(nameof(IsCurrentModuleLocal));
        }

        // Shows the operation dialog with the status, output and progress while the module change runs
        private async Task Run(string title, Func<Action<ProgressUpdate>, Task<bool>> action)
        {
            Busy = true;
            var dialog = new ModuleOperationDialog(title);
            var closed = dialog.ShowDialog(OwnerWindow);
            var success = false;
            try
            {
                success = await action(dialog.Report);
            }
            catch (Exception exception)
            {
                ResourceModel.Get<ILogModel>()?.Log($"Could not change project modules: {exception}");
                dialog.ReportOutput(exception.Message);
            }
            finally
            {
                // The dialog closes itself on success and stays open on failure so the output can be read
                dialog.Complete(success);
                Busy = false;
            }
            await closed;
            await ListModules();
        }

        private Task ShowModuleError(string message)
        {
            return MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                "Could not change project modules", message, MsBox.Avalonia.Enums.ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error).ShowWindowDialogAsync(OwnerWindow);
        }
    }
}
