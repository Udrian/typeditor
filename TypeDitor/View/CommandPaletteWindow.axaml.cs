using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.ObjectModel;
using System.Linq;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;

namespace TypeDitor.View
{
    public partial class CommandPaletteWindow : Window
    {
        private readonly IEditorCommandRegistry CommandRegistry;
        public ObservableCollection<EditorCommand> FilteredCommands { get; } = new();

        public CommandPaletteWindow(IEditorCommandRegistry commandRegistry)
        {
            InitializeComponent();
            CommandRegistry = commandRegistry;
            DataContext = this;
            Opened += (_, _) =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            };
            RefreshCommands();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshCommands();
        }

        private void RefreshCommands()
        {
            var search = SearchBox?.Text?.Trim();
            var commands = CommandRegistry.Commands
                .Where(command => command.CanExecute())
                .Where(command => string.IsNullOrEmpty(search) ||
                    command.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    command.Description.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    command.Id.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    command.Shortcut.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(command => command.Name)
                .ToList();

            FilteredCommands.Clear();
            foreach (var command in commands)
                FilteredCommands.Add(command);

            if (CommandList != null)
                CommandList.SelectedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        }

        private void ExecuteCommand(EditorCommand command)
        {
            CommandRegistry.Execute(command.Id);
            Close();
        }

        private void Command_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: EditorCommand command })
                ExecuteCommand(command);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (CommandList.SelectedItem is EditorCommand command)
                    ExecuteCommand(command);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && CommandList.Items.Count > 0)
            {
                CommandList.SelectedIndex = (CommandList.SelectedIndex + 1) % CommandList.Items.Count;
                CommandList.ScrollIntoView(CommandList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && CommandList.Items.Count > 0)
            {
                CommandList.SelectedIndex = (CommandList.SelectedIndex - 1 + CommandList.Items.Count) % CommandList.Items.Count;
                CommandList.ScrollIntoView(CommandList.SelectedItem);
                e.Handled = true;
            }
        }
    }
}
