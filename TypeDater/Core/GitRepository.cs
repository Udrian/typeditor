namespace TypeDater.Core
{
    // Talks to the TypeDitor GitHub repository through the git command line.
    public class GitRepository
    {
        public const string RepositoryUrl = "https://github.com/Udrian/typeditor.git";

        private readonly ProcessRunner _runner;

        public GitRepository(ProcessRunner runner)
        {
            _runner = runner;
        }

        // Highest semantic version tag on the remote, e.g. "1.2.0". Non-version tags such as "0.1.0.dev" are ignored.
        public async Task<Version?> GetLatestTag()
        {
            var result = await _runner.Run("git", new[] { "ls-remote", "--tags", "--refs", RepositoryUrl });
            if (!result.Success) return null;

            Version? latest = null;
            foreach (var line in result.Output.Split('\n'))
            {
                var index = line.IndexOf("refs/tags/", StringComparison.Ordinal);
                if (index < 0) continue;
                var tag = line.Substring(index + "refs/tags/".Length).Trim();
                if (Version.TryParse(tag, out var version) && (latest == null || version > latest))
                {
                    latest = version;
                }
            }
            return latest;
        }

        // Shallow clone of a tag including the TypeR submodule.
        public async Task<bool> CloneTag(string tag, string destination, Action<string>? onOutput = null)
        {
            var result = await _runner.Run("git", new[]
            {
                "clone",
                "--depth", "1",
                "--branch", tag,
                "--recurse-submodules",
                "--shallow-submodules",
                RepositoryUrl,
                destination
            }, onOutput: onOutput);
            return result.Success;
        }
    }
}
