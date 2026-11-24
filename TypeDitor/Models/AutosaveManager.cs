using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using TypeD.Models.Data;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;

namespace TypeDitor.Models
{
    internal sealed class AutosaveManager : IDisposable
    {
        private sealed class SnapshotMetadata
        {
            public string ProjectFilePath { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public DateTime LastSavedAtUtc { get; set; }
        }

        internal sealed record RecoverySnapshot(string ProjectDirectory, DateTime CreatedAtUtc);

        private const string MetadataFileName = "autosave.json";
        private readonly Project Project;
        private readonly ISaveModel SaveModel;
        private readonly ISettingModel SettingModel;
        private readonly ILogModel LogModel;
        private readonly DispatcherTimer Timer;
        private AutosaveSettingContext Settings;
        private DateTime LastAutosaveAtUtc = DateTime.UtcNow;
        private bool IsSaving;
        private static event Action SettingsChanged;

        public AutosaveManager(Project project, ISaveModel saveModel, ISettingModel settingModel, ILogModel logModel)
        {
            Project = project;
            SaveModel = saveModel;
            SettingModel = settingModel;
            LogModel = logModel;
            Settings = SettingModel.GetContext<AutosaveSettingContext>(SettingLevel.User);
            SettingsChanged += RefreshSettings;
            Timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            Timer.Tick += OnTimerTick;
            Timer.Start();
        }

        private async void OnTimerTick(object sender, EventArgs e)
        {
            if (IsSaving)
                return;

            var settings = Settings;
            if (settings.BackupCount.Value <= 0)
            {
                var existingSnapshots = GetProjectAutosaveDirectory(Project.ProjectFilePath);
                if (Directory.Exists(existingSnapshots))
                    await Task.Run(() => PruneSnapshots(existingSnapshots, 0));
                return;
            }
            if (settings.IntervalMinutes.Value <= 0 ||
                DateTime.UtcNow - LastAutosaveAtUtc < TimeSpan.FromMinutes(settings.IntervalMinutes.Value))
                return;
            if (!SaveModel.AnythingToSave)
                return;

            IsSaving = true;
            try
            {
                var root = GetProjectAutosaveDirectory(Project.ProjectFilePath);
                Directory.CreateDirectory(root);
                var createdAt = DateTime.UtcNow;
                var snapshotRoot = Path.Combine(root, createdAt.ToString("yyyyMMddTHHmmssfffffffZ"));
                var snapshotProjectPath = Path.Combine(snapshotRoot, "project");
                Directory.CreateDirectory(snapshotRoot);
                try
                {
                    await SaveModel.CreateAutosave(Project, snapshotProjectPath);
                    var metadata = new SnapshotMetadata
                    {
                        ProjectFilePath = Path.GetFullPath(Project.ProjectFilePath),
                        CreatedAtUtc = DateTime.UtcNow,
                        LastSavedAtUtc = ProjectSaveState.GetLastSavedUtc(Project)
                    };
                    await File.WriteAllTextAsync(
                        Path.Combine(snapshotRoot, MetadataFileName),
                        JsonSerializer.Serialize(metadata));
                    SaveModel.NotifyAutosaveCompleted(Project);
                    LastAutosaveAtUtc = metadata.CreatedAtUtc;
                    await Task.Run(() => PruneSnapshots(root, settings.BackupCount.Value));
                }
                catch
                {
                    Directory.Delete(snapshotRoot, recursive: true);
                    throw;
                }
            }
            catch (Exception exception)
            {
                LogModel.Log($"Could not create autosave for {Project.ProjectFilePath}: {exception}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        internal static void NotifySettingsChanged()
        {
            SettingsChanged?.Invoke();
        }

        private void RefreshSettings()
        {
            Settings = SettingModel.GetContext<AutosaveSettingContext>(SettingLevel.User);
        }

        public static RecoverySnapshot FindNewerSnapshot(Project project)
        {
            var root = GetProjectAutosaveDirectory(project.ProjectFilePath);
            if (!Directory.Exists(root))
                return null;

            var latestSavedAt = ProjectSaveState.GetLastSavedUtc(project);
            foreach (var snapshotRoot in Directory.EnumerateDirectories(root)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                var metadataPath = Path.Combine(snapshotRoot, MetadataFileName);
                if (!File.Exists(metadataPath))
                    continue;

                var metadata = JsonSerializer.Deserialize<SnapshotMetadata>(File.ReadAllText(metadataPath));
                if (metadata == null ||
                    !PathsEqual(metadata.ProjectFilePath, project.ProjectFilePath) ||
                    metadata.CreatedAtUtc.ToUniversalTime() <= latestSavedAt ||
                    !Directory.Exists(Path.Combine(snapshotRoot, "project")))
                    continue;

                return new RecoverySnapshot(
                    Path.Combine(snapshotRoot, "project"),
                    metadata.CreatedAtUtc.ToUniversalTime());
            }

            return null;
        }

        public static void RestoreSnapshot(
            Project project,
            RecoverySnapshot snapshot,
            ISettingModel settingModel)
        {
            var sourceRoot = Path.GetFullPath(snapshot.ProjectDirectory);
            var targetRoot = Path.GetFullPath(project.Location);
            if (PathsEqual(sourceRoot, targetRoot) || IsSameOrAncestor(sourceRoot, targetRoot) ||
                IsSameOrAncestor(targetRoot, sourceRoot))
                throw new InvalidOperationException("The autosave snapshot path is not safe to restore.");

            var snapshotFiles = EnumerateProjectFiles(sourceRoot).ToDictionary(
                path => Path.GetRelativePath(sourceRoot, path),
                path => path,
                PathComparer);
            foreach (var currentFile in EnumerateProjectFiles(targetRoot))
            {
                var relativePath = Path.GetRelativePath(targetRoot, currentFile);
                if (!snapshotFiles.ContainsKey(relativePath))
                    File.Delete(currentFile);
            }

            foreach (var entry in snapshotFiles)
            {
                var destination = Path.Combine(targetRoot, entry.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(entry.Value, destination, overwrite: true);
            }

            var snapshotSettings = Path.Combine(sourceRoot, ".autosave-settings");
            if (Directory.Exists(snapshotSettings))
            {
                foreach (var levelDirectory in Directory.EnumerateDirectories(snapshotSettings))
                {
                    if (!Enum.TryParse<SettingLevel>(Path.GetFileName(levelDirectory), out var level))
                        continue;
                    CopyFiles(levelDirectory, settingModel.GetPath(project, level));
                }
            }

            ProjectSaveState.Record(project);
        }

        private static void CopyFiles(string sourceRoot, string targetRoot)
        {
            foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
                var destination = Path.Combine(targetRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(sourceFile, destination, overwrite: true);
            }
        }

        private static IEnumerable<string> EnumerateProjectFiles(string root)
        {
            return EnumerateProjectFiles(root, root);
        }

        private static IEnumerable<string> EnumerateProjectFiles(string root, string directory)
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                var relativePath = Path.GetRelativePath(root, childDirectory);
                if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0 ||
                    IsExcludedDirectory(relativePath))
                    continue;

                foreach (var file in EnumerateProjectFiles(root, childDirectory))
                    yield return file;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                    yield return file;
        }

        private static bool IsExcludedDirectory(string relativePath)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Any(part => part.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || part.Equals(".autosave-settings", StringComparison.OrdinalIgnoreCase)
                || part.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                || part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || part.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                || (parts.Length >= 2 && parts[0].Equals("TypeO", StringComparison.OrdinalIgnoreCase)
                    && parts[1].Equals("build", StringComparison.OrdinalIgnoreCase));
        }

        private static void PruneSnapshots(string root, int backupCount)
        {
            foreach (var snapshot in Directory.EnumerateDirectories(root)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Skip(backupCount))
                Directory.Delete(snapshot, recursive: true);
        }

        private static string GetProjectAutosaveDirectory(string projectFilePath)
        {
            var key = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(Path.GetFullPath(projectFilePath))));
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Typedeaf", "TypeO", "Autosaves", key);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static bool IsSameOrAncestor(string candidate, string path)
        {
            if (PathsEqual(candidate, path))
                return true;
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(
                prefix,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        public void Dispose()
        {
            Timer.Stop();
            Timer.Tick -= OnTimerTick;
            SettingsChanged -= RefreshSettings;
        }
    }
}
