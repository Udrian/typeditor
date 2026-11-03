using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal class ModulesDialogViewModel : ViewModelBase
    {
        public class Module : ViewModelBase
        {
            public string Name { get; set; }
            public string Author { get; set; }
            public bool Enabled { get; set; }
            public bool IsDirect { get; set; }
            public bool IsLocal { get; set; }
            public string LocalPath { get; set; }
            public string InstalledVersion { get; set; }
            public string Version { get; set; }
            public List<string> Versions { get; set; } = new List<string>();
            public string DisplayVersion { get { return IsLocal ? $"local: {LocalPath}" : InstalledVersion; } }
        }

        // Models
        IModuleModel ModuleModel { get; set; }
        ITyperModel TyperModel { get; set; }

        // Providers
        IModuleProvider ModuleProvider { get; set; }

        // Data
        TypeD.Models.Data.Project LoadedProject { get; set; }
        public ObservableCollection<Module> Modules { get; set; }

        // Properties
        public Module SelectedModule { get; set; }
        public bool SelectedModuleVisibility { get { return SelectedModule == null ? false : true; } }
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

        // Constructors
        public ModulesDialogViewModel(Control element, TypeD.Models.Data.Project loadedProject) : base(element)
        {
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
                    Modules.Add(new Module()
                    {
                        Name = entry.Name,
                        Author = entry.Author ?? dependency?.Author,
                        Enabled = used != null || dependency != null,
                        IsDirect = dependency != null,
                        IsLocal = dependency?.IsLocal ?? used?.IsLocal ?? false,
                        LocalPath = dependency?.LocalPath,
                        InstalledVersion = dependency?.Version ?? used?.Version ?? "",
                        Version = dependency?.Version ?? versions.FirstOrDefault() ?? "",
                        Versions = versions
                    });
                }
                SelectedModule = Modules.FirstOrDefault(m => m.Name == selectedName);
                OnPropertyChanged(nameof(SelectedModule));
                OnPropertyChanged(nameof(SelectedModuleVisibility));
            }
            catch (System.Exception e)
            {
                ResourceModel.Get<ILogModel>()?.Log($"Could not list modules: {e}");
            }
            finally
            {
                Busy = false;
            }
        }

        public Task InstallSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || string.IsNullOrEmpty(module.Version)) return Task.CompletedTask;
            return Run(() => ModuleModel.Add(LoadedProject, module.Author, module.Name, module.Version));
        }

        public Task UpdateSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || !module.IsDirect || module.IsLocal) return Task.CompletedTask;
            return Run(() => ModuleModel.Update(LoadedProject, module.Name));
        }

        public Task UninstallSelectedModule()
        {
            var module = SelectedModule;
            if (module == null || !module.IsDirect) return Task.CompletedTask;
            return Run(() => ModuleModel.Remove(LoadedProject, module.Name));
        }

        public Task UseLocal(string path)
        {
            var module = SelectedModule;
            if (module == null) return Task.CompletedTask;
            return Run(async () =>
            {
                if (!module.IsDirect && !await ModuleModel.Add(LoadedProject, module.Author, module.Name, module.Version)) return false;
                return await ModuleModel.UseLocal(LoadedProject, module.Name, path);
            });
        }

        public Task UseReleased()
        {
            var module = SelectedModule;
            if (module == null || !module.IsLocal) return Task.CompletedTask;
            return Run(() => ModuleModel.UseReleased(LoadedProject, module.Name));
        }

        public Task UpdateLocalDependencies()
        {
            var module = SelectedModule;
            if (module == null || !module.IsLocal) return Task.CompletedTask;
            return Run(() => ModuleModel.UpdateLocal(LoadedProject, module.Name));
        }

        public Task CreateModule(string name, string outputPath)
        {
            return Run(() => ModuleModel.CreateLocal(LoadedProject, name, outputPath));
        }

        public void SelectedChanged(Module module)
        {
            SelectedModule = module;
            OnPropertyChanged(nameof(SelectedModule));
            OnPropertyChanged(nameof(SelectedModuleVisibility));
        }

        private async Task Run(System.Func<Task<bool>> action)
        {
            Busy = true;
            try
            {
                await action();
            }
            finally
            {
                Busy = false;
            }
            await ListModules();
        }
    }
}
