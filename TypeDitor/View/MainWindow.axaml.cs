using Avalonia.Controls;
using Avalonia.Input;
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
            AddHandler(KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel);
            WarningIssuesPopup.DataContext = ViewModel;
            ErrorIssuesPopup.DataContext = ViewModel;
        }

        public Menu TopMenu { get { return _TopMenu; } }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var shortcut = TypeD.Models.Data.EditorShortcut.FromKey(
                e.Key.ToString(),
                e.KeyModifiers.HasFlag(KeyModifiers.Control),
                e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                e.KeyModifiers.HasFlag(KeyModifiers.Alt),
                e.KeyModifiers.HasFlag(KeyModifiers.Meta));

            if (ViewModel.TryExecuteShortcut(shortcut, "global"))
                e.Handled = true;
        }

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

        private void CommandPaletteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ExecuteCommand("command-palette.open");
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
