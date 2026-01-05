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
        private SplashViewModel ViewModel { get { return DataContext as SplashViewModel; } }

        public Splash()
        {
            InitializeComponent();
            DataContext = new SplashViewModel();
        }

        private void RecentList_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (RecentList.SelectedItem is not Recent recent) return;
            ViewModel.OpenProjectCommand.Execute(recent);
        }
    }
}
