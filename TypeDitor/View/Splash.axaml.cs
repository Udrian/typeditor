using Avalonia.Controls;
using Avalonia.Input;
using TypeD.Models.Data;
using TypeDitor.ViewModel;

namespace TypeDitor.View
{
    /// <summary>
    /// Interaction logic for Splash.axaml
    /// </summary>
    public partial class Splash : Window
    {
        SplashViewModel ViewModel { get; set; }

        public Splash()
        {
            InitializeComponent();
            ViewModel = new SplashViewModel(this);
            DataContext = ViewModel;

            RecentList.ItemsSource = ViewModel.GetRecents();
        }

        private void RecentList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is not ListBox lbRecent) return;
            if (lbRecent.SelectedItem is not Recent recent) return;
            ViewModel.OpenProjectCommand.Execute(recent);
        }
    }
}
