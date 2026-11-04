using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeDitor.Models;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Crash dialog shown when the game project or a loaded module crashes, with an expandable detail view that
    /// includes the game log output, and buttons to exit the editor or reload the game and modules within the editor.
    /// </summary>
    public partial class GameCrashDialog : Window
    {
        private readonly CrashReport _report;
        private readonly ICrashModel _crashModel;
        private bool _reloading;

        public GameCrashDialog()
        {
            InitializeComponent();
        }

        public GameCrashDialog(CrashReport report, ICrashModel crashModel, bool canReload) : this()
        {
            _report = report;
            _crashModel = crashModel;

            var crashed = report.GameCrashed && report.CrashedModules.Count > 0
                ? $"The game and the {ModuleList(report)} crashed"
                : report.GameCrashed
                    ? "The game crashed"
                    : $"The {ModuleList(report)} crashed";

            TitleText.Text = report.GameCrashed ? "The game crashed" : "A module crashed";
            SummaryText.Text = $"{crashed} inside the editor.{Environment.NewLine}{Environment.NewLine}" +
                $"{report.Exception.GetType().Name}: {report.Exception.Message}";
            HintText.Text = canReload
                ? "The game has been stopped. Show the details for the full error and the game log output. " +
                  "Reload unloads the game" + (report.CrashedModules.Count > 0 ? " and the crashed modules" : "") +
                  " and starts them again inside the editor."
                : "The editor can't recover from this crash and will close. Show the details for the full error and the game log output.";
            ReloadButton.IsVisible = canReload;
            ExitButton.IsDefault = !canReload;

            DetailsBox.Text = BuildDetails(report);
            Closing += (_, e) => e.Cancel = _reloading;
        }

        private static string ModuleList(CrashReport report)
        {
            return report.CrashedModules.Count == 1
                ? $"module {report.CrashedModules[0]}"
                : $"modules {string.Join(", ", report.CrashedModules)}";
        }

        private static string BuildDetails(CrashReport report)
        {
            var details = new StringBuilder();
            details.AppendLine($"Project: {report.Project.ProjectName}");
            if (report.GameCrashed)
                details.AppendLine("Crashed: Game project");
            foreach (var module in report.CrashedModules)
                details.AppendLine($"Crashed: Module {module}");
            details.AppendLine();
            details.AppendLine("Exception:");
            details.AppendLine(report.Exception.ToString());
            details.AppendLine();
            details.AppendLine("Game log output:");
            details.AppendLine(string.IsNullOrEmpty(report.LogOutput) ? "<no log output captured>" : report.LogOutput);
            return details.ToString();
        }

        private void OnToggleDetails(object sender, RoutedEventArgs e)
        {
            var show = !DetailsBox.IsVisible;
            DetailsBox.IsVisible = show;
            CopyButton.IsVisible = show;
            DetailsButton.Content = show ? "Hide details" : "Show details";
            if (show)
            {
                DetailsBox.CaretIndex = DetailsBox.Text?.Length ?? 0;
            }
        }

        private async void OnCopy(object sender, RoutedEventArgs e)
        {
            if (Clipboard != null)
            {
                await Clipboard.SetTextAsync(DetailsBox.Text ?? "");
            }
        }

        private void OnExit(object sender, RoutedEventArgs e)
        {
            Close();
            CrashHandler.Exit();
        }

        private async void OnReload(object sender, RoutedEventArgs e)
        {
            SetReloading(true, "Reloading the game...");
            var reloaded = await _crashModel.Reload(_report, step => Dispatcher.UIThread.Post(() => StatusText.Text = $"{step}..."));
            SetReloading(false, null);

            if (reloaded)
            {
                Close();
                return;
            }

            StatusPanel.IsVisible = true;
            StatusProgress.IsVisible = false;
            StatusText.Text = "The game could not be reloaded. Fix the error in the project and try again, " +
                "check the build errors and the Output panel for more information.";
            ReloadButton.Content = "Try again";
        }

        private void SetReloading(bool reloading, string status)
        {
            _reloading = reloading;
            StatusPanel.IsVisible = reloading;
            StatusProgress.IsVisible = reloading;
            StatusText.Text = status;
            ReloadButton.IsEnabled = !reloading;
            ExitButton.IsEnabled = !reloading;
        }
    }
}
