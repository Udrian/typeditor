using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TypeDater.Core;

namespace TypeDater.View
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            BrowseButton.Click += async (_, _) => await Browse();
            InstallButton.Click += (_, _) => StartInstall();
            CancelButton.Click += (_, _) => Close();
            UpdateYesButton.Click += (_, _) => AcceptUpdate();
            UpdateSkipButton.Click += (_, _) => SkipUpdate();
            UpdateNoButton.Click += (_, _) => Close();
            CloseButton.Click += (_, _) => Close();

            if (AppState.Mode == DaterMode.Install)
            {
                InstallPathBox.Text = AppState.DefaultInstallDirectory;
                ShowPanel(ConfigPanel);
            }
            else if (AppState.UpdateAccepted)
            {
                StartUpdate();
            }
            else
            {
                var installed = AppState.Manifest.InstalledVersion ?? "unknown";
                PromptText.Text = $"TypeDitor {AppState.LatestVersion} is available (installed version: {installed}). Do you want to update now? TypeDitor will restart after the update.";
                ShowPanel(PromptPanel);
            }
        }

        private void ShowPanel(Control panel)
        {
            ConfigPanel.IsVisible = panel == ConfigPanel;
            PromptPanel.IsVisible = panel == PromptPanel;
            ProgressPanel.IsVisible = panel == ProgressPanel;
        }

        private async Task Browse()
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the TypeDitor install folder",
                AllowMultiple = false
            });
            var folder = folders.FirstOrDefault()?.Path.LocalPath;
            if (folder == null) return;
            if (!Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)).Equals("TypeDitor", StringComparison.OrdinalIgnoreCase))
            {
                folder = Path.Combine(folder, "TypeDitor");
            }
            InstallPathBox.Text = folder;
        }

        private void StartInstall()
        {
            var installDirectory = (InstallPathBox.Text ?? "").Trim();
            if (installDirectory.Length == 0 || !Path.IsPathRooted(installDirectory))
            {
                PromptInvalidPath();
                return;
            }
            var options = new InstallOptions
            {
                InstallDirectory = installDirectory,
                StartMenuShortcut = StartMenuCheck.IsChecked == true,
                DesktopShortcut = DesktopCheck.IsChecked == true
            };
            ShowPanel(ProgressPanel);
            _ = RunPipeline(pipeline => pipeline.RunInstall(options, AppState.LatestVersion), installDirectory);
        }

        private void PromptInvalidPath()
        {
            InstallPathBox.Focus();
            InstallPathBox.SelectAll();
        }

        private void AcceptUpdate()
        {
            // Installs in protected folders (e.g. Program Files) need an elevated restart before files can be replaced.
            if (OperatingSystem.IsWindows() && !Program.IsAdministrator() && !CanWrite(AppState.ExeDirectory))
            {
                var args = new List<string> { "--accepted", AppState.LatestVersion!.ToString() };
                if (AppState.TypeDitorPid != null)
                {
                    args.Add("--pid");
                    args.Add(AppState.TypeDitorPid.Value.ToString());
                }
                Program.RelaunchElevated(AppState.Logger, args);
                Close();
                return;
            }
            StartUpdate();
        }

        private void StartUpdate()
        {
            ShowPanel(ProgressPanel);
            _ = RunPipeline(pipeline => pipeline.RunUpdate(AppState.ExeDirectory, AppState.LatestVersion!, AppState.TypeDitorPid), AppState.ExeDirectory);
        }

        private void SkipUpdate()
        {
            try
            {
                AppState.Manifest.SkippedVersion = AppState.LatestVersion!.ToString();
                AppState.Manifest.Save(AppState.ExeDirectory);
                AppState.Logger.Log($"User skipped version {AppState.LatestVersion}");
            }
            catch (Exception e)
            {
                AppState.Logger.Log(e, "saving skipped version");
            }
            Close();
        }

        private async Task RunPipeline(Func<InstallPipeline, Task> work, string launchDirectory)
        {
            var pipeline = new InstallPipeline(AppState.Logger)
            {
                OnStep = (step, total, title) => Dispatcher.UIThread.Post(() =>
                {
                    StepTitle.Text = title;
                    StepProgress.Maximum = total;
                    StepProgress.Value = step;
                    DetailText.Text = "";
                }),
                OnDetail = line => Dispatcher.UIThread.Post(() =>
                {
                    DetailText.Text = line;
                    AppendLog(line);
                })
            };

            try
            {
                await Task.Run(() => work(pipeline));
                pipeline.LaunchTypeDitor(launchDirectory);
                Close();
            }
            catch (Exception e)
            {
                AppState.Logger.Log(e, "pipeline");
                StepTitle.Text = "Something went wrong";
                AppendLog(e is StepFailedException ? e.Message : e.ToString());
                if (AppState.Logger.LogPath != null) AppendLog($"Log file: {AppState.Logger.LogPath}");
                CloseButton.IsVisible = true;
            }
        }

        private void AppendLog(string line)
        {
            LogBox.Text += line + Environment.NewLine;
            LogBox.CaretIndex = LogBox.Text!.Length;
        }

        private static bool CanWrite(string directory)
        {
            try
            {
                var probe = Path.Combine(directory, $".typedater-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
