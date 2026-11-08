using Avalonia.Controls;
using Avalonia.Controls.Templates;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.ViewModel;
using TypeOEngine.Typedeaf.Core.Common;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class AddCustomPropertyViewModel : ViewModelBase
    {
        /// <summary>A property value type the user can pick in the dialog.</summary>
        public class TypeOption
        {
            public string Name { get; set; }
            public IPropertyDefinition Definition { get; set; }
            public Type Type => Definition.Type;
            public override string ToString() => Name;
        }

        // Data
        public string PropertyName { get; set; } = "";
        public string Description { get; set; } = "";
        public List<TypeOption> Types { get; }
        public string Group { get; set; } = "General";
        public List<string> AccessOptions { get; } = new List<string> { "Public", "Private", "Protected" };
        public string SelectedAccess { get; set; } = "Public";

        private TypeOption _selectedType;
        public TypeOption SelectedType
        {
            get => _selectedType;
            set
            {
                if (ReferenceEquals(_selectedType, value))
                    return;
                _selectedType = value;
                OnPropertyChanged();
                CreateDefaultValueNode();
            }
        }

        private PropertyNode _defaultValueNode;
        /// <summary>Gets the node editing the default value, created by the selected type's property definition.</summary>
        public PropertyNode DefaultValueNode
        {
            get => _defaultValueNode;
            private set
            {
                _defaultValueNode = value;
                OnPropertyChanged();
            }
        }

        private IDataTemplate _defaultValueTemplate;
        /// <summary>Gets the template used to edit the default value, the same one the properties panel uses.</summary>
        public IDataTemplate DefaultValueTemplate
        {
            get => _defaultValueTemplate;
            private set
            {
                _defaultValueTemplate = value;
                OnPropertyChanged();
            }
        }

        private string _error;
        public string Error
        {
            get => _error;
            set
            {
                _error = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }
        public bool HasError => !string.IsNullOrEmpty(Error);

        private HashSet<string> TakenNames { get; }
        private Dictionary<Type, IDataTemplate> TemplateCache { get; } = new Dictionary<Type, IDataTemplate>();
        // Stand-in owner for the default value property while it is edited in the dialog.
        private Component DefaultValueOwner { get; } = new Component();

        // Constructors
        public AddCustomPropertyViewModel(Control element, IEnumerable<IPropertyDefinition> definitions, IEnumerable<string> takenNames) : base(element)
        {
            // Only concrete types stored by value can be created and edited through the dialog.
            Types = definitions
                .Where(definition => definition.ValueType == definition.Type && !definition.Type.IsAbstract)
                .Select(definition => new TypeOption { Name = GetTypeDisplayName(definition.Type), Definition = definition })
                .OrderBy(option => option.Name)
                .ToList();
            TakenNames = new HashSet<string>(takenNames ?? Enumerable.Empty<string>());
            SelectedType = Types.FirstOrDefault();
        }

        // Functions
        /// <summary>Validates the dialog input, setting <see cref="Error"/>. Returns whether the input is valid.</summary>
        public bool Validate()
        {
            var name = PropertyName?.Trim() ?? "";
            if (name.Length == 0)
            {
                Error = "Enter a property name.";
                return false;
            }
            if (CSharpIdentifier.Sanitize(name) != name)
            {
                Error = $"'{name}' is not a valid C# property name.";
                return false;
            }
            if (TakenNames.Contains(name))
            {
                Error = $"A property named '{name}' already exists on the component.";
                return false;
            }
            if (SelectedType?.Type == null || DefaultValueNode == null)
            {
                Error = "Select a value type.";
                return false;
            }
            if (DefaultValueNode.HasValidationError)
            {
                Error = DefaultValueNode.ValidationError;
                return false;
            }

            Error = null;
            return true;
        }

        /// <summary>Creates the custom property from the validated dialog input.</summary>
        public CustomProperty Build()
        {
            return new CustomProperty
            {
                Name = PropertyName.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                TypeName = SelectedType.Type.FullName,
                DefaultValue = DefaultValueNode.Property.Value,
                Group = Group,
                Access = Enum.TryParse<CustomPropertyAccess>(SelectedAccess, out var access)
                    ? access
                    : CustomPropertyAccess.Public
            };
        }

        private void CreateDefaultValueNode()
        {
            if (SelectedType == null)
            {
                DefaultValueNode = null;
                DefaultValueTemplate = null;
                return;
            }

            var property = new Property
            {
                Name = "DefaultValue",
                Type = SelectedType.Type,
                Value = GetFallbackDefault(SelectedType.Type)
            };
            Action<PropertyNode, object> valueChanged = (node, value) =>
            {
                node.Property.Value = value;
                node.RefreshValue();
            };

            if (!TemplateCache.TryGetValue(SelectedType.Type, out var template))
            {
                template = SelectedType.Definition.CreateTemplate();
                TemplateCache[SelectedType.Type] = template;
            }

            // Template first so the new node is never shown through the previous type's template.
            DefaultValueTemplate = template;
            DefaultValueNode = SelectedType.Definition.CreateNode(DefaultValueOwner, property, valueChanged)
                ?? new PropertyNode(DefaultValueOwner, property, valueChanged);
        }

        private static string GetTypeDisplayName(Type type)
        {
            if (type == typeof(bool)) return "Boolean";
            if (type == typeof(int)) return "Integer";
            if (type == typeof(double)) return "Double";
            if (type == typeof(string)) return "String";
            return type.Name;
        }

        private static object GetFallbackDefault(Type type)
        {
            if (type == typeof(string)) return "";
            if (type == typeof(Vec2)) return new Vec2(0, 0);
            if (type == typeof(Color)) return new Color(255, 0, 0, 0);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
