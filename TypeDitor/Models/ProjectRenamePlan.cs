using System.Text.Json.Nodes;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;

namespace TypeDitor.Models
{
    public enum ProjectRenameAssetKind
    {
        ProjectFile,
        SourceFolder,
        TypeOManifest,
        GeneratedCode,
        Components,
        BuildOutput,
        Solution,
        UserSettings,
        RecentEntry,
        ProjectFolder
    }

    public sealed class ProjectRenameAsset
    {
        public ProjectRenameAssetKind Kind { get; }
        public string Name { get; }
        public string Path { get; }
        public bool IsRequired { get; }
        public bool IsSelected { get; set; } = true;

        public ProjectRenameAsset(ProjectRenameAssetKind kind, string name, string path, bool isRequired = false)
        {
            Kind = kind;
            Name = name;
            Path = path;
            IsRequired = isRequired;
        }
    }

    public sealed class ProjectRenamePlan
    {
        public string ProjectName { get; init; }
        public string Location { get; init; }
        public List<ProjectRenameAsset> Assets { get; } = new();
    }

    internal static class ProjectRenameService
    {
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".sln", ".slnx", ".json", ".props", ".targets", ".config",
            ".xml", ".axaml", ".xaml", ".resx", ".component"
        };
        private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "node_modules"
        };
        private static readonly StringComparer PathComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private static readonly StringComparison NameComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        public static ProjectRenamePlan CreatePlan(DiscoveredProject project, IRecentProvider recentProvider)
        {
            ArgumentNullException.ThrowIfNull(project);
            var location = Path.GetFullPath(project.Location);
            var projectFilePath = Path.GetFullPath(project.ProjectFilePath);
            if (!File.Exists(projectFilePath))
                throw new FileNotFoundException("The project file no longer exists.", projectFilePath);

            var data = JsonNode.Parse(File.ReadAllText(projectFilePath)) as JsonObject
                ?? throw new InvalidDataException($"The project file is invalid: '{projectFilePath}'.");
            var projectName = ReadString(data, "ProjectName");
            if (!IsValidProjectName(projectName))
                throw new InvalidDataException($"The project name is missing or invalid in '{projectFilePath}'.");

            var plan = new ProjectRenamePlan { ProjectName = projectName, Location = location };
            plan.Assets.Add(new ProjectRenameAsset(ProjectRenameAssetKind.ProjectFile,
                "Project file and project identifier", projectFilePath, true));

            AddDirectory(plan, ProjectRenameAssetKind.SourceFolder, "Source project and C# files",
                Path.Combine(location, projectName));
            AddFile(plan, ProjectRenameAssetKind.TypeOManifest, "TypeO project manifest",
                Path.Combine(location, "typeo.json"));
            AddDirectory(plan, ProjectRenameAssetKind.GeneratedCode, "Generated TypeO code",
                Path.Combine(location, "TypeO", "code", projectName));
            AddDirectory(plan, ProjectRenameAssetKind.Components, "Component data and generated component code",
                Path.Combine(location, "TypeO", "components", projectName));
            if (string.Equals(ReadString(data, "CSProjName"), projectName, StringComparison.Ordinal))
                AddDirectory(plan, ProjectRenameAssetKind.BuildOutput, "C# build output",
                    Path.Combine(location, "TypeO", "build", projectName));

            var solutionPath = FindSolutionPath(location, ReadString(data, "CSSolutionPath"));
            if (solutionPath != null)
                plan.Assets.Add(new ProjectRenameAsset(ProjectRenameAssetKind.Solution,
                    "C# solution and project references", solutionPath));

            var userSettings = UserSettingsPath(projectName);
            AddDirectory(plan, ProjectRenameAssetKind.UserSettings, "User settings and theme data", userSettings);
            if (recentProvider.Get().Any(recent => PathsEqual(recent.Path, projectFilePath)))
                plan.Assets.Add(new ProjectRenameAsset(ProjectRenameAssetKind.RecentEntry,
                    "Recent-project entry", projectFilePath));
            if (PathComparer.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(location)), projectName))
                plan.Assets.Add(new ProjectRenameAsset(ProjectRenameAssetKind.ProjectFolder,
                    "Project folder", location));

            return plan;
        }

        public static bool IsValidProjectName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !name.Contains(' ')
                && (char.IsLetter(name[0]) || name[0] == '_');
        }

        public static string UserSettingsPath(string projectName)
        {
            if (!IsValidProjectName(projectName))
                throw new ArgumentException("The project name is invalid.", nameof(projectName));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TypeO", "Settings", projectName);
        }

        public static void Rename(ProjectRenamePlan plan, string newName, IRecentProvider recentProvider)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(recentProvider);
            if (!IsValidProjectName(newName))
                throw new ArgumentException("The project name is invalid.", nameof(newName));
            if (string.Equals(plan.ProjectName, newName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The new project name must be different.", nameof(newName));

            var selected = plan.Assets.Where(asset => asset.IsSelected).ToList();
            if (!selected.Any(asset => asset.Kind == ProjectRenameAssetKind.ProjectFile))
                throw new InvalidOperationException("The project file and identifier must be renamed.");

            var projectFile = selected.Single(asset => asset.Kind == ProjectRenameAssetKind.ProjectFile);
            var newProjectFilePath = Path.Combine(plan.Location, $"{newName}.typeo");
            EnsureDestinationAvailable(projectFile.Path, newProjectFilePath);

            foreach (var asset in selected)
            {
                var destination = asset.Kind switch
                {
                    ProjectRenameAssetKind.SourceFolder or ProjectRenameAssetKind.GeneratedCode or
                    ProjectRenameAssetKind.Components or ProjectRenameAssetKind.BuildOutput or
                    ProjectRenameAssetKind.UserSettings
                        => RenameDirectoryPath(asset.Path, newName),
                    ProjectRenameAssetKind.Solution
                        => RenameFileName(asset.Path, plan.ProjectName, newName),
                    ProjectRenameAssetKind.ProjectFolder
                        => RenameDirectoryPath(asset.Path, newName),
                    _ => null
                };
                if (destination != null)
                    EnsureDestinationAvailable(asset.Path, destination);
            }

            var undo = new Stack<Action>();
            try
            {
                UpdateProjectFile(projectFile.Path, newName, selected, plan, undo);

                foreach (var asset in selected.Where(asset => asset.Kind is
                    ProjectRenameAssetKind.SourceFolder or ProjectRenameAssetKind.GeneratedCode or
                    ProjectRenameAssetKind.Components or ProjectRenameAssetKind.BuildOutput))
                    RenameTree(asset.Path, plan.ProjectName, newName, undo);

                foreach (var asset in selected.Where(asset => asset.Kind == ProjectRenameAssetKind.TypeOManifest))
                    UpdateTypeOManifest(asset.Path, plan.ProjectName, newName, undo);

                foreach (var asset in selected.Where(asset => asset.Kind == ProjectRenameAssetKind.Solution))
                {
                    if (selected.Any(tree => IsRenameTree(tree.Kind) && IsWithin(asset.Path, tree.Path)))
                        continue;
                    ReplaceFileContents(asset.Path, plan.ProjectName, newName, undo);
                    var destination = RenameFileName(asset.Path, plan.ProjectName, newName);
                    if (!PathsEqual(asset.Path, destination))
                        MoveFile(asset.Path, destination, undo);
                }

                foreach (var asset in selected.Where(asset => asset.Kind == ProjectRenameAssetKind.UserSettings))
                    MoveDirectory(asset.Path, RenameDirectoryPath(asset.Path, newName), undo);

                MoveFile(projectFile.Path, newProjectFilePath, undo);

                var finalLocation = selected.Any(asset => asset.Kind == ProjectRenameAssetKind.ProjectFolder)
                        ? RenameDirectoryPath(plan.Location, newName)
                    : plan.Location;
                var finalProjectFilePath = Path.Combine(finalLocation, $"{newName}.typeo");

                if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.ProjectFolder))
                    MoveDirectory(plan.Location, finalLocation, undo);

                if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.RecentEntry))
                    recentProvider.Rename(projectFile.Path, finalProjectFilePath, newName);
            }
            catch (Exception exception)
            {
                var rollbackFailures = new List<Exception>();
                while (undo.Count > 0)
                {
                    try
                    {
                        undo.Pop()();
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackFailures.Add(rollbackException);
                    }
                }

                if (rollbackFailures.Count > 0)
                    throw new AggregateException("The project rename failed and rollback was incomplete.",
                        new[] { exception }.Concat(rollbackFailures));
                throw;
            }
        }

        private static void UpdateProjectFile(string path, string newName, IReadOnlyCollection<ProjectRenameAsset> selected,
            ProjectRenamePlan plan, Stack<Action> undo)
        {
            var oldName = plan.ProjectName;
            var project = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidDataException($"The project file is invalid: '{path}'.");
            project["ProjectName"] = newName;

            if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.SourceFolder)
                && string.Equals(ReadString(project, "CSProjName"), oldName, StringComparison.Ordinal))
                project["CSProjName"] = newName;

            if (ReadString(project, "CSSolutionPath") is string solutionPath)
            {
                var isAbsolute = Path.IsPathRooted(solutionPath);
                var fullSolutionPath = Path.GetFullPath(isAbsolute
                    ? solutionPath
                    : Path.Combine(plan.Location, solutionPath));
                var sourcePath = Path.Combine(plan.Location, oldName);
                if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.SourceFolder)
                    && IsWithin(fullSolutionPath, sourcePath))
                    fullSolutionPath = Path.Combine(
                        Path.Combine(plan.Location, newName),
                        Path.GetRelativePath(sourcePath, fullSolutionPath));

                if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.ProjectFolder)
                    && IsWithin(fullSolutionPath, plan.Location))
                    fullSolutionPath = Path.Combine(
                        RenameDirectoryPath(plan.Location, newName),
                        Path.GetRelativePath(plan.Location, fullSolutionPath));

                if (selected.Any(asset => asset.Kind == ProjectRenameAssetKind.Solution))
                    fullSolutionPath = RenameFileName(fullSolutionPath, oldName, newName);

                var finalLocation = selected.Any(asset => asset.Kind == ProjectRenameAssetKind.ProjectFolder)
                    ? RenameDirectoryPath(plan.Location, newName)
                    : plan.Location;
                project["CSSolutionPath"] = isAbsolute
                    ? fullSolutionPath
                    : Path.GetRelativePath(finalLocation, fullSolutionPath);
            }

            if (ReadString(project, "StartScene") is string startScene
                && (startScene == oldName || startScene.StartsWith(oldName + ".", StringComparison.Ordinal)))
                project["StartScene"] = newName + startScene.Substring(oldName.Length);

            WriteText(path, project.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), undo);
        }

        private static void UpdateTypeOManifest(string path, string oldName, string newName, Stack<Action> undo)
        {
            var manifest = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidDataException($"The TypeO manifest is invalid: '{path}'.");
            var manifestName = ReadString(manifest, "name");
            SetStringProperty(manifest, "name", oldName, newName, updateAnyString: true);
            foreach (var property in manifest.Where(property => property.Key.Equals("project", StringComparison.OrdinalIgnoreCase)))
            {
                if (property.Value is not JsonArray projects) continue;
                foreach (var item in projects.OfType<JsonObject>())
                {
                    if (ReadString(item, "Name") is string projectName
                        && (projectName == oldName || projectName == manifestName))
                        SetStringProperty(item, "Name", projectName, newName);
                }
            }
            WriteText(path, manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), undo);
        }

        private static void SetStringProperty(JsonObject item, string name, string oldValue, string newValue,
            bool updateAnyString = false)
        {
            foreach (var property in item.ToList())
            {
                if (property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value is JsonValue value
                    && value.TryGetValue<string>(out var current)
                    && (updateAnyString || current == oldValue))
                    item[property.Key] = newValue;
            }
        }

        private static void RenameTree(string root, string oldName, string newName, Stack<Action> undo)
        {
            ReplaceFilesInTree(root, oldName, newName, undo);

            var files = EnumerateFiles(root).ToList();
            foreach (var path in files.OrderByDescending(path => path.Length))
            {
                var destination = RenameFileName(path, oldName, newName);
                if (!PathsEqual(path, destination))
                    MoveFile(path, destination, undo);
            }

            var directories = EnumerateDirectories(root).Where(path => !PathsEqual(path, root))
                .OrderByDescending(path => path.Length).ToList();
            foreach (var path in directories)
            {
                var destination = RenameFileName(path, oldName, newName);
                if (!PathsEqual(path, destination))
                    MoveDirectory(path, destination, undo);
            }

            var rootDestination = RenameFileName(root, oldName, newName);
            if (!PathsEqual(root, rootDestination))
                MoveDirectory(root, rootDestination, undo);
        }

        private static bool IsRenameTree(ProjectRenameAssetKind kind)
        {
            return kind is ProjectRenameAssetKind.SourceFolder or ProjectRenameAssetKind.GeneratedCode
                or ProjectRenameAssetKind.Components or ProjectRenameAssetKind.BuildOutput;
        }

        private static void ReplaceFilesInTree(string root, string oldName, string newName, Stack<Action> undo)
        {
            foreach (var path in EnumerateFiles(root))
            {
                if (!TextExtensions.Contains(Path.GetExtension(path))) continue;
                ReplaceFileContents(path, oldName, newName, undo);
            }
        }

        private static IEnumerable<string> EnumerateFiles(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                        yield return file;
                }
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (SkippedDirectories.Contains(Path.GetFileName(child))) continue;
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    pending.Push(child);
                }
            }
        }

        private static IEnumerable<string> EnumerateDirectories(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (SkippedDirectories.Contains(Path.GetFileName(child))) continue;
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    yield return child;
                    pending.Push(child);
                }
            }
        }

        private static void ReplaceFileContents(string path, string oldName, string newName, Stack<Action> undo)
        {
            if (!TextExtensions.Contains(Path.GetExtension(path))) return;
            var original = File.ReadAllText(path);
            if (!original.Contains(oldName, StringComparison.Ordinal)) return;
            WriteText(path, original.Replace(oldName, newName, StringComparison.Ordinal), undo);
        }

        private static void WriteText(string path, string contents, Stack<Action> undo)
        {
            var original = File.Exists(path) ? File.ReadAllText(path) : null;
            undo.Push(() =>
            {
                if (original == null) File.Delete(path);
                else File.WriteAllText(path, original);
            });
            File.WriteAllText(path, contents);
        }

        private static void MoveFile(string source, string destination, Stack<Action> undo)
        {
            if (PathsEqual(source, destination)) return;
            EnsureDestinationAvailable(source, destination);
            File.Move(source, destination);
            undo.Push(() => File.Move(destination, source));
        }

        private static void MoveDirectory(string source, string destination, Stack<Action> undo)
        {
            if (PathsEqual(source, destination)) return;
            EnsureDestinationAvailable(source, destination);
            Directory.Move(source, destination);
            undo.Push(() => Directory.Move(destination, source));
        }

        private static void EnsureDestinationAvailable(string source, string destination)
        {
            if (PathsEqual(source, destination)) return;
            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException($"Cannot rename '{source}' because '{destination}' already exists.");
        }

        private static string FindSolutionPath(string location, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return null;
            var fullConfiguredPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(location, configuredPath);
            var basePath = Path.Combine(Path.GetDirectoryName(fullConfiguredPath) ?? location,
                Path.GetFileNameWithoutExtension(fullConfiguredPath));
            var candidates = new[] { basePath + ".sln", basePath + ".slnx" };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string ReadString(JsonObject item, string name)
        {
            foreach (var property in item)
                if (property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value is JsonValue value && value.TryGetValue<string>(out var result))
                    return result;
            return null;
        }

        private static string RenameFileName(string path, string oldName, string newName)
        {
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path).Replace(oldName, newName, NameComparison);
            return Path.Combine(directory ?? string.Empty, name);
        }

        private static string RenameDirectoryPath(string path, string newName)
        {
            var parent = Path.GetDirectoryName(path);
            return Path.Combine(parent ?? string.Empty, newName);
        }

        private static bool IsWithin(string path, string directory)
        {
            try
            {
                var relative = Path.GetRelativePath(directory, path);
                return relative == "." || (!Path.IsPathRooted(relative)
                    && relative != ".."
                    && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return PathComparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));
        }

        private static void AddFile(ProjectRenamePlan plan, ProjectRenameAssetKind kind, string name, string path)
        {
            if (File.Exists(path))
                plan.Assets.Add(new ProjectRenameAsset(kind, name, path));
        }

        private static void AddDirectory(ProjectRenamePlan plan, ProjectRenameAssetKind kind, string name, string path)
        {
            if (Directory.Exists(path))
                plan.Assets.Add(new ProjectRenameAsset(kind, name, path));
        }
    }
}
