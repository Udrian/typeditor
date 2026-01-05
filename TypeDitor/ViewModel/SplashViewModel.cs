using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Commands;

namespace TypeDitor.ViewModel
{
    internal partial class SplashViewModel : ViewModelBase
    {
        // Providers
        private IRecentProvider RecentProvider { get; set; }

        // Commands
        [ObservableProperty]
        private ImportProjectCommand _importProjectCommand;
        [ObservableProperty]
        private OpenProjectCommand _openProjectCommand;
        [ObservableProperty]
        private NewProjectCommand _newProjectCommand;

        // Properties
        [ObservableProperty]
        private ObservableCollection<Recent> _recents;

        // Constructors
        public SplashViewModel() : base()
        {
            RecentProvider = ResourceModel.Get<IRecentProvider>();

            ImportProjectCommand = new ImportProjectCommand();
            OpenProjectCommand = new OpenProjectCommand();
            NewProjectCommand = new NewProjectCommand();

            Recents = GetRecents();
        }

        // Functions
        private ObservableCollection<Recent> GetRecents()
        {
            return new ObservableCollection<Recent>(RecentProvider.Get());
        }
    }
}
