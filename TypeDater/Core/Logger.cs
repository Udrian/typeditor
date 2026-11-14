using System.Text;

namespace TypeDater.Core
{
    // Appends timestamped lines to typedater.log next to the executable,
    // falling back to the local application data folder when that is not writable.
    public class Logger
    {
        private readonly object _lock = new();
        private readonly string? _logPath;

        public string? LogPath { get { return _logPath; } }

        public Logger(string preferredDirectory)
        {
            _logPath = TryOpen(Path.Combine(preferredDirectory, "typedater.log"))
                    ?? TryOpen(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypeDater", "typedater.log"));
        }

        private static string? TryOpen(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, "");
                return path;
            }
            catch
            {
                return null;
            }
        }

        public void Log(string message)
        {
            if (_logPath == null) return;
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}", Encoding.UTF8);
                }
                catch { }
            }
        }

        public void Log(Exception e, string context)
        {
            Log($"ERROR {context}: {e}");
        }
    }
}
