using System.Collections.Generic;
using System.Windows;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Commands;

namespace TypeDitor.ViewModel
{
    internal class SplashViewModel : ViewModelBase 
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }

        // Commands
        public ImportProjectCommand ImportProjectCommand { get; set; }
        public OpenProjectCommand OpenProjectCommand { get; set; }
        public NewProjectCommand NewProjectCommand { get; set; }

        // Constructors
        public SplashViewModel(FrameworkElement element) : base(element)
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();

            ImportProjectCommand = new ImportProjectCommand(element);
            OpenProjectCommand = new OpenProjectCommand(element);
            NewProjectCommand = new NewProjectCommand(element);
        }

        // Functions
        public IEnumerable<Recent> GetRecents()
        {
            return RecentProvider.Get();
        }
    }
}
