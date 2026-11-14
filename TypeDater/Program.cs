using Avalonia;
using System.Diagnostics;
using TypeDater.Core;

namespace TypeDater
{
    class Program
    {
        // Mode A (install): no TypeDitor next to us; elevate on Windows and show the install wizard.
        // Mode B (update): TypeDitor lives next to us; check for a newer tag and exit silently when there is none.
        [STAThread]
        public static int Main(string[] args)
        {
            var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var logger = new Logger(exeDirectory);
            AppState.ExeDirectory = exeDirectory;
            AppState.Logger = logger;

            try
            {
                ParseArguments(args, out var pid, out var acceptedVersion, out var elevated);
                AppState.TypeDitorPid = pid;

                if (File.Exists(Path.Combine(exeDirectory, AppState.TypeDitorExeName)))
                {
                    AppState.Mode = DaterMode.Update;
                    if (!PrepareUpdate(logger, acceptedVersion)) return 0;
                }
                else
                {
                    AppState.Mode = DaterMode.Install;
                    logger.Log("Starting clean install");
                    if (OperatingSystem.IsWindows() && !elevated && !IsAdministrator())
                    {
                        return RelaunchElevated(logger, args) ? 0 : 1;
                    }
                }

                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                logger.Log(e, "unhandled");
                return 1;
            }
        }

        // Returns false when no update is needed and TypeDater should exit silently.
        private static bool PrepareUpdate(Logger logger, string? acceptedVersion)
        {
            AppState.Manifest = Manifest.Load(AppState.ExeDirectory);

            var repository = new GitRepository(new ProcessRunner(logger));
            var latest = repository.GetLatestTag().GetAwaiter().GetResult();
            if (latest == null)
            {
                logger.Log("Update check: could not determine the latest version, exiting silently");
                return false;
            }
            AppState.LatestVersion = latest;

            if (acceptedVersion != null && acceptedVersion == latest.ToString())
            {
                // Elevated relaunch after the user already accepted the update.
                AppState.UpdateAccepted = true;
                return true;
            }

            Version.TryParse(AppState.Manifest.InstalledVersion, out var installed);
            if (installed != null && latest <= installed)
            {
                logger.Log($"Update check: {installed} is up to date");
                return false;
            }
            if (AppState.Manifest.SkippedVersion == latest.ToString())
            {
                logger.Log($"Update check: version {latest} was skipped by the user");
                return false;
            }
            logger.Log($"Update check: {latest} is available (installed: {AppState.Manifest.InstalledVersion ?? "unknown"})");
            return true;
        }

        private static void ParseArguments(string[] args, out int? pid, out string? acceptedVersion, out bool elevated)
        {
            pid = null;
            acceptedVersion = null;
            elevated = false;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--pid" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                        pid = parsed;
                        i++;
                        break;
                    case "--accepted" when i + 1 < args.Length:
                        acceptedVersion = args[++i];
                        break;
                    case "--elevated":
                        elevated = true;
                        break;
                }
            }
        }

        public static bool IsAdministrator()
        {
            if (!OperatingSystem.IsWindows()) return true;
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        public static bool RelaunchElevated(Logger logger, IEnumerable<string> args)
        {
            try
            {
                logger.Log("Restarting with administrator rights");
                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                startInfo.ArgumentList.Add("--elevated");
                foreach (var arg in args) startInfo.ArgumentList.Add(arg);
                Process.Start(startInfo);
                return true;
            }
            catch (Exception e)
            {
                // Typically the user declined the elevation prompt.
                logger.Log(e, "restarting elevated");
                return false;
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
