using System.Windows;
using System.Windows.Controls;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.View;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal class OptionsDialogViewModel : ViewModelBase
    {
        public class Option : ViewModelBase
        {
            public SettingItem Item { get; set; }
            public string Title { get => Item.Title; }
            public string Description { get => Item.Description; }
            public string Value {
                get => Item.Setting.GetValue().ToString();
                set => Item.Set(value);
            }

            public Option(SettingItem item)
            {
                Item = item;
            }
        }

        // Data
        TypeD.Models.Data.Project LoadedProject { get; set; }

        // Models
        IHookModel HookModel { get; set; }

        // Constructors
        public OptionsDialogViewModel(FrameworkElement element, TypeD.Models.Data.Project loadedProject) : base(element)
        {
            LoadedProject = loadedProject;

            HookModel = ResourceModel.Get<IHookModel>();
        }

        // Functions
        public void InitUI(ListBox listBox)
        {
            var hook = new OptionsHook() { Level = SettingLevel.Local };
            HookModel.Shoot(hook);

            listBox.Items.Clear();
            foreach(var item in hook.Items)
            {
                listBox.Items.Add(new Option(item));
            }
        }
    }
}
