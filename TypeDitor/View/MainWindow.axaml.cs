using Avalonia.Controls;
using Avalonia.Interactivity;
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
    }
}
