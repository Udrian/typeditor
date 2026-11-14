using System.Text.Json;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;

namespace TypeDitor.Models
{
    internal static class ProjectScanSettings
    {
        private static readonly string LegacySettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TypeO",
            "Settings",
            "ProjectScanFolders.json");
        private static readonly StringComparer PathComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public static bool PathsEqual(string left, string right)
        {
            return PathComparer.Equals(left, right);
        }

        public static List<string> GetFolders(ISettingModel settingModel)
        {
            var context = settingModel?.GetContext<ProjectScanSettingContext>(SettingLevel.User);
            return Normalize(context?.Folders.Value);
        }

        /// <summary>Adds the folder to the project scan folders unless it is already listed.</summary>
        /// <returns>True if the setting was changed.</returns>
        public static bool AddFolder(ISettingModel settingModel, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return false;
            var context = settingModel.GetContext<ProjectScanSettingContext>(SettingLevel.User);
            if (context == null) return false;

            var folders = Normalize(context.Folders.Value);
            var path = Normalize(new[] { folder }).FirstOrDefault();
            if (path == null || folders.Contains(path, PathComparer)) return false;

            folders.Add(path);
            context.Folders.Value = folders;
            settingModel.SetContext(context, SettingLevel.User);
            return true;
        }

        public static void MigrateLegacy(ISettingModel settingModel, ILogModel logModel)
        {
            if (!File.Exists(LegacySettingsPath)) return;

            var context = settingModel.GetContext<ProjectScanSettingContext>(SettingLevel.User);
            if (context == null || settingModel.HasUserOverride(context.Folders))
                return;

            try
            {
                var legacyFolders = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(LegacySettingsPath));
                if (legacyFolders == null)
                    throw new JsonException("The legacy project scan folder list is invalid.");

                context.Folders.Value = Normalize(legacyFolders);
                settingModel.SetContext(context, SettingLevel.User);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                JsonException or ArgumentException or NotSupportedException)
            {
                logModel?.Log($"Could not migrate project scan folders: {exception}");
            }
        }

        private static List<string> Normalize(IEnumerable<string> folders)
        {
            var normalized = new List<string>();
            foreach (var folder in folders ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var path = folder.Trim();
                if (Path.IsPathFullyQualified(path))
                    path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                if (!normalized.Contains(path, PathComparer))
                    normalized.Add(path);
            }
            return normalized;
        }
    }
}
