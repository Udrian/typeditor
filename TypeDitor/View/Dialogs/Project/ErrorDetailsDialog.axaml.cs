using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>Error dialog with an explanation and an expandable log of what happened before the error.</summary>
    public partial class ErrorDetailsDialog : Window
    {
        private readonly string _details;

        public ErrorDetailsDialog()
        {
            InitializeComponent();
            _details = "";
        }

        public ErrorDetailsDialog(string title, string summary, string hint, string details) : this()
        {
            _details = details;
            TitleText.Text = title;
            SummaryText.Text = summary;
            HintText.Text = hint;
            HintText.IsVisible = !string.IsNullOrEmpty(hint);
            DetailsBox.Text = details;
            DetailsButton.IsVisible = !string.IsNullOrWhiteSpace(details);
        }

        private void OnToggleDetails(object sender, RoutedEventArgs e)
        {
            var show = !DetailsBox.IsVisible;
            DetailsBox.IsVisible = show;
            CopyButton.IsVisible = show;
            DetailsButton.Content = show ? "Hide details" : "Show details";
            if (show)
            {
                DetailsBox.CaretIndex = DetailsBox.Text?.Length ?? 0;
            }
        }

        private async void OnCopy(object sender, RoutedEventArgs e)
        {
            if (Clipboard != null)
            {
                await Clipboard.SetTextAsync(_details);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
