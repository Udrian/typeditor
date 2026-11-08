using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeD.Models.Data;
using TypeDitor.ViewModel.Dialogs.Project;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for AddCustomPropertyDialog.xaml
    /// </summary>
    public partial class AddCustomPropertyDialog : Window
    {
        // ViewModel
        internal AddCustomPropertyViewModel ViewModel { get; set; }

        // Constructors
        /// <summary>Creates a dialog for adding a custom property to a component.</summary>
        /// <param name="definitions">The property definitions the user can pick a value type from.</param>
        /// <param name="takenNames">Property names that are already in use on the component.</param>
        public AddCustomPropertyDialog(IEnumerable<IPropertyDefinition> definitions, IEnumerable<string> takenNames)
        {
            InitializeComponent();
            DataContext = ViewModel = new AddCustomPropertyViewModel(this, definitions, takenNames);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Validate())
                Close(true);
        }
    }
}
