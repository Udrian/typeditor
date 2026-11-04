using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using TypeD.Models.Data;
using TypeD.Models.Data.SaveContexts;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.Models
{
    /// <summary>Loads, stores and applies the project specific window icon and the splash screen shown while a project opens.</summary>
    public static class BrandingManager
    {
        public const string IconKind = "Icon";
        public const string SplashKind = "Splash";
        public static readonly string[] ImageExtensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico" };

        private static readonly Uri DefaultIconUri = new("avares://TypeDitor/Branding/DefaultIcon.png");
        private static readonly Uri DefaultSplashUri = new("avares://TypeDitor/Branding/DefaultSplash.png");
        private static Style IconStyle;

        public static string BrandingFolder(string projectLocation) => Path.Combine(projectLocation, "TypeO", "Branding");

        // Settings
        public static BrandingSettingContext Load(string projectLocation)
        {
            try
            {
                return BrandingSettingContext.LoadWorkspace(projectLocation);
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                ViewModelBase.ResourceModel?.Get<ILogModel>()?.Log($"Could not load workspace branding '{projectLocation}': {e}");
                return new BrandingSettingContext();
            }
        }

        public static async Task Save(Project project, BrandingSettingContext branding)
        {
            var resources = ViewModelBase.ResourceModel;
            var settings = resources.Get<ISettingModel>();
            var context = settings.GetContext<BrandingSettingContext>(SettingLevel.Project);
            context.Icon.Value = branding.Icon.Value;
            context.Splash.Value = branding.Splash.Value;
            settings.SetContext(context, SettingLevel.Project);

            // Branding must be available to the splash loader on the next project launch.
            var save = new SettingSaveContext();
            save.Init(resources);
            save.Settings.Add(context);
            await save.SaveAction(project);
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

        public static Bitmap LoadSplash(string projectLocation) => LoadBitmap(Resolve(projectLocation, Load(projectLocation).Splash.Value), true);

        public static Bitmap LoadIcon(string projectLocation) => LoadBitmap(Resolve(projectLocation, Load(projectLocation).Icon.Value), false);

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
