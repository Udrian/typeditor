using System.Diagnostics;

namespace TypeDater.Core
{
    public class InstallOptions
    {
        public string InstallDirectory { get; set; } = AppState.DefaultInstallDirectory;
        public bool StartMenuShortcut { get; set; } = true;
        public bool DesktopShortcut { get; set; } = true;
    }

    // Runs the install (A) and update (B) flows as a sequence of reported steps.
    public class InstallPipeline
    {
        private readonly Logger _logger;
        private readonly ProcessRunner _runner;
        private readonly Prerequisites _prerequisites;
        private readonly GitRepository _repository;
        private readonly Shortcuts _shortcuts;

        // UI callbacks: step index/count/title and detail lines.
        public Action<int, int, string>? OnStep { get; set; }
        public Action<string>? OnDetail { get; set; }

        private string? _tempRoot;

        public InstallPipeline(Logger logger)
        {
            _logger = logger;
            _runner = new ProcessRunner(logger);
            _prerequisites = new Prerequisites(_runner, logger);
            _repository = new GitRepository(_runner);
            _shortcuts = new Shortcuts(_runner, _logger);
        }

        public Task RunInstall(InstallOptions options, Version? knownLatest)
        {
            return Run(options, knownLatest, null, cleanInstall: true);
        }

        public Task RunUpdate(string installDirectory, Version targetVersion, int? typeDitorPid)
        {
            return Run(new InstallOptions { InstallDirectory = installDirectory }, targetVersion, typeDitorPid, cleanInstall: false);
        }

        private async Task Run(InstallOptions options, Version? version, int? typeDitorPid, bool cleanInstall)
        {
            var steps = new List<(string Title, Func<Task> Action)>();
            void Detail(string line) { OnDetail?.Invoke(line); }

            if (!cleanInstall)
            {
                steps.Add(("Closing TypeDitor", () => Task.Run(() => CloseTypeDitor(options.InstallDirectory, typeDitorPid))));
            }
            steps.Add(("Checking Git", () => _prerequisites.EnsureGit(Detail)));
            steps.Add(("Checking .NET", () => _prerequisites.EnsureDotnet(Detail)));
            steps.Add(($"Checking .NET {Prerequisites.RequiredSdkMajor} SDK", () => _prerequisites.EnsureDotnetSdk(Detail)));
            steps.Add(("Checking Python", () => _prerequisites.EnsurePython(Detail)));
            steps.Add(("Checking Avalonia templates", () => _prerequisites.EnsureAvaloniaTemplates(Detail)));
            steps.Add(("Fetching latest version", async () =>
            {
                version ??= await _repository.GetLatestTag();
                if (version == null) throw new StepFailedException("No TypeDitor version could be found on GitHub. See the log for details.");
                Detail($"Latest version is {version}");
            }));
            steps.Add(("Downloading TypeDitor", async () =>
            {
                _tempRoot = Path.Combine(Path.GetTempPath(), "TypeDater", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_tempRoot);
                Detail($"Downloading TypeDitor {version} with TypeR...");
                if (!await _repository.CloneTag(version!.ToString(), RepoDirectory, Detail))
                {
                    throw new StepFailedException($"TypeDitor {version} could not be downloaded. See the log for details.");
                }
            }));
            steps.Add(("Generating project files", async () =>
            {
                // Tags don't contain csproj/sln files; TypeR generates them (create_project_files).
                var typerDirectory = Path.Combine(RepoDirectory, "typer");
                var projectPath = RepoDirectory + Path.DirectorySeparatorChar;
                if (OperatingSystem.IsWindows())
                {
                    await _runner.Run("cmd", new[] { "/c", Path.Combine(typerDirectory, "typer.bat"), "generate", "-p", projectPath }, workingDirectory: RepoDirectory, onOutput: Detail);
                }
                else
                {
                    await _runner.Run("bash", new[] { Path.Combine(typerDirectory, "typer.sh"), "generate", "-p", projectPath }, workingDirectory: RepoDirectory, onOutput: Detail);
                }
                if (!File.Exists(Path.Combine(RepoDirectory, "TypeDitor", "TypeDitor.csproj")))
                {
                    throw new StepFailedException("The TypeDitor project files could not be generated. See the log for details.");
                }
            }));
            steps.Add(("Building TypeDitor", async () =>
            {
                var result = await _runner.Run(_prerequisites.DotnetCommand,
                    new[] { "build", Path.Combine("TypeDitor", "TypeDitor.csproj"), "-c", "Release" },
                    workingDirectory: RepoDirectory, onOutput: Detail);
                if (!result.Success) throw new StepFailedException("TypeDitor could not be built. See the log for details.");
            }));
            steps.Add(("Installing TypeDitor", () => Task.Run(() =>
            {
                var buildOutput = FindBuildOutput();
                Detail($"Copying files to {options.InstallDirectory}");
                CopyDirectory(buildOutput, options.InstallDirectory);
                EnvironmentSetup.TryMakeExecutable(_logger, Path.Combine(options.InstallDirectory, AppState.TypeDitorExeName));
            })));
            steps.Add(("Installing TypeR", () => Task.Run(() =>
            {
                CopyDirectory(Path.Combine(RepoDirectory, "typer"), Path.Combine(options.InstallDirectory, "typer"));
            })));
            if (cleanInstall)
            {
                if (options.StartMenuShortcut || options.DesktopShortcut)
                {
                    steps.Add(("Creating shortcuts", () => _shortcuts.Create(options.InstallDirectory, options.StartMenuShortcut, options.DesktopShortcut)));
                }
                steps.Add(("Registering TypeR path", () => Task.Run(() =>
                {
                    EnvironmentSetup.RegisterTyperPath(_logger, Path.Combine(options.InstallDirectory, "typer"));
                })));
                steps.Add(("Installing TypeDater", () => Task.Run(() => CopySelf(options.InstallDirectory))));
            }
            steps.Add(("Finishing up", () => Task.Run(() =>
            {
                var manifest = Manifest.Load(options.InstallDirectory);
                manifest.InstalledVersion = version!.ToString();
                manifest.SkippedVersion = null;
                manifest.Save(options.InstallDirectory);
                Cleanup();
            })));

            try
            {
                for (var i = 0; i < steps.Count; i++)
                {
                    OnStep?.Invoke(i, steps.Count, steps[i].Title);
                    _logger.Log($"=== {steps[i].Title} ===");
                    await steps[i].Action();
                }
                OnStep?.Invoke(steps.Count, steps.Count, "Done");
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        private string RepoDirectory { get { return Path.Combine(_tempRoot!, "typeditor"); } }

        public void LaunchTypeDitor(string installDirectory)
        {
            var exePath = Path.Combine(installDirectory, AppState.TypeDitorExeName);
            _logger.Log($"Launching {exePath}");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = installDirectory,
                UseShellExecute = true
            });
        }

        private void CloseTypeDitor(string installDirectory, int? pid)
        {
            var processes = new List<Process>();
            if (pid != null)
            {
                try { processes.Add(Process.GetProcessById(pid.Value)); } catch { }
            }
            foreach (var process in Process.GetProcessesByName("TypeDitor"))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (path != null && Path.GetDirectoryName(path)!.Equals(Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                        && processes.All(p => p.Id != process.Id))
                    {
                        processes.Add(process);
                    }
                }
                catch { }
            }

            foreach (var process in processes)
            {
                try
                {
                    _logger.Log($"Closing TypeDitor (pid {process.Id})");
                    process.CloseMainWindow();
                    if (!process.WaitForExit(10000))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                }
                catch (Exception e)
                {
                    _logger.Log(e, $"closing TypeDitor (pid {process.Id})");
                }
            }
        }

        private string FindBuildOutput()
        {
            var releaseDirectory = Path.Combine(RepoDirectory, "TypeDitor", "bin", "Release");
            if (Directory.Exists(releaseDirectory))
            {
                foreach (var candidate in Directory.GetDirectories(releaseDirectory))
                {
                    if (File.Exists(Path.Combine(candidate, "TypeDitor.dll"))) return candidate;
                }
            }
            throw new StepFailedException("The TypeDitor build output could not be found. See the log for details.");
        }

        // Recursive copy, overwriting existing files and skipping git metadata.
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var directory in Directory.GetDirectories(source))
            {
                var name = Path.GetFileName(directory);
                if (name == ".git") continue;
                CopyDirectory(directory, Path.Combine(destination, name));
            }
        }

        private void CopySelf(string installDirectory)
        {
            var self = Environment.ProcessPath;
            if (self == null || Path.GetDirectoryName(Path.GetFullPath(self))!.Equals(Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (File.Exists(Path.Combine(Path.GetDirectoryName(self)!, "TypeDater.deps.json")))
            {
                _logger.Log("Warning: TypeDater is not a single-file build; only the executable is copied to the install folder.");
            }
            File.Copy(self, Path.Combine(installDirectory, AppState.TypeDaterExeName), overwrite: true);
        }

        private void Cleanup()
        {
            if (_tempRoot == null) return;
            try
            {
                // Clear read-only flags that git sets on object files so the delete succeeds.
                foreach (var file in Directory.GetFiles(_tempRoot, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(_tempRoot, recursive: true);
                _logger.Log($"Cleaned up {_tempRoot}");
            }
            catch (Exception e)
            {
                _logger.Log(e, $"cleaning up {_tempRoot}");
            }
            _tempRoot = null;
        }
    }
}
