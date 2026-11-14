namespace TypeDater.Core
{
    // Start menu / desktop shortcuts for the installed TypeDitor.
    public class Shortcuts
    {
        private readonly ProcessRunner _runner;
        private readonly Logger _logger;

        public Shortcuts(ProcessRunner runner, Logger logger)
        {
            _runner = runner;
            _logger = logger;
        }

        public async Task Create(string installDirectory, bool startMenu, bool desktop)
        {
            var exePath = Path.Combine(installDirectory, AppState.TypeDitorExeName);
            if (OperatingSystem.IsWindows())
            {
                if (startMenu) await CreateWindowsShortcut(exePath, Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
                if (desktop) await CreateWindowsShortcut(exePath, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (startMenu) CreateDesktopEntry(exePath, Path.Combine(home, ".local", "share", "applications"));
                if (desktop) CreateDesktopEntry(exePath, Path.Combine(home, "Desktop"));
            }
        }

        private async Task CreateWindowsShortcut(string exePath, string folder)
        {
            try
            {
                var shortcutPath = Path.Combine(folder, "TypeDitor.lnk");
                var script =
                    $"$ws = New-Object -ComObject WScript.Shell; " +
                    $"$s = $ws.CreateShortcut('{shortcutPath}'); " +
                    $"$s.TargetPath = '{exePath}'; " +
                    $"$s.WorkingDirectory = '{Path.GetDirectoryName(exePath)}'; " +
                    $"$s.Save()";
                await _runner.Run("powershell", new[] { "-NoProfile", "-NonInteractive", "-Command", script });
            }
            catch (Exception e)
            {
                _logger.Log(e, $"creating shortcut in {folder}");
            }
        }

        private void CreateDesktopEntry(string exePath, string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var entry = string.Join("\n",
                    "[Desktop Entry]",
                    "Type=Application",
                    "Name=TypeDitor",
                    $"Exec=\"{exePath}\"",
                    $"Path={Path.GetDirectoryName(exePath)}",
                    "Terminal=false",
                    "Categories=Development;IDE;",
                    "");
                var path = Path.Combine(folder, "typeditor.desktop");
                File.WriteAllText(path, entry);
                EnvironmentSetup.TryMakeExecutable(_logger, path);
            }
            catch (Exception e)
            {
                _logger.Log(e, $"creating desktop entry in {folder}");
            }
        }
    }
}
