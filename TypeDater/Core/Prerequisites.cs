namespace TypeDater.Core
{
    public class StepFailedException : Exception
    {
        public StepFailedException(string message) : base(message) { }
    }

    // Checks for, and where possible installs, everything needed to build TypeDitor from source.
    public class Prerequisites
    {
        // TypeDitor targets net10.0, so this is the oldest SDK that can build it.
        public const int RequiredSdkMajor = 10;

        private readonly ProcessRunner _runner;
        private readonly Logger _logger;

        public string GitCommand { get; private set; } = "git";
        public string DotnetCommand { get; private set; } = "dotnet";
        public string PythonCommand { get; private set; } = OperatingSystem.IsWindows() ? "py" : "python3";

        public Prerequisites(ProcessRunner runner, Logger logger)
        {
            _runner = runner;
            _logger = logger;
        }

        public async Task EnsureGit(Action<string> report)
        {
            if (await TryResolve(c => GitCommand = c, "--version", "git", @"C:\Program Files\Git\cmd\git.exe")) return;

            if (OperatingSystem.IsWindows())
            {
                report("Git not found, installing with winget...");
                await Winget("Git.Git");
                if (await TryResolve(c => GitCommand = c, "--version", "git", @"C:\Program Files\Git\cmd\git.exe")) return;
            }
            throw new StepFailedException("Git is required but could not be found or installed. Please install git and run TypeDater again.");
        }

        public async Task EnsureDotnet(Action<string> report)
        {
            var dotnetHome = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet");

            if (await TryResolve(c => DotnetCommand = c, "--version", "dotnet", dotnetHome)) return;

            report(".NET not found, installing...");
            await InstallDotnetSdk(report);
            if (await TryResolve(c => DotnetCommand = c, "--version", "dotnet", dotnetHome)) return;
            throw new StepFailedException(".NET is required but could not be found or installed. Please install the .NET SDK from https://dotnet.microsoft.com and run TypeDater again.");
        }

        public async Task EnsureDotnetSdk(Action<string> report)
        {
            if (await HasRequiredSdk()) return;

            report($".NET SDK {RequiredSdkMajor} not found, installing...");
            await InstallDotnetSdk(report);
            if (await HasRequiredSdk()) return;
            throw new StepFailedException($"The .NET {RequiredSdkMajor} SDK could not be installed. Please install it from https://dotnet.microsoft.com and run TypeDater again.");
        }

        public async Task EnsurePython(Action<string> report)
        {
            if (await TryResolve(c => PythonCommand = c, "--version", PythonCommand)) return;
            if (OperatingSystem.IsWindows() && await TryResolve(c => PythonCommand = c, "--version", "python")) return;

            if (OperatingSystem.IsWindows())
            {
                report("Python not found, installing with winget...");
                await Winget("Python.Python.3.13");
                if (await TryResolve(c => PythonCommand = c, "--version", "py", @"C:\Windows\py.exe")) return;
            }
            throw new StepFailedException("Python 3 is required but could not be found or installed. Please install Python 3 and run TypeDater again.");
        }

        public async Task EnsureAvaloniaTemplates(Action<string> report)
        {
            var list = await _runner.Run(DotnetCommand, new[] { "new", "list", "Avalonia" });
            if (list.Success && list.Output.Contains("avalonia.app"))
            {
                report("Updating Avalonia templates...");
                await _runner.Run(DotnetCommand, new[] { "new", "update" });
                return;
            }

            report("Installing Avalonia templates...");
            var install = await _runner.Run(DotnetCommand, new[] { "new", "install", "Avalonia.Templates" });
            if (!install.Success)
            {
                throw new StepFailedException("The Avalonia templates could not be installed. See the log for details.");
            }
        }

        // Tries the command from PATH first, then well-known absolute locations.
        // On success the command is kept and its folder is prepended to PATH so child processes find it too.
        private async Task<bool> TryResolve(Action<string> setCommand, string probeArgument, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (Path.IsPathRooted(candidate) && !File.Exists(candidate)) continue;
                if (!await _runner.Exists(candidate, probeArgument)) continue;

                setCommand(candidate);
                if (Path.IsPathRooted(candidate)) PrependToPath(Path.GetDirectoryName(candidate)!);
                return true;
            }
            return false;
        }

        private static void PrependToPath(string directory)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (path.Split(Path.PathSeparator).Contains(directory, StringComparer.OrdinalIgnoreCase)) return;
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + path);
        }

        private async Task<bool> HasRequiredSdk()
        {
            var result = await _runner.Run(DotnetCommand, new[] { "--list-sdks" });
            if (!result.Success) return false;
            foreach (var line in result.Output.Split('\n'))
            {
                var versionPart = line.Trim().Split(' ').FirstOrDefault() ?? "";
                if (Version.TryParse(versionPart, out var version) && version.Major >= RequiredSdkMajor) return true;
            }
            return false;
        }

        private async Task InstallDotnetSdk(Action<string> report)
        {
            if (OperatingSystem.IsWindows())
            {
                await Winget($"Microsoft.DotNet.SDK.{RequiredSdkMajor}");
            }
            else
            {
                // Official install script into ~/.dotnet, made visible to this process and future shells.
                var script = Path.Combine(Path.GetTempPath(), "dotnet-install.sh");
                using (var http = new HttpClient())
                {
                    File.WriteAllBytes(script, await http.GetByteArrayAsync("https://dot.net/v1/dotnet-install.sh"));
                }
                await _runner.Run("bash", new[] { script, "--channel", $"{RequiredSdkMajor}.0" }, onOutput: report);
                try { File.Delete(script); } catch { }

                var dotnetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");
                Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
                PrependToPath(dotnetRoot);
                EnvironmentSetup.AppendToShellProfile(_logger,
                    $"export DOTNET_ROOT=\"{dotnetRoot}\"",
                    $"export PATH=\"{dotnetRoot}:$PATH\"");
            }
        }

        private async Task Winget(string packageId)
        {
            var result = await _runner.Run("winget", new[]
            {
                "install", "--id", packageId, "-e", "--silent",
                "--accept-package-agreements", "--accept-source-agreements"
            });
            if (!result.Success)
            {
                _logger.Log($"winget install {packageId} failed or winget is unavailable");
            }
        }
    }
}
