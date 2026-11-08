using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TypeD.Models.Data;
using TypeD.ViewModel;
using TypeDCore.Models;
using TypeD.Helpers;
using TypeD.Models.Interfaces;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class CreateComponentTypeBaseViewModel : ViewModelBase
    {
        // Data
        TypeD.Models.Data.Project Project { get; set; }

        // Properties
        public string ComponentName { get; set; }
        public string ComponentNamespace { get; set; }

        public string ParentComponentFullName { get; set; }
        public Component ParentComponent { get; set; }
        public string ComponentBaseType { get; set; }

        /// <summary>
        /// The icon explicitly chosen for the new component, or <see langword="null"/> to inherit the one of the parent component.
        /// </summary>
        public string IconName { get; private set; }
        public Avalonia.Media.Imaging.Bitmap IconPreview
        {
            get { return IconCache.Get(Project, IconName ?? ParentComponent?.ResolvedIcon); }
        }
        public string IconText
        {
            get { return IconName ?? $"{ParentComponent?.ResolvedIcon ?? Component.DefaultIcon} (inherited)"; }
        }

        // Constructors
        public CreateComponentTypeBaseViewModel(Control element, TypeD.Models.Data.Project project, string @namespace, Component componentBaseType) : base(element)
        {
            Project = project;

            ComponentBaseType = componentBaseType.FullName;
            ComponentNamespace = @namespace;
            ParentComponentFullName = ComponentBaseType;
            ParentComponent = componentBaseType;
        }

        // Functions
        public virtual bool Validate()
        {
            bool isValid = !string.IsNullOrEmpty(ComponentName) &&
                            ComponentName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
                            !ComponentName.Contains(" ") &&
                            (char.IsLetter(ComponentName.FirstOrDefault()) || ComponentName.StartsWith("_"));
            if (!isValid)
            {
                //MessageBox.Show($"Invalid name '{ComponentName}'");
                return false;
            }

            if (File.Exists(@$"{Project.ProjectTypeOPath}\components\{Project.ProjectName}\{ComponentNamespace.Replace(".", "\\")}\{ComponentName}.component"))
            {
                //MessageBox.Show($"'{ComponentNamespace}.{ComponentName}' already exists");
                return false;
            }

            return true;
        }

        public async void OpenNamespace()
        {
            var files = await MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Location Folder",
                SuggestedStartLocation = await MainWindow.StorageProvider.TryGetFolderFromPathAsync(@$"{Project.ProjectSourcePath}\{ComponentNamespace.Replace(".", "\\")}"),
                AllowMultiple = false

            });

            if (files.Count >= 1)
            {
                ComponentNamespace = files[0].Path.AbsolutePath.Replace("\\", ".").Substring(@$"{Project.ProjectSourcePath}\".Length);
                OnPropertyChanged(nameof(ComponentNamespace));
            }
        }

        public async Task OpenComponents()
        {
            var filter = new FilterHelper { Filters = $"{ComponentBaseType};" };
            var component = await ResourceModel.Get<IEditorDialogModel>().SelectComponentAsync(Project, filter);
            if (component != null)
            {
                OnParentComponentSet(component);
                OnPropertyChanged(nameof(ParentComponentFullName));
            }
        }

        public virtual void OnParentComponentSet(Component parentComponent)
        {
            ParentComponent = parentComponent;
            ParentComponentFullName = ParentComponent.FullName;
            OnPropertyChanged(nameof(IconPreview));
            OnPropertyChanged(nameof(IconText));
        }

        public async Task OpenIcons()
        {
            var icon = await ResourceModel.Get<IEditorDialogModel>().SelectIconAsync(Project, IconName ?? ParentComponent?.ResolvedIcon);
            if (icon != null)
            {
                IconName = icon;
                OnPropertyChanged(nameof(IconPreview));
                OnPropertyChanged(nameof(IconText));
            }
        }
    }
}
