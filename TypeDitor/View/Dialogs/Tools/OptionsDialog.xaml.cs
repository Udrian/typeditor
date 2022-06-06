using System.Windows;
using TypeDitor.ViewModel.Dialogs.Tools;

namespace TypeDitor.View.Dialogs.Tools
{
    /// <summary>
    /// Interaction logic for OptionsDialog.xaml
    /// </summary>
    public partial class OptionsDialog : Window
    {
        // ViewModel
        OptionsDialogViewModel OptionsDialogViewModel { get; set; }

        public OptionsDialog(TypeD.Models.Data.Project loadedProject)
        {
            InitializeComponent();

            OptionsDialogViewModel = new OptionsDialogViewModel(this, loadedProject);
            DataContext = OptionsDialogViewModel;

            OptionsDialogViewModel.InitUI(OptionList);
        }
    }
}
