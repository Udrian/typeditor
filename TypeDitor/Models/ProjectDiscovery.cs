using System.Text.Json;

namespace TypeDitor.Models
{
    public enum ProjectFormatStatus
    {
        UpToDate,
        UpgradeNeeded,
        Unsupported,
        Invalid
    }

    public sealed class DiscoveredProject
    {
        public string Name { get; init; }
        public string Location { get; init; }
        public string ProjectFilePath { get; init; }

        /// <summary>The TypeD project format version stored in the file; missing metadata counts as version 0.</summary>
        public int? TypeDFormatVersion { get; init; }
        public int CurrentTypeDFormatVersion { get; init; }

        public ProjectFormatStatus FormatStatus => TypeDFormatVersion switch
        {
            null => ProjectFormatStatus.Invalid,
            var version when version < CurrentTypeDFormatVersion => ProjectFormatStatus.UpgradeNeeded,
            var version when version > CurrentTypeDFormatVersion => ProjectFormatStatus.Unsupported,
            _ => ProjectFormatStatus.UpToDate
        };

        public string CompatibilityText => TypeDFormatVersion is int version
            ? $"TypeD format {version}"
            : "TypeD format unknown";

        public string FormatStatusText => FormatStatus switch
        {
            ProjectFormatStatus.UpgradeNeeded => "Upgrade on open",
            ProjectFormatStatus.Unsupported => "Needs newer TypeD",
            ProjectFormatStatus.Invalid => "Invalid format",
            _ => "Up to date"
        };

        public string FormatStatusTip => FormatStatus switch
        {
            ProjectFormatStatus.UpgradeNeeded =>
                $"Opening the project upgrades it from TypeD format {TypeDFormatVersion} to {CurrentTypeDFormatVersion}. The original file is backed up.",
            ProjectFormatStatus.Unsupported =>
                $"The project uses TypeD format {TypeDFormatVersion}, but this editor supports up to format {CurrentTypeDFormatVersion}.",
            ProjectFormatStatus.Invalid => "The project file has an invalid FormatVersions entry and cannot be opened.",
            _ => $"The project matches this editor's TypeD format {CurrentTypeDFormatVersion}."
        };

        public bool HasFormatWarning => FormatStatus != ProjectFormatStatus.UpToDate;
    }

    internal sealed class ProjectDiscoveryResult
    {
        public List<DiscoveredProject> Projects { get; } = new();
        public List<string> Errors { get; } = new();
    }

    internal static class ProjectDiscovery
    {
        private static readonly StringComparer PathComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public static ProjectDiscoveryResult Scan(IEnumerable<string> folders, int currentTypeDFormatVersion)
        {
            var result = new ProjectDiscoveryResult();
            var projects = new Dictionary<string, DiscoveredProject>(PathComparer);
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var folder in folders ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                if (!Path.IsPathFullyQualified(folder))
                {
                    result.Errors.Add($"Scan folder is not an absolute path: {folder}");
                    continue;
                }

                string root;
                try
                {
                    root = Path.GetFullPath(folder);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    result.Errors.Add($"Invalid scan folder '{folder}': {exception.Message}");
                    continue;
                }
                if (!Directory.Exists(root))
                {
                    result.Errors.Add($"Scan folder does not exist: {root}");
                    continue;
                }

                try
                {
                    foreach (var manifest in Directory.EnumerateFiles(root, "typeo.json", options))
                    {
                        try
                        {
                            var project = ReadProject(manifest, currentTypeDFormatVersion, result.Errors);
                            if (project != null)
                                projects.TryAdd(project.ProjectFilePath, project);
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
                        {
                            result.Errors.Add($"{manifest}: {exception.Message}");
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    result.Errors.Add($"{root}: {exception.Message}");
                }
            }

            result.Projects.AddRange(projects.Values.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase));
            return result;
        }

        private static DiscoveredProject ReadProject(string manifestPath, int currentTypeDFormatVersion, List<string> errors)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            if (!string.Equals(ReadString(root, "type"), "exe", StringComparison.OrdinalIgnoreCase))
                return null;

            var name = ReadString(root, "name");
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
            {
                errors.Add($"{manifestPath}: the project name is missing or invalid.");
                return null;
            }

            var location = Path.GetDirectoryName(manifestPath);
            var projectFilePath = Path.Combine(location, $"{name}.typeo");
            if (!File.Exists(projectFilePath))
            {
                errors.Add($"{manifestPath}: matching project file '{name}.typeo' was not found.");
                return null;
            }

            using var project = JsonDocument.Parse(File.ReadAllText(projectFilePath));
            var projectName = ReadString(project.RootElement, "ProjectName");
            if (string.IsNullOrWhiteSpace(projectName))
            {
                errors.Add($"{projectFilePath}: the project name is missing.");
                return null;
            }

            return new DiscoveredProject
            {
                Name = projectName,
                Location = location,
                ProjectFilePath = projectFilePath,
                TypeDFormatVersion = ReadTypeDFormatVersion(project.RootElement),
                CurrentTypeDFormatVersion = currentTypeDFormatVersion
            };
        }

        // Mirrors TypeD's FormatVersionModel: a missing FormatVersions object or TypeD entry is version 0.
        private static int? ReadTypeDFormatVersion(JsonElement root)
        {
            if (!root.TryGetProperty("FormatVersions", out var versions))
                return 0;
            if (versions.ValueKind != JsonValueKind.Object)
                return null;
            if (!versions.TryGetProperty("TypeD", out var version))
                return 0;
            return version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var value) && value >= 0
                ? value
                : null;
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
            }
            return null;
        }
    }
}
