using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.View;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Tools
{
    internal class OptionsDialogViewModel : ViewModelBase
    {
        public sealed class SettingCategory
        {
            public string Name { get; }
            public ObservableCollection<Option> Options { get; } = new();
            public ObservableCollection<Option> VisibleOptions { get; } = new();

            public SettingCategory(string name)
            {
                Name = name;
            }
        }

        public sealed class Option : ViewModelBase
        {
            private readonly Action<EditorCommand, string> SetShortcut;
            private readonly SettingItem Item;
            private readonly ISettingModel Settings;
            private readonly TypeD.Models.Data.Project Project;
            private readonly ISetting DefaultSetting;
            private string Entry => Command?.Id;
            public bool CanSetProjectDefault => Project != null && DefaultSetting != null && Settings != null;
            public bool HasProjectDefault => CanSetProjectDefault && Settings.HasProjectDefault(DefaultSetting, Entry);
            public string DefaultLabel => HasProjectDefault ? "Project default" : "Default";
            private object RevertValue => Settings != null && DefaultSetting != null
                ? Settings.GetRevertValue(DefaultSetting, Entry, Command?.DefaultShortcut)
                : IsShortcut ? Command.DefaultShortcut : Item.Setting.GetDefaultValue();

            public string Title => Item?.Title ?? Command?.Name;
            public string Description => Item?.Description ?? Command?.Description;
            public EditorCommand Command { get; }
            public bool IsShortcut => Command != null;
            public string ShortcutName => IsShortcut ? "Shortcut" : "Value";
            public PropertyNode Editor { get; }
            public IDataTemplate EditorTemplate { get; }

            public string Value => Command?.Shortcut;

            public bool CanRevert
            {
                get
                {
                    if (IsShortcut)
                        return !string.Equals(Command.Shortcut, (string)RevertValue, StringComparison.OrdinalIgnoreCase);

                    var current = Item.Setting.GetValue();
                    var defaultValue = RevertValue;
                    return !Equals(current, defaultValue);
                }
            }

            public Option(SettingItem item, IPropertyModel propertyModel, ISettingModel settings = null, TypeD.Models.Data.Project project = null)
            {
                Item = item;
                Settings = settings;
                Project = project;
                DefaultSetting = item.Setting;
                var property = new Property
                {
                    Name = item.Title,
                    Description = item.Description,
                    Group = item.Category,
                    Type = item.Setting.GetValueType(),
                    Value = item.Setting.GetValue()
                };
                var definition = propertyModel.Get(property.Type);
                Editor = definition?.CreateNode(null, property, (_, value) => SetValue(value))
                    ?? new PropertyNode(null, property, (_, value) => SetValue(value));
                EditorTemplate = (definition ?? propertyModel.Get(typeof(string)))?.CreateTemplate();
            }

            public Option(EditorCommand command, Action<EditorCommand, string> setShortcut,
                ISettingModel settings = null, TypeD.Models.Data.Project project = null, ISetting shortcutSetting = null)
            {
                Command = command;
                SetShortcut = setShortcut;
                Settings = settings;
                Project = project;
                DefaultSetting = shortcutSetting;
            }

            public void Revert()
            {
                if (!CanRevert)
                    return;

                var revertValue = RevertValue;
                if (IsShortcut)
                    UpdateShortcut((string)revertValue);
                else
                    SetValue(revertValue);
                if (CanSetProjectDefault && Equals(IsShortcut ? Command.Shortcut : Item.Setting.GetValue(), revertValue))
                    Settings.ClearUserOverride(DefaultSetting, Entry);

                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(CanRevert));
            }

            public async Task SetProjectDefaultAsync(bool remove = false)
            {
                if (!CanSetProjectDefault)
                    throw new InvalidOperationException("Open a project before changing project defaults.");
                var userOverride = Settings.HasUserOverride(DefaultSetting, Entry);
                await Settings.SetProjectDefaultAsync(Project, DefaultSetting,
                    IsShortcut ? Command.Shortcut : Item.Setting.GetValue(), remove, Entry);
                if (!userOverride)
                {
                    var revertValue = RevertValue;
                    if (IsShortcut) UpdateShortcut((string)revertValue);
                    else SetValue(revertValue);
                    if (Equals(IsShortcut ? Command.Shortcut : Item.Setting.GetValue(), revertValue))
                        Settings.ClearUserOverride(DefaultSetting, Entry);
                }
                RefreshDefaults();
            }

            public void RefreshDefaults()
            {
                OnPropertyChanged(nameof(HasProjectDefault));
                OnPropertyChanged(nameof(DefaultLabel));
                OnPropertyChanged(nameof(CanRevert));
            }

            private void SetValue(object value)
            {
                Item.Set(value);
                Editor.Property.Value = Item.Setting.GetValue();
                Editor.RefreshValue();
                OnPropertyChanged(nameof(CanRevert));
            }

            public void UpdateShortcut(string shortcut)
            {
                if (!IsShortcut)
                    return;

                SetShortcut(Command, shortcut);
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(CanRevert));
            }
        }

        private readonly IHookModel HookModel;
        private readonly ISettingModel SettingModel;
        private readonly IPropertyModel PropertyModel;
        private readonly IEditorCommandRegistry CommandRegistry;
        private SettingCategory _selectedCategory;
        private readonly TypeD.Models.Data.Project LoadedProject;
        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set { if (SetProperty(ref _filterText, value)) RefreshDisplayedCategories(); }
        }
        public bool NoResults => DisplayedCategories.Count == 0;

        public ObservableCollection<SettingCategory> Categories { get; } = new();
        public ObservableCollection<SettingCategory> DisplayedCategories { get; } = new();
        public string ShortcutError { get; private set; } = string.Empty;

        public SettingCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    RefreshDisplayedCategories();
            }
        }

        public OptionsDialogViewModel(Control element, TypeD.Models.Data.Project loadedProject) : base(element)
        {
            HookModel = ResourceModel.Get<IHookModel>();
            SettingModel = ResourceModel.Get<ISettingModel>();
            PropertyModel = ResourceModel.Get<IPropertyModel>();
            CommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            LoadedProject = loadedProject;
        }

        public void InitUI()
        {
            var hook = new OptionsHook() { Level = SettingLevel.User };
            HookModel.Shoot(hook);

            Categories.Clear();
            var categories = new Dictionary<string, SettingCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in hook.Items)
            {
                var categoryName = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category;
                if (!categories.TryGetValue(categoryName, out var category))
                {
                    category = new SettingCategory(categoryName);
                    categories.Add(categoryName, category);
                }

                category.Options.Add(new Option(item, PropertyModel, SettingModel, LoadedProject));
            }

            var shortcutCategory = new SettingCategory("Shortcuts");
            var shortcutSetting = SettingModel.GetContext<MainWindowSettingContext>()?.ShortcutOverrides;
            foreach (var command in CommandRegistry.Commands.OrderBy(command => command.Name))
                shortcutCategory.Options.Add(new Option(command, (commandToSet, shortcut) =>
                {
                    SetShortcut(commandToSet, shortcut);
                }, SettingModel, LoadedProject, shortcutSetting));
            if (shortcutCategory.Options.Count > 0)
                categories[shortcutCategory.Name] = shortcutCategory;

            foreach (var category in categories.Values.OrderBy(category => category.Name))
                Categories.Add(category);

            RefreshDisplayedCategories();
        }

        public void ShowAllCategories()
        {
            SelectedCategory = null;
        }

        public bool CaptureShortcut(EditorCommand command, string shortcut)
        {
            return SetShortcut(command, shortcut);
        }

        private bool SetShortcut(EditorCommand command, string shortcut)
        {
            var normalizedShortcut = NormalizeShortcut(shortcut);
            var conflict = CommandRegistry.Commands.FirstOrDefault(candidate =>
                candidate.Id != command.Id &&
                candidate.Scope.Equals(command.Scope, StringComparison.OrdinalIgnoreCase) &&
                NormalizeShortcut(candidate.Shortcut) == normalizedShortcut &&
                normalizedShortcut.Length > 0);

            if (conflict != null)
            {
                ShortcutError = $"Shortcut is already assigned to {conflict.Name}.";
                OnPropertyChanged(nameof(ShortcutError));
                return false;
            }

            command.Shortcut = shortcut ?? string.Empty;
            var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.User);
            var overrides = setting.ShortcutOverrides.Value;
            if (overrides == null)
            {
                overrides = new ShortcutBindingOverrides();
                setting.ShortcutOverrides.Value = overrides;
            }

            var revertValue = (string)SettingModel.GetRevertValue(setting.ShortcutOverrides, command.Id, command.DefaultShortcut);
            if (NormalizeShortcut(command.Shortcut) == NormalizeShortcut(revertValue))
                overrides.Remove(command.Id);
            else
                overrides[command.Id] = command.Shortcut;

            SettingModel.SetContext(setting, SettingLevel.User);
            if (NormalizeShortcut(command.Shortcut) == NormalizeShortcut(revertValue))
                SettingModel.ClearUserOverride(setting.ShortcutOverrides, command.Id);
            ShortcutError = string.Empty;
            OnPropertyChanged(nameof(ShortcutError));
            return true;
        }

        private void RefreshDisplayedCategories()
        {
            DisplayedCategories.Clear();
            var categories = SelectedCategory == null
                ? Categories
                : Categories.Where(category => category == SelectedCategory);

            foreach (var category in categories)
            {
                category.VisibleOptions.Clear();
                var terms = FilterText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var option in category.Options.Where(option => terms.All(term =>
                    $"{category.Name} {option.Title} {option.Description} {option.Command?.Id} {option.Value}"
                        .Contains(term, StringComparison.OrdinalIgnoreCase))))
                    category.VisibleOptions.Add(option);
                if (category.VisibleOptions.Count > 0) DisplayedCategories.Add(category);
            }
            OnPropertyChanged(nameof(NoResults));
        }

        private static string NormalizeShortcut(string shortcut)
        {
            return (shortcut ?? string.Empty)
                .Replace("Control", "Ctrl", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }
    }
}
