using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>Specifies the user's choice when a content file has references.</summary>
    public enum ContentReferencesResult
    {
        /// <summary>Cancel the operation.</summary>
        Cancel,
        /// <summary>Continue without changing the referenced components.</summary>
        Ignore,
        /// <summary>Confirm the operation.</summary>
        Confirm
    }

    /// <summary>
    /// Lists the components and node paths that reference a content file about to be renamed or deleted.
    /// </summary>
    public partial class ContentReferencesDialog : Window
    {
        /// <summary>Gets the choice made in the dialog.</summary>
        public ContentReferencesResult Result { get; private set; } = ContentReferencesResult.Cancel;

        /// <summary>Creates a dialog showing references to content and the available actions.</summary>
        public ContentReferencesDialog(string message, string confirmText, IEnumerable<string> references)
        {
            InitializeComponent();
            tbMessage.Text = message;
            btnConfirm.Content = confirmText;
            lbReferences.ItemsSource = new List<string>(references);
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                    Close();
            };
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => Finish(ContentReferencesResult.Confirm);
        private void Ignore_Click(object sender, RoutedEventArgs e) => Finish(ContentReferencesResult.Ignore);
        private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(ContentReferencesResult.Cancel);

        private void Finish(ContentReferencesResult result)
        {
            Result = result;
            Close();
        }
    }
}
