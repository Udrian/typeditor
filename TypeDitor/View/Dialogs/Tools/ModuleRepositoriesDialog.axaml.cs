using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TypeDitor.ViewModel.Dialogs.Tools;

namespace TypeDitor.View.Dialogs.Tools
{
    public partial class ModuleRepositoriesDialog : Window
    {
        internal ModuleRepositoriesDialogViewModel ViewModel => (ModuleRepositoriesDialogViewModel)DataContext;

        public ModuleRepositoriesDialog()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            ViewModel.Load();
        }

        private void ButtonSave_Click(object? sender, RoutedEventArgs e)
        {
            Close(ViewModel.Save());
        }
    }
}