using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Models
{
    internal sealed class EditorDialogModel : IEditorDialogModel
    {
        public async Task<Component> SelectComponentAsync(Project project, FilterHelper typeFilter = null, FilterHelper nameFilter = null, bool excludeAbstract = false)
        {
            var dialog = new ComponentSelectorDialog(project);
            if (typeFilter != null)
                dialog.ViewModel.TypeFilter = typeFilter;
            if (nameFilter != null)
                dialog.ViewModel.NameFilter = nameFilter;
            dialog.ViewModel.ExcludeAbstract = excludeAbstract;
            dialog.ViewModel.UpdateFilter();
            return await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true
                ? dialog.ViewModel.SelectedComponent : null;
        }

        public async Task<Component> SelectMoveTargetAsync(Component root, Component node)
        {
            var dialog = new ComponentMoveTargetDialog(root, node);
            return await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true
                ? dialog.SelectedComponent : null;
        }

        public async Task<string> SelectIconAsync(Project project, string currentIcon)
        {
            var dialog = new IconSelectorDialog(project, currentIcon);
            return await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true
                ? dialog.SelectedIcon : null;
        }

        public async Task<string> SelectContentAsync(Project project, IEnumerable<string> extensions)
        {
            var dialog = new ContentSelectorDialog(project, extensions);
            await dialog.ShowDialog(ViewModelBase.MainWindow);
            return dialog.SelectedPath;
        }

        public async Task<string> RenameComponentAsync(Component component)
        {
            var dialog = new RenameComponentTypeDialog(component);
            return await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true
                ? dialog.ViewModel.Name : null;
        }

        public Task<ComponentCreationOptions> CreateComponentAsync(Project project, string componentNamespace, Component baseComponent)
        {
            var hook = ViewModelBase.ResourceModel.Get<IHookModel>().Shoot(new CreateComponentDialogHook
            {
                Project = project,
                ComponentNamespace = componentNamespace,
                BaseComponent = baseComponent
            });
            return hook.Result ?? Task.FromResult<ComponentCreationOptions>(null);
        }

        public Task<(CustomProperty Property, Type Type)?> AddCustomPropertyAsync(IEnumerable<IPropertyDefinition> definitions, IEnumerable<string> takenNames)
        {
            var hook = ViewModelBase.ResourceModel.Get<IHookModel>().Shoot(new AddCustomPropertyDialogHook
            {
                Definitions = definitions,
                TakenNames = takenNames
            });
            return hook.Result ?? Task.FromResult<(CustomProperty Property, Type Type)?>(null);
        }
    }
}