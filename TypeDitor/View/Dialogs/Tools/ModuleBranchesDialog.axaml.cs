using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TypeDitor.ViewModel.Dialogs.Tools;

namespace TypeDitor.View.Dialogs.Tools
{
    public partial class ModuleBranchesDialog : Window
    {
        internal ModuleBranchesDialogViewModel ViewModel => (ModuleBranchesDialogViewModel)DataContext;
        
        public ModuleBranchesDialog()
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