using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;
using TypeD.Models.Data;
using TypeDitor.ViewModel;

namespace TypeDitor.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Properties
        private bool CanClose { get; set; } = false;

        // ViewModel
        MainWindowViewModel ViewModel { get; set; }

        // Constructors
        public MainWindow(Project loadedProject)
        {
            InitializeComponent();

            ViewModel = new MainWindowViewModel(this, loadedProject);
            
            DataContext = ViewModel;

            ViewModel.InitUI(this);
            WarningIssuesPopup.DataContext = ViewModel;
            ErrorIssuesPopup.DataContext = ViewModel;
        }

        public Menu TopMenu { get { return _TopMenu; } }

        private void ModulesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenModulesWindow();
        }

        private async void Window_Closing(object sender, WindowClosingEventArgs e)
        {
            if (CanClose) return;

            e.Cancel = true;
            var result = await ViewModel.OnClose();
            if(!result)
            {
                CanClose = true;
                this.Close();
            }
        }

        private void OptionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenOptionsWindow();
        }

        private void WarningBadge_Click(object sender, RoutedEventArgs e)
        {
            ErrorIssuesPopup.IsOpen = false;
            WarningIssuesPopup.IsOpen = !WarningIssuesPopup.IsOpen;
        }

        private void ErrorBadge_Click(object sender, RoutedEventArgs e)
        {
            WarningIssuesPopup.IsOpen = false;
            ErrorIssuesPopup.IsOpen = !ErrorIssuesPopup.IsOpen;
        }

        private async void OpenBuildIssueInExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Control { DataContext: BuildIssueDisplay issue })
                return;

            try
            {
                await ViewModel.OpenBuildIssueInExternalEditor(issue);
            }
            catch (Exception exception)
            {
                var box = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "External editor error",
                    $"Could not open the issue location:{Environment.NewLine}{exception.Message}",
                    MsBox.Avalonia.Enums.ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await box.ShowWindowDialogAsync(this);
            }
        }
    }
}
