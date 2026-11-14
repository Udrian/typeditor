using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeDater.Core
{
    // typedater.json next to the installed TypeDitor executable.
    public class Manifest
    {
        public const string FileName = "typedater.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string? InstalledVersion { get; set; }
        public string? SkippedVersion { get; set; }

        public static Manifest Load(string directory)
        {
            var path = Path.Combine(directory, FileName);
            try
            {
                if (File.Exists(path))
                {
                    return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), JsonOptions) ?? new Manifest();
                }
            }
            catch { }
            return new Manifest();
        }

        public void Save(string directory)
        {
            File.WriteAllText(Path.Combine(directory, FileName), JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
