using CommunityToolkit.Mvvm.ComponentModel;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal partial class ModuleRepositoriesDialogViewModel : ViewModelBase
    {
        private ISettingModel SettingModel { get; set; }

        public ModuleRepositoriesDialogViewModel()
        {
            SettingModel = ResourceModel.Get<ISettingModel>();
        }

        [ObservableProperty]
        private string _repositoriesDisplayed;

        public void Load()
        {
            ModuleSettingContext moduleSetting = SettingModel.GetContext<ModuleSettingContext>(SettingLevel.System);
            if(moduleSetting == null) return;
            RepositoriesDisplayed = string.Join("\n",  moduleSetting?.Repositories.Value);
        }

        public bool Save()
        {
            ModuleSettingContext moduleSetting = SettingModel.GetContext<ModuleSettingContext>(SettingLevel.System);
            RepositoriesDisplayed = RepositoriesDisplayed.Replace("\r", "");

            if (string.Join("\n",  moduleSetting?.Repositories.Value) == RepositoriesDisplayed) return false;

            if (string.IsNullOrWhiteSpace(RepositoriesDisplayed))
                moduleSetting.Repositories.Value.Clear();
            else
                moduleSetting.Repositories.SetValue(RepositoriesDisplayed.Split("\n").ToList().Select(b => b.Trim()).ToList());
            SettingModel.SetContext(moduleSetting, SettingLevel.System);

            return true;
        }
    }
}