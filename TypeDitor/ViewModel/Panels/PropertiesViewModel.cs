#nullable enable annotations

using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using System.Collections.ObjectModel;
using System.Globalization;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDCore.Commands.Data;
using TypeDitor.View.Panels;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using TypeD.Models.Data.SettingContexts;

namespace TypeDitor.ViewModel.Panels
{
    internal sealed class PropertyGroup : ViewModelBase
    {
        public string Name { get; }
        public IReadOnlyList<PropertyNode> Properties { get; }
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Indicator));
            }
        }
        public string Indicator => IsExpanded ? "-" : "+";

        public PropertyGroup(string name, IEnumerable<PropertyNode> properties)
        {
            Name = name;
            Properties = properties.ToList();
        }
    }

    /// <summary>Converts property values to templates selected by their runtime type.</summary>
    public class TypeToDataTemplateConverter : IValueConverter
    {
        private Dictionary<Type, IDataTemplate> TemplateCache { get; } = new Dictionary<Type, IDataTemplate>();

        /// <summary>
        /// Creates the template for a property type, or returns null if the type has no definition.
        /// </summary>
        public Func<Type, IDataTemplate?>? TemplateResolver { get; set; }

        /// <summary>Gets or sets the fallback template used when no type-specific template is found.</summary>
        public IDataTemplate? DefaultTemplate { get; set; }

        /// <summary>Returns the template for the value's type, or the default template if none is registered.</summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            Type type = null;
            if (value is PropertyNode node)
            {
                type = node.Type ?? node.Value?.GetType();
            }

            if (type != null)
            {
                if (!TemplateCache.TryGetValue(type, out var template))
                {
                    template = TemplateResolver?.Invoke(type);
                    if (template != null)
                        TemplateCache[type] = template;
                }
                if (template != null)
                    return template;
            }

            return DefaultTemplate;
        }

        /// <summary>Converts a template value back to its source representation.</summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }

    internal partial class PropertiesViewModel : ViewModelBase
    {
        // Models
        public IHookModel HookModel { get; set; }
        public IEditorCommandRegistry EditorCommandRegistry { get; set; }
        public IPropertyModel PropertyModel { get; set; }

        // Properties
        private Project Project { get; set; }
        private Component Component { get; set; }
        public ObservableCollection<PropertyNode> PropertyNodes { get; set; }
        public ObservableCollection<PropertyGroup> PropertyGroups { get; } = new();
        public TypeToDataTemplateConverter Converter { get; set; }
        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText == value)
                    return;
                _filterText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFilterActive));
                RefreshGroups();
            }
        }
        public bool IsFilterActive => !string.IsNullOrWhiteSpace(FilterText);
        public int HiddenPropertyCount => PropertyNodes.Count - PropertyGroups.Sum(group => group.Properties.Count);
        public string FilterStatus => $"{HiddenPropertyCount} {(HiddenPropertyCount == 1 ? "property" : "properties")} hidden";
        public ICommand ClearFilterCommand { get; }
        private readonly ISettingModel _settingModel;
        private readonly List<string> _customGroupOrder;
        private readonly Dictionary<string, List<string>> _customPropertyOrder;
        private readonly Dictionary<string, bool> _expandedGroups = new(StringComparer.Ordinal);
        private bool _sortAlphabetically;
        public bool SortAlphabetically
        {
            get => _sortAlphabetically;
            set
            {
                if (_sortAlphabetically == value)
                    return;
                _sortAlphabetically = value;
                OnPropertyChanged();
                RefreshGroups();
                SaveGroupOrder();
            }
        }

        private PropertiesPanel PropertiesPanel { get; set; }

        // Constructors
        public PropertiesViewModel(PropertiesPanel element, Project project) : base(element)
        {
            PropertiesPanel = element;
            Project = project;

            HookModel = ResourceModel.Get<IHookModel>();
            EditorCommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            PropertyModel = ResourceModel.Get<IPropertyModel>();
            _settingModel = ResourceModel.Get<ISettingModel>();
            var settings = _settingModel.GetContext<MainWindowSettingContext>(SettingLevel.User);
            _sortAlphabetically = settings.SortPropertyGroupsAlphabetically.Value;
            var savedOrder = settings.PropertyGroupOrder.Value;
            try
            {
                _customGroupOrder = string.IsNullOrEmpty(savedOrder)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(savedOrder) ?? new List<string>();
            }
            catch (JsonException exception)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not load property group order: {exception}");
                _customGroupOrder = new List<string>();
            }
            try
            {
                _customPropertyOrder = string.IsNullOrEmpty(settings.PropertyOrder.Value)
                    ? new Dictionary<string, List<string>>()
                    : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(settings.PropertyOrder.Value)
                        ?? new Dictionary<string, List<string>>();
            }
            catch (JsonException exception)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not load property order: {exception}");
                _customPropertyOrder = new Dictionary<string, List<string>>();
            }

            Converter = new TypeToDataTemplateConverter
            {
                TemplateResolver = type => PropertyModel.Get(type)?.CreateTemplate(),
                // Fallback: edit unknown types as strings.
                DefaultTemplate = PropertyModel.Get(typeof(string))?.CreateTemplate()
            };
            PropertiesPanel.Resources["TypeToDataTemplateConverter"] = Converter;

            PropertyNodes = new ObservableCollection<PropertyNode>();
            ClearFilterCommand = new RelayCommand(() => FilterText = string.Empty);

            HookModel.AddHook<ComponentFocusHook>((hook) =>
            {
                if(Component == hook.Component)
                    return;
                Component = hook.Component;
                BuildPropertyNodes();
            });

            HookModel.AddHook<ComponentPropertiesChangedHook>((hook) =>
            {
                if (Component == null || !ReferenceEquals(Component, hook.Component))
                    return;
                BuildPropertyNodes();
            });

            HookModel.AddHook<PropertyChangedHook>((hook) =>
            {
                if (!ReferenceEquals(Component, hook.Component) || hook.Property == null)
                    return;

                var propertyNode = PropertyNodes.FirstOrDefault(node => ReferenceEquals(node.Property, hook.Property))
                    ?? PropertyNodes.FirstOrDefault(node => node.Property.Name == hook.Property.Name);
                propertyNode?.RefreshValue();
                if (IsFilterActive)
                    RefreshGroups();
            });

            HookModel.AddHook<CloseComponentHook>((hook) =>
            {
                Component = null;
                PropertyNodes.Clear();
                RefreshGroups();
            });
        }

        private void BuildPropertyNodes()
        {
            PropertyNodes.Clear();
            if (Component == null)
            {
                RefreshGroups();
                return;
            }
            foreach(var property in Component.Properties)
            {
                Action<PropertyNode, object> valueChanged = (node, value) =>
                {
                    var executed = EditorCommandRegistry.Execute("component.change-property", new ChangePropertyCommandData
                    {
                        Project = Project,
                        Property = node.Property,
                        Value = value
                    });
                    if (!executed)
                        throw new InvalidOperationException($"Could not execute the change-property command for '{node.Property.Name}'.");
                };
                var definition = PropertyModel.Get(property.Type);
                PropertyNodes.Add(definition?.CreateNode(Component, property, valueChanged) ?? new PropertyNode(Component, property, valueChanged));
            }
            RefreshGroups();
        }

        private void RefreshGroups()
        {
            foreach (var group in PropertyGroups)
                _expandedGroups[group.Name] = group.IsExpanded;
            foreach (var name in PropertyNodes.Select(node => node.Group).Distinct())
            {
                if (!_customGroupOrder.Contains(name))
                    _customGroupOrder.Add(name);
            }
            foreach (var group in PropertyNodes.GroupBy(node => node.Group))
            {
                if (!_customPropertyOrder.TryGetValue(group.Key, out var order) || order == null)
                    _customPropertyOrder[group.Key] = order = new List<string>();
                foreach (var node in group)
                {
                    if (!order.Contains(node.Name))
                        order.Add(node.Name);
                }
            }

            var visibleNodes = IsFilterActive
                ? PropertyNodes.Where(node => node.MatchesFilter(FilterText))
                : PropertyNodes;

            PropertyGroups.Clear();
            var groups = visibleNodes.GroupBy(node => node.Group);
            var orderedGroups = SortAlphabetically
                ? groups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                : groups.OrderBy(group => _customGroupOrder.IndexOf(group.Key));
            foreach (var group in orderedGroups)
                PropertyGroups.Add(new PropertyGroup(group.Key, SortAlphabetically
                    ? group.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                    : group.OrderBy(node => _customPropertyOrder[group.Key].IndexOf(node.Name)))
                {
                    IsExpanded = !_expandedGroups.TryGetValue(group.Key, out var expanded) || expanded
                });

            OnPropertyChanged(nameof(HiddenPropertyCount));
            OnPropertyChanged(nameof(FilterStatus));
        }

        public void MoveGroup(string source, string target, bool after)
        {
            if (source == target)
                return;

            var displayedOrder = PropertyGroups.Select(group => group.Name).ToList();
            if (!displayedOrder.Contains(source) || !displayedOrder.Contains(target))
                return;

            displayedOrder.Remove(source);
            displayedOrder.Insert(displayedOrder.IndexOf(target) + (after ? 1 : 0), source);

            // Replace only the visible slots so filtering doesn't discard or shuffle hidden groups.
            ReplaceVisibleOrder(_customGroupOrder, displayedOrder);

            _sortAlphabetically = false;
            OnPropertyChanged(nameof(SortAlphabetically));
            RefreshGroups();
            SaveGroupOrder();
        }

        public void MoveProperty(PropertyNode source, PropertyNode target, bool after)
        {
            if (ReferenceEquals(source, target) || source.Group != target.Group)
                return;

            var group = PropertyGroups.FirstOrDefault(group => group.Name == source.Group);
            if (group == null || !group.Properties.Contains(source) || !group.Properties.Contains(target))
                return;

            var order = group.Properties.Select(node => node.Name).ToList();
            order.Remove(source.Name);
            order.Insert(order.IndexOf(target.Name) + (after ? 1 : 0), source.Name);
            if (order.SequenceEqual(group.Properties.Select(node => node.Name)))
                return;

            // Keep the displayed group order when leaving A-Z mode, without losing filtered-out entries.
            if (SortAlphabetically)
                ReplaceVisibleOrder(_customGroupOrder, PropertyGroups.Select(item => item.Name).ToList());
            ReplaceVisibleOrder(_customPropertyOrder[source.Group], order);

            _sortAlphabetically = false;
            OnPropertyChanged(nameof(SortAlphabetically));
            RefreshGroups();
            SaveGroupOrder();
        }

        private static void ReplaceVisibleOrder(List<string> customOrder, List<string> displayedOrder)
        {
            var visibleNames = displayedOrder.ToHashSet(StringComparer.Ordinal);
            var next = 0;
            for (var index = 0; index < customOrder.Count; index++)
            {
                if (visibleNames.Contains(customOrder[index]))
                    customOrder[index] = displayedOrder[next++];
            }
        }

        private void SaveGroupOrder()
        {
            var settings = _settingModel.GetContext<MainWindowSettingContext>(SettingLevel.User);
            settings.PropertyGroupOrder.Value = JsonSerializer.Serialize(_customGroupOrder);
            settings.PropertyOrder.Value = JsonSerializer.Serialize(_customPropertyOrder);
            settings.SortPropertyGroupsAlphabetically.Value = SortAlphabetically;
            _settingModel.SetContext(settings, SettingLevel.User);
        }
    }
}
