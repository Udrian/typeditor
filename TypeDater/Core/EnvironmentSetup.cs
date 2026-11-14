namespace TypeDater.Core
{
    // Makes the installed TypeR available: TYPER_PATH environment variable on Windows,
    // exports plus a typer command in the shell profile on Unix.
    public static class EnvironmentSetup
    {
        private const string ProfileMarker = "# Added by TypeDater";

        public static void RegisterTyperPath(Logger logger, string typerDirectory)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    Environment.SetEnvironmentVariable("TYPER_PATH", typerDirectory, EnvironmentVariableTarget.Machine);
                }
                catch (Exception e)
                {
                    logger.Log(e, "setting machine TYPER_PATH, falling back to user");
                    Environment.SetEnvironmentVariable("TYPER_PATH", typerDirectory, EnvironmentVariableTarget.User);
                }
            }
            else
            {
                var typerScript = Path.Combine(typerDirectory, "typer.sh");
                AppendToShellProfile(logger,
                    $"export TYPER_PATH=\"{typerDirectory}\"",
                    $"alias typer=\"{typerScript}\"");
                TryMakeExecutable(logger, typerScript);
                TryMakeExecutable(logger, Path.Combine(typerDirectory, "typer.py"));
            }
            Environment.SetEnvironmentVariable("TYPER_PATH", typerDirectory);
        }

        // Appends the lines once to ~/.bashrc (or ~/.profile when no .bashrc exists), keyed by a marker comment.
        public static void AppendToShellProfile(Logger logger, params string[] lines)
        {
            if (OperatingSystem.IsWindows()) return;
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var profile = Path.Combine(home, ".bashrc");
                if (!File.Exists(profile)) profile = Path.Combine(home, ".profile");

                var existing = File.Exists(profile) ? File.ReadAllText(profile) : "";
                var missing = lines.Where(l => !existing.Contains(l)).ToList();
                if (missing.Count == 0) return;

                File.AppendAllText(profile, Environment.NewLine + ProfileMarker + Environment.NewLine + string.Join(Environment.NewLine, missing) + Environment.NewLine);
                logger.Log($"Added {missing.Count} line(s) to {profile}");
            }
            catch (Exception e)
            {
                logger.Log(e, "updating shell profile");
            }
        }

        public static void TryMakeExecutable(Logger logger, string path)
        {
            try
            {
                if (!OperatingSystem.IsWindows() && File.Exists(path))
                {
                    File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, $"making {path} executable");
            }
        }
    }
}
