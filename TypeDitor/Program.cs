using Avalonia;
using Avalonia.OpenGL;
using System;
using System.Diagnostics;
using System.IO;

namespace TypeDitor
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            LaunchTypeDater();
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Silently runs TypeDater in update mode when it is installed next to TypeDitor.
        // TypeDater exits on its own when there is no newer version, and closes us by pid when an update is accepted.
        private static void LaunchTypeDater()
        {
            try
            {
                var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                var typeDaterPath = Path.Combine(exeDirectory, OperatingSystem.IsWindows() ? "TypeDater.exe" : "TypeDater");
                if (!File.Exists(typeDaterPath)) return;

                var startInfo = new ProcessStartInfo
                {
                    FileName = typeDaterPath,
                    WorkingDirectory = exeDirectory,
                    UseShellExecute = true
                };
                startInfo.ArgumentList.Add("--pid");
                startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
                Process.Start(startInfo);
            }
            catch
            {
                // Updating is best effort; never block the editor from starting.
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Wgl], WglProfiles = [new(GlProfileType.OpenGL, 4, 6)] })
                .LogToTrace();
    }
}
