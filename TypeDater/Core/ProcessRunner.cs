using System.Diagnostics;

namespace TypeDater.Core
{
    public record ProcessResult(int ExitCode, string Output, string Error)
    {
        public bool Success { get { return ExitCode == 0; } }
    }

    // Runs external commands, mirroring their output to the log and an optional live callback.
    public class ProcessRunner
    {
        private readonly Logger _logger;

        public ProcessRunner(Logger logger)
        {
            _logger = logger;
        }

        public async Task<ProcessResult> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory = null, Action<string>? onOutput = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            if (workingDirectory != null) startInfo.WorkingDirectory = workingDirectory;

            _logger.Log($"> {fileName} {string.Join(" ", startInfo.ArgumentList)}");

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null) return new ProcessResult(-1, "", $"Could not start {fileName}");
                process.StandardInput.Close();

                var output = new List<string>();
                var error = new List<string>();
                var outputTask = ReadLines(process.StandardOutput, output, onOutput);
                var errorTask = ReadLines(process.StandardError, error, onOutput);

                await process.WaitForExitAsync();
                await Task.WhenAll(outputTask, errorTask);

                if (process.ExitCode != 0)
                {
                    _logger.Log($"  exited with {process.ExitCode}");
                }
                return new ProcessResult(process.ExitCode, string.Join(Environment.NewLine, output), string.Join(Environment.NewLine, error));
            }
            catch (Exception e)
            {
                _logger.Log(e, $"running {fileName}");
                return new ProcessResult(-1, "", e.Message);
            }
        }

        public async Task<bool> Exists(string fileName, params string[] arguments)
        {
            return (await Run(fileName, arguments)).Success;
        }

        private async Task ReadLines(StreamReader reader, List<string> lines, Action<string>? onOutput)
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lines.Add(line);
                if (line.Trim().Length == 0) continue;
                _logger.Log($"  {line}");
                try { onOutput?.Invoke(line); } catch { }
            }
        }
    }
}
