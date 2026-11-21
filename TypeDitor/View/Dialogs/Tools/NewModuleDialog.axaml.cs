using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDitor.ViewModel.Dialogs.Tools;

namespace TypeDitor.View.Dialogs.Tools
{
    public partial class NewModuleDialog : Window
    {
        internal NewModuleDialogViewModel ViewModel => (NewModuleDialogViewModel)DataContext;
        
        public NewModuleDialog()
        {
            InitializeComponent();
        }
        
        private async void btnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenLocation(StorageProvider);
        }

        private void AddLocalModule_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddLocalModule();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;

            Close(true);
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            await ViewModel.LoadAvailableModules();
        }
    }
}