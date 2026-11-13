using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TypeOProject = TypeD.Models.Data.Project;

namespace TypeDitor.View.Dialogs.Tools
{
    public sealed class AboutModuleDisplay
    {
        public string Name { get; init; }
        public string Version { get; init; }
        public string GitPage { get; init; }
        public string BuildDescription { get; init; }
        public bool HasGitPage => !string.IsNullOrEmpty(GitPage);
        public bool HasBuildDescription => !string.IsNullOrEmpty(BuildDescription);
    }

    public partial class AboutDialog : Window
    {
        public AboutDialog() : this(null) { }

        public AboutDialog(TypeOProject project)
        {
            InitializeComponent();

            var editorAssembly = typeof(MainWindow).Assembly;
            var typeDAssembly = typeof(TypeOProject).Assembly;
            TypeDitorVersion.Text = $"Version {GetVersion(editorAssembly)}";
            TypeDitorBuild.Text = $"Built {GetBuildTime(editorAssembly)}";
            TypeDVersion.Text = $"Version {GetVersion(typeDAssembly)}";
            TypeDBuild.Text = $"Built {GetBuildTime(typeDAssembly)}";
            ProjectName.Text = project == null ? "No project is open" : $"Project: {project.ProjectName}";
            RuntimeInfo.Text = $"{RuntimeInformation.FrameworkDescription} · {Environment.OSVersion.VersionString}";

            var modules = project?.Modules;
            NoModulesText.IsVisible = modules == null || modules.Count == 0;
            if (modules == null) return;

            ModulesList.ItemsSource = modules.ConvertAll(module =>
            {
                var assembly = module.ModuleTypeInfo?.Assembly;
                var local = module.IsLocal;
                var version = local ? module.TypeO?.Version ?? "Local" : module.Version;
                var gitPage = string.IsNullOrWhiteSpace(module.Author) || string.IsNullOrWhiteSpace(module.Name)
                    ? null
                    : $"https://github.com/{Uri.EscapeDataString(module.Author)}/{Uri.EscapeDataString(module.Name)}";
                return new AboutModuleDisplay
                {
                    Name = module.Name,
                    Version = $"Version {version}",
                    GitPage = gitPage,
                    BuildDescription = local
                        ? $"Local build · Built {GetBuildTime(assembly)}"
                        : null
                };
            });
        }

        private static string GetVersion(Assembly assembly)
        {
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informationalVersion))
                return informationalVersion.Split('+')[0];

            var version = assembly.GetName().Version;
            return version == null
                ? "Unknown"
                : $"{version.Major}.{version.Minor}{(version.Build >= 0 ? $".{version.Build}" : string.Empty)}";
        }

        private static string GetBuildTime(Assembly assembly)
        {
            var location = assembly?.Location;
            return !string.IsNullOrEmpty(location) && File.Exists(location)
                ? File.GetLastWriteTime(location).ToString("g", CultureInfo.CurrentCulture)
                : "unavailable";
        }

        private async void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string url }
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
                return;

            try
            {
                Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
            }
            catch (Win32Exception exception)
            {
                await ShowLinkError(exception);
            }
            catch (InvalidOperationException exception)
            {
                await ShowLinkError(exception);
            }
        }

        private async Task ShowLinkError(Exception exception)
        {
            var dialog = MessageBoxManager.GetMessageBoxStandard(
                "Could not open link",
                exception.Message,
                ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Error);
            await dialog.ShowWindowDialogAsync(this);
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
