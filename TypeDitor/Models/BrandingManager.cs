using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace TypeDitor.Models
{
    /// <summary>The icon and splash screen of a project, as paths relative to the project folder. Null uses the default.</summary>
    public class ProjectBranding
    {
        public string Icon { get; set; }
        public string Splash { get; set; }
    }

    /// <summary>Loads, stores and applies the project specific window icon and the splash screen shown while a project opens.</summary>
    public static class BrandingManager
    {
        public const string IconKind = "Icon";
        public const string SplashKind = "Splash";
        public static readonly string[] ImageExtensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico" };

        private static readonly Uri DefaultIconUri = new("avares://TypeDitor/Branding/DefaultIcon.png");
        private static readonly Uri DefaultSplashUri = new("avares://TypeDitor/Branding/DefaultSplash.png");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        private static Style IconStyle;

        private static string SettingsFile(string projectLocation) => Path.Combine(projectLocation, "TypeO", "Settings", "Branding.json");
        public static string BrandingFolder(string projectLocation) => Path.Combine(projectLocation, "TypeO", "Branding");

        // Settings
        public static ProjectBranding Load(string projectLocation)
        {
            if (string.IsNullOrEmpty(projectLocation)) return new ProjectBranding();
            try
            {
                var file = SettingsFile(projectLocation);
                return (File.Exists(file) ? JsonSerializer.Deserialize<ProjectBranding>(File.ReadAllText(file)) : null) ?? new ProjectBranding();
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                return new ProjectBranding();
            }
        }

        public static void Save(string projectLocation, ProjectBranding branding)
        {
            var file = SettingsFile(projectLocation);
            if (branding == null || (branding.Icon == null && branding.Splash == null))
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonSerializer.Serialize(branding, JsonOptions));
        }

        /// <summary>
        /// Copies an image into the branding folder of the project, so it travels with the project, and returns its path relative to the project.
        /// Earlier images of the same kind are removed.
        /// </summary>
        public static string Import(string projectLocation, string sourceFile, string kind)
        {
            var folder = BrandingFolder(projectLocation);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, kind + Path.GetExtension(sourceFile).ToLowerInvariant());
            if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                var bytes = File.ReadAllBytes(sourceFile);
                Remove(projectLocation, kind);
                File.WriteAllBytes(target, bytes);
            }
            return Path.GetRelativePath(projectLocation, target);
        }

        /// <summary>Removes the imported images of a kind from the branding folder.</summary>
        public static void Remove(string projectLocation, string kind)
        {
            var folder = BrandingFolder(projectLocation);
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, kind + ".*"))
            {
                File.Delete(file);
            }
        }

        /// <summary>The full path of a branding image of the project, or null if it isn't set or is missing.</summary>
        public static string Resolve(string projectLocation, string relativePath)
        {
            if (string.IsNullOrEmpty(projectLocation) || string.IsNullOrEmpty(relativePath)) return null;
            var path = Path.GetFullPath(Path.Combine(projectLocation, relativePath));
            return File.Exists(path) ? path : null;
        }

        // Images
        /// <summary>Loads an image file without keeping it locked, falling back to the default image if it can't be read.</summary>
        public static Bitmap LoadBitmap(string file, bool splash)
        {
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    using var stream = new MemoryStream(File.ReadAllBytes(file));
                    return new Bitmap(stream);
                }
                catch (Exception)
                {
                    // Unreadable or unsupported image, the default is used instead
                }
            }
            using var asset = AssetLoader.Open(splash ? DefaultSplashUri : DefaultIconUri);
            return new Bitmap(asset);
        }

        public static Bitmap LoadSplash(string projectLocation) => LoadBitmap(Resolve(projectLocation, Load(projectLocation).Splash), true);

        public static Bitmap LoadIcon(string projectLocation) => LoadBitmap(Resolve(projectLocation, Load(projectLocation).Icon), false);

        // Applying
        /// <summary>Uses the icon of the project, or the default icon when the project has none or no project is given, for all editor windows.</summary>
        public static void ApplyIcon(string projectLocation) => ApplyIcon(LoadIcon(projectLocation));

        public static void ApplyIcon(Bitmap bitmap)
        {
            var app = Application.Current;
            if (app == null) return;

            var icon = new WindowIcon(bitmap);
            // A style applies to windows opened later as well; it is replaced rather than changed so open windows pick up the new icon
            var style = new Style(x => x.Is<Window>());
            style.Setters.Add(new Setter(Window.IconProperty, icon));
            var index = IconStyle == null ? -1 : app.Styles.IndexOf(IconStyle);
            if (index >= 0) app.Styles[index] = style;
            else app.Styles.Add(style);
            IconStyle = style;
        }

        public static bool IsImageFile(string file)
        {
            var extension = Path.GetExtension(file);
            return ImageExtensions.Any(e => string.Equals(e.TrimStart('*'), extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
