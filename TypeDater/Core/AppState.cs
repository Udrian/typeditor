namespace TypeDater.Core
{
    public enum DaterMode
    {
        Install,
        Update
    }

    // State resolved in Program.Main before the UI starts.
    public static class AppState
    {
        public static DaterMode Mode { get; set; } = DaterMode.Install;
        public static string ExeDirectory { get; set; } = AppContext.BaseDirectory;
        public static Logger Logger { get; set; } = null!;
        public static Manifest Manifest { get; set; } = new Manifest();
        public static Version? LatestVersion { get; set; }
        // Update already accepted (elevated relaunch); skip the prompt.
        public static bool UpdateAccepted { get; set; }
        // Process id of the running TypeDitor that launched us.
        public static int? TypeDitorPid { get; set; }

        public static string TypeDitorExeName { get { return OperatingSystem.IsWindows() ? "TypeDitor.exe" : "TypeDitor"; } }
        public static string TypeDaterExeName { get { return OperatingSystem.IsWindows() ? "TypeDater.exe" : "TypeDater"; } }

        public static string DefaultInstallDirectory
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TypeDitor");
                }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "TypeDitor");
            }
        }
    }
}
