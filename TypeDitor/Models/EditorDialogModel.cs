using Avalonia.Controls;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Dialogs.Project;
using TypeDitor.ViewModel.Dialogs.Project;
using TypeOEngine.Typedeaf.Core.Entities;
using TypeOEngine.Typedeaf.Core.Entities.Drawables;

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

        public async Task<ComponentCreationOptions> CreateComponentAsync(Project project, string componentNamespace, Component baseComponent)
        {
            Window dialog = baseComponent.FullName == typeof(Entity).FullName
                ? new CreateEntityTypeDialog(project, componentNamespace)
                : baseComponent.FullName == typeof(Drawable).FullName
                    ? new CreateDrawableTypeDialog(project, componentNamespace)
                    : new CreateSceneTypeDialog(project, componentNamespace);
            if (await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) != true)
                return null;

            var viewModel = (CreateComponentTypeBaseViewModel)dialog.DataContext;
            var entity = viewModel as CreateEntityTypeViewModel;
            return new ComponentCreationOptions
            {
                Name = viewModel.ComponentName,
                Namespace = viewModel.ComponentNamespace,
                ParentComponent = viewModel.ParentComponent,
                BaseType = viewModel.ComponentBaseType,
                Icon = viewModel.IconName,
                Updatable = entity?.ComponentUpdatable ?? false,
                Drawable = entity?.ComponentDrawable ?? false
            };
        }

        public async Task<(CustomProperty Property, Type Type)?> AddCustomPropertyAsync(IEnumerable<IPropertyDefinition> definitions, IEnumerable<string> takenNames)
        {
            var dialog = new AddCustomPropertyDialog(definitions, takenNames);
            return await dialog.ShowDialog<bool>(ViewModelBase.MainWindow) == true
                ? (dialog.ViewModel.Build(), dialog.ViewModel.SelectedType.Type) : null;
        }
    }
}