using CommunityToolkit.Mvvm.ComponentModel;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal partial class ModuleBranchesDialogViewModel : ViewModelBase
    {
        private ISettingModel SettingModel { get; set; }

        public ModuleBranchesDialogViewModel()
        {
            SettingModel = ResourceModel.Get<ISettingModel>();
        }

        [ObservableProperty]
        private string _branchesDisplayed;

        public void Load()
        {
            ModuleSettingContext moduleSetting = SettingModel.GetContext<ModuleSettingContext>(SettingLevel.System);
            if(moduleSetting == null) return;
            BranchesDisplayed = string.Join("\n",  moduleSetting?.Branches.Value);
        }

        public bool Save()
        {
            ModuleSettingContext moduleSetting = SettingModel.GetContext<ModuleSettingContext>(SettingLevel.System);
            BranchesDisplayed = BranchesDisplayed.Replace("\r", "");

            if (string.Join("\n",  moduleSetting?.Branches.Value) == BranchesDisplayed) return false;

            if (string.IsNullOrWhiteSpace(BranchesDisplayed))
                moduleSetting.Branches.Value.Clear();
            else
                moduleSetting.Branches.SetValue(BranchesDisplayed.Split("\n").ToList().Select(b => b.Trim()).ToList());
            SettingModel.SetContext(moduleSetting, SettingLevel.System);

            return true;
        }
    }
}