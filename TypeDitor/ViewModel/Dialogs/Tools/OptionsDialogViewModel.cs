using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

            public SettingCategory(string name)
            {
                Name = name;
            }
        }

        public sealed class Option : ViewModelBase
        {
            private readonly Action<EditorCommand, string> SetShortcut;
            private readonly SettingItem Item;

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
                        return !string.Equals(Command.Shortcut, Command.DefaultShortcut, StringComparison.OrdinalIgnoreCase);

                    var current = Item.Setting.GetValue();
                    var defaultValue = Item.Setting.GetDefaultValue();
                    return !Equals(current, defaultValue);
                }
            }

            public Option(SettingItem item, IPropertyModel propertyModel)
            {
                Item = item;
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

            public Option(EditorCommand command, Action<EditorCommand, string> setShortcut)
            {
                Command = command;
                SetShortcut = setShortcut;
            }

            public void Revert()
            {
                if (!CanRevert)
                    return;

                if (IsShortcut)
                    UpdateShortcut(Command.DefaultShortcut);
                else
                    SetValue(Item.Setting.GetDefaultValue());

                OnPropertyChanged(nameof(Value));
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

                category.Options.Add(new Option(item, PropertyModel));
            }

            var shortcutCategory = new SettingCategory("Shortcuts");
            foreach (var command in CommandRegistry.Commands.OrderBy(command => command.Name))
                shortcutCategory.Options.Add(new Option(command, (commandToSet, shortcut) =>
                {
                    SetShortcut(commandToSet, shortcut);
                }));
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

            if (NormalizeShortcut(command.Shortcut) == NormalizeShortcut(command.DefaultShortcut))
                overrides.Remove(command.Id);
            else
                overrides[command.Id] = command.Shortcut;

            SettingModel.SetContext(setting, SettingLevel.User);
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
                DisplayedCategories.Add(category);
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
