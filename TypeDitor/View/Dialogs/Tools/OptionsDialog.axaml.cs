using Avalonia.Controls;
using System;
using Avalonia.Input;
using Avalonia.Interactivity;
using TypeD.Models.Data;
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

            OptionsDialogViewModel.InitUI();
        }

        private void AllSettings_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialogViewModel.ShowAllCategories();
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: OptionsDialogViewModel.Option option })
                option.Revert();
        }

        private async void SetProjectDefault_Click(object sender, RoutedEventArgs e) =>
            await ChangeProjectDefault(sender, false);

        private async void RemoveProjectDefault_Click(object sender, RoutedEventArgs e) =>
            await ChangeProjectDefault(sender, true);

        private async System.Threading.Tasks.Task ChangeProjectDefault(object sender, bool remove)
        {
            if (sender is not MenuItem { DataContext: OptionsDialogViewModel.Option option })
                return;
            try
            {
                await option.SetProjectDefaultAsync(remove);
            }
            catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException or InvalidOperationException or NotSupportedException)
            {
                TypeD.ViewModel.ViewModelBase.ResourceModel.Get<TypeD.Models.Interfaces.ILogModel>()
                    .Log($"Could not save project default: {exception}");
                await MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard("Project default", exception.Message,
                    MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowWindowDialogAsync(this);
            }
        }

        private void ValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox { DataContext: OptionsDialogViewModel.Option { IsShortcut: true, Command: not null } option })
                return;

            if (e.Key is Key.Back or Key.Delete)
            {
                option.UpdateShortcut(string.Empty);
                e.Handled = true;
                return;
            }

            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
                Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return;

            var shortcut = EditorShortcut.FromKey(
                e.Key.ToString(),
                e.KeyModifiers.HasFlag(KeyModifiers.Control),
                e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                e.KeyModifiers.HasFlag(KeyModifiers.Alt),
                e.KeyModifiers.HasFlag(KeyModifiers.Meta));
            option.UpdateShortcut(shortcut);
            e.Handled = true;
        }
    }
}
