using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TypeDitor.Models
{
    /// <summary>
    /// The colors of a scheme in one mode. Only the colors that are set are used, the rest are derived from
    /// <see cref="Background"/>, <see cref="Text"/> and <see cref="Accent"/>. Colors are written as <c>#rrggbb</c>.
    /// </summary>
    public class ThemeColors
    {
        /// <summary>Buttons, selections, active tabs and links.</summary>
        public string Accent { get; set; }
        /// <summary>The window and workspace background.</summary>
        public string Background { get; set; }
        /// <summary>Dock panels and sidebars.</summary>
        public string Panel { get; set; }
        /// <summary>Text boxes, lists and the editor area.</summary>
        public string Surface { get; set; }
        /// <summary>Menus, toolbars, flyouts and panel headers.</summary>
        public string Header { get; set; }
        /// <summary>Normal text.</summary>
        public string Text { get; set; }
        /// <summary>Secondary text, hints and disabled content.</summary>
        public string MutedText { get; set; }
        /// <summary>Borders and separators.</summary>
        public string Border { get; set; }
        /// <summary>Hovered and selected items.</summary>
        public string Hover { get; set; }

        public IEnumerable<string> All() => new[] { Accent, Background, Panel, Surface, Header, Text, MutedText, Border, Hover };
    }

    /// <summary>A color scheme with a light and a dark variant, plus shape and font.</summary>
    public class ThemeScheme
    {
        public string Name { get; set; } = "";
        public ThemeColors Light { get; set; }
        public ThemeColors Dark { get; set; }
        /// <summary>Corner radius of controls, null keeps the default.</summary>
        public int? CornerRadius { get; set; }
        /// <summary>Font family of the editor, null keeps the default.</summary>
        public string FontFamily { get; set; }

        // Only read from themes made before the schemes had full light and dark colors
        public string Accent { get; set; }
        public string DarkAccent { get; set; }
        public string LightBackground { get; set; }
        public string DarkBackground { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }
    }

    /// <summary>The colors of a scheme in one mode, all resolved.</summary>
    public record ResolvedColors(Color Accent, Color Background, Color Panel, Color Surface, Color Header, Color Text, Color MutedText, Color Border, Color Hover);

    /// <summary>The chosen mode and scheme; a null value means "not specified", so the editor default is used.</summary>
    public class ThemePreference
    {
        /// <summary>System, Light or Dark.</summary>
        public string Mode { get; set; }
        public string Scheme { get; set; }
    }

    /// <summary>Loads, stores and applies the editor theme, both the editor default and the per project override.</summary>
    public static class ThemeManager
    {
        public const string DefaultMode = "System";
        public const string DefaultScheme = "Grape Soda";
        public static readonly string[] Modes = { "System", "Light", "Dark" };

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        private static bool WatchingSystemVariant;
        private static ThemeScheme AppliedScheme;
        private static string AppliedMode;

        public static string UserRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypeO");
        public static string UserThemesFolder => Path.Combine(UserRoot, "Themes");
        private static string GlobalFile => Path.Combine(UserRoot, "Settings", "Theme.json");
        private static string ProjectFile(string projectLocation) => Path.Combine(projectLocation, "TypeO", "Settings", "Theme.json");
        private static string ProjectThemesFolder(string projectLocation) => Path.Combine(projectLocation, "TypeO", "Themes");

        private static ThemeColors C(string accent, string background, string panel, string surface, string header, string text, string muted, string border, string hover)
        {
            return new ThemeColors { Accent = accent, Background = background, Panel = panel, Surface = surface, Header = header, Text = text, MutedText = muted, Border = border, Hover = hover };
        }

        private static ThemeScheme S(string name, int radius, string font, ThemeColors light, ThemeColors dark)
        {
            return new ThemeScheme { Name = name, CornerRadius = radius, FontFamily = font, Light = light, Dark = dark, IsBuiltIn = true };
        }

        public static readonly IReadOnlyList<ThemeScheme> BuiltInSchemes = new List<ThemeScheme>
        {
            // The original purple look
            S("Grape Soda", 3, null,
                C("#8961cc", "#fef6ff", "#f8ecfc", "#ffffff", "#fbe4ff", "#2b1a3a", "#7b5890", "#eeceff", "#fbe4ff"),
                C("#a07ae0", "#262738", "#2c2d42", "#1b1b2a", "#3f2e4b", "#f3ecff", "#b6aabc", "#64576b", "#3f2e4b")),
            // Neon on near black, sharp edges
            S("Midnight Arcade", 0, null,
                C("#d100a8", "#f1f0ff", "#e6e4ff", "#ffffff", "#dcd9ff", "#14123a", "#5a5890", "#b9b5f0", "#d6d2ff"),
                C("#ff2bd6", "#0b0b1e", "#12122e", "#070714", "#1a1a40", "#e8f0ff", "#7f8cc4", "#2d2d66", "#25255a")),
            // Warm browns and cream, soft and rounded
            S("Cozy Cabin", 8, null,
                C("#c2631a", "#f6ead9", "#efdfc7", "#fff8ec", "#e6d2b3", "#3b2a1c", "#85684d", "#d2b894", "#ead7b8"),
                C("#e08a3c", "#2a211a", "#31271f", "#1f1813", "#3d3025", "#f2e4d0", "#b39a7e", "#55442f", "#45372a")),
            // Fresh greens, very round
            S("Mint Condition", 12, null,
                C("#00a87e", "#eefaf5", "#e0f5ed", "#ffffff", "#cfeee2", "#0f3b2e", "#4f8a77", "#b5e2d2", "#d3f0e4"),
                C("#2ee6b0", "#0f2a26", "#123330", "#0a1f1c", "#17403b", "#dffaf1", "#7fb8a8", "#25554d", "#1d4a44")),
            // Plain high contrast ink on paper
            S("Paper Plane", 2, null,
                C("#1f4fd8", "#ffffff", "#f7f7f5", "#ffffff", "#efefeb", "#111111", "#666666", "#d0d0cc", "#e9edfb"),
                C("#6e93ff", "#1a1a1a", "#202020", "#141414", "#2a2a2a", "#f2f2f2", "#9a9a9a", "#3a3a3a", "#2b3350")),
            // Pink and plum
            S("Dragon Fruit", 10, null,
                C("#e4007c", "#fff2f8", "#ffe3f0", "#fffafc", "#ffd0e5", "#4a0d2e", "#a14a78", "#f5b5d4", "#ffd9ea"),
                C("#ff4fa3", "#2a0f20", "#35142a", "#1f0a17", "#4a1a3a", "#ffe8f3", "#c98ab0", "#6b2b55", "#5a2147")),
            // Phosphor green terminal with a monospace font
            S("Green Screen", 0, "Consolas, Menlo, monospace",
                C("#0a8a2e", "#eaffee", "#d9f7df", "#f6fff8", "#c6eed0", "#06330f", "#3f8a54", "#a6dfb4", "#c9f0d3"),
                C("#33ff66", "#000000", "#07110a", "#000000", "#0d1f13", "#33ff66", "#1fa046", "#145a2a", "#0f3a1d")),
        };

        // Schemes
        /// <summary>Lists the built in schemes followed by the custom schemes of the user and, if given, the project.</summary>
        public static List<ThemeScheme> Schemes(string projectLocation = null)
        {
            var schemes = new List<ThemeScheme>(BuiltInSchemes);
            var folders = new List<string> { UserThemesFolder };
            if (!string.IsNullOrEmpty(projectLocation)) folders.Add(ProjectThemesFolder(projectLocation));

            foreach (var folder in folders.Where(Directory.Exists))
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.json").OrderBy(f => f))
                {
                    var scheme = Read<ThemeScheme>(file);
                    if (scheme == null || string.IsNullOrWhiteSpace(scheme.Name)) continue;
                    Normalize(scheme);
                    if (!IsValid(scheme)) continue;
                    if (schemes.Any(s => string.Equals(s.Name, scheme.Name, StringComparison.OrdinalIgnoreCase))) continue;
                    schemes.Add(scheme);
                }
            }
            return schemes;
        }

        // Moves the accent and backgrounds of older theme files into the light and dark colors
        private static void Normalize(ThemeScheme scheme)
        {
            scheme.Light ??= new ThemeColors { Accent = scheme.Accent, Background = scheme.LightBackground };
            scheme.Dark ??= new ThemeColors { Accent = scheme.DarkAccent ?? scheme.Accent, Background = scheme.DarkBackground };
        }

        public static bool IsValid(ThemeScheme scheme)
        {
            return new[] { scheme.Light, scheme.Dark }.All(c => c != null && c.All().All(v => string.IsNullOrEmpty(v) || Color.TryParse(v, out _)))
                && (scheme.CornerRadius is null or >= 0 and <= 30);
        }

        /// <summary>Saves a custom scheme to the user's themes folder, replacing a custom scheme with the same name.</summary>
        public static void SaveCustom(ThemeScheme scheme)
        {
            Directory.CreateDirectory(UserThemesFolder);
            Write(CustomFile(scheme.Name), scheme);
        }

        public static void DeleteCustom(string name)
        {
            var file = CustomFile(name);
            if (File.Exists(file)) File.Delete(file);
        }

        private static string CustomFile(string name)
        {
            var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(UserThemesFolder, safe + ".json");
        }

        // Colors
        /// <summary>Fills in the colors that a scheme leaves out.</summary>
        public static ResolvedColors Resolve(ThemeColors colors, bool dark)
        {
            static Color? Parse(string value) => Color.TryParse(value, out var color) ? color : null;

            var accent = Parse(colors?.Accent) ?? Color.Parse(dark ? "#ffa07ae0" : "#ff8961cc");
            var background = Parse(colors?.Background) ?? Color.Parse(dark ? "#ff262738" : "#fffef6ff");
            var darkBackground = Luminance(background) < 0.5;
            var text = Parse(colors?.Text) ?? (darkBackground ? Colors.White : Colors.Black);

            return new ResolvedColors(
                accent,
                background,
                Parse(colors?.Panel) ?? background,
                Parse(colors?.Surface) ?? Blend(background, darkBackground ? Colors.Black : Colors.White, 0.5),
                Parse(colors?.Header) ?? Blend(background, text, 0.08),
                text,
                Parse(colors?.MutedText) ?? Blend(background, text, 0.6),
                Parse(colors?.Border) ?? Blend(background, text, 0.2),
                Parse(colors?.Hover) ?? Blend(background, text, 0.12));
        }

        private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;

        private static Color Blend(Color from, Color to, double amount)
        {
            byte Channel(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
            return Color.FromRgb(Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
        }

        // Preferences
        public static ThemePreference LoadGlobal()
        {
            var preference = Read<ThemePreference>(GlobalFile) ?? new ThemePreference();
            preference.Mode ??= DefaultMode;
            preference.Scheme ??= DefaultScheme;
            return preference;
        }

        public static void SaveGlobal(ThemePreference preference) => Write(GlobalFile, preference);

        /// <summary>Loads the theme override of a project, or null if the project uses the editor default.</summary>
        public static ThemePreference LoadProject(string projectLocation)
        {
            var preference = Read<ThemePreference>(ProjectFile(projectLocation));
            return preference == null || (preference.Mode == null && preference.Scheme == null) ? null : preference;
        }

        /// <summary>Saves the theme override of a project; null removes it so the editor default is used.</summary>
        public static void SaveProject(string projectLocation, ThemePreference preference)
        {
            if (preference == null)
            {
                var file = ProjectFile(projectLocation);
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            Write(ProjectFile(projectLocation), preference);
        }

        public static ThemePreference Effective(ThemePreference global, ThemePreference project)
        {
            return new ThemePreference
            {
                Mode = project?.Mode ?? global?.Mode ?? DefaultMode,
                Scheme = project?.Scheme ?? global?.Scheme ?? DefaultScheme
            };
        }

        // Applying
        /// <summary>Applies the editor default theme, used on startup.</summary>
        public static void ApplyGlobal()
        {
            Apply(LoadGlobal(), null);
            BrandingManager.ApplyIcon((string)null);
        }

        /// <summary>Applies the editor default theme with the override of the project on top, and the icon of the project.</summary>
        public static void ApplyProject(string projectLocation)
        {
            Apply(Effective(LoadGlobal(), string.IsNullOrEmpty(projectLocation) ? null : LoadProject(projectLocation)), projectLocation);
            BrandingManager.ApplyIcon(projectLocation);
        }

        public static void Apply(ThemePreference preference, string projectLocation)
        {
            var app = Application.Current;
            if (app == null) return;

            var scheme = Schemes(projectLocation).FirstOrDefault(s => string.Equals(s.Name, preference.Scheme, StringComparison.OrdinalIgnoreCase))
                ?? BuiltInSchemes[0];

            app.RequestedThemeVariant = preference.Mode switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            AppliedScheme = scheme;
            AppliedMode = preference.Mode;
            if (!WatchingSystemVariant)
            {
                WatchingSystemVariant = true;
                app.ActualThemeVariantChanged += (_, _) =>
                {
                    if (AppliedMode == "System" && AppliedScheme != null) ApplyScheme(AppliedScheme);
                };
            }
            ApplyScheme(scheme);
        }

        private static void ApplyScheme(ThemeScheme scheme)
        {
            var app = Application.Current;
            var oldFluent = app.Styles.OfType<FluentTheme>().FirstOrDefault();
            if (oldFluent == null) return;

            var light = Resolve(scheme.Light, false);
            var dark = Resolve(scheme.Dark, true);

            // Changing the palettes of a theme that is in use is not picked up by open windows, so a new theme instance
            // replaces it, which makes Avalonia resolve all theme resources again
            var fluent = new FluentTheme();
            fluent.Palettes[ThemeVariant.Light] = BuildPalette(light);
            fluent.Palettes[ThemeVariant.Dark] = BuildPalette(dark);
            var index = app.Styles.IndexOf(oldFluent);
            app.Styles[index] = fluent;

            // The dock theme has its own brushes that do not follow the palette, so they are set for the variant in use
            ApplyDock(app.Resources, app.ActualThemeVariant == ThemeVariant.Dark ? dark : light);

            if (scheme.CornerRadius.HasValue)
            {
                app.Resources["ControlCornerRadius"] = new CornerRadius(scheme.CornerRadius.Value);
                app.Resources["OverlayCornerRadius"] = new CornerRadius(scheme.CornerRadius.Value + 2);
            }
            else
            {
                app.Resources.Remove("ControlCornerRadius");
                app.Resources.Remove("OverlayCornerRadius");
            }

            if (!string.IsNullOrWhiteSpace(scheme.FontFamily))
            {
                app.Resources["ContentControlThemeFontFamily"] = new FontFamily(scheme.FontFamily);
            }
            else
            {
                app.Resources.Remove("ContentControlThemeFontFamily");
            }
        }

        private static ColorPaletteResources BuildPalette(ResolvedColors c)
        {
            var dark = Luminance(c.Background) < 0.5;
            var black = dark ? Colors.Black : c.Text;
            return new ColorPaletteResources
            {
                Accent = c.Accent,
                RegionColor = c.Background,
                AltHigh = c.Surface, AltMediumHigh = c.Surface, AltMedium = c.Surface, AltMediumLow = c.Surface, AltLow = c.Surface,
                BaseHigh = c.Text,
                BaseMediumHigh = Blend(c.Text, c.MutedText, 0.5),
                BaseMedium = c.MutedText,
                BaseMediumLow = Blend(c.MutedText, c.Border, 0.5),
                BaseLow = c.Border,
                ChromeHigh = c.Border,
                ChromeMedium = c.Header,
                ChromeMediumLow = c.Header,
                ChromeLow = Blend(c.Header, c.Background, 0.5),
                ChromeAltLow = c.MutedText,
                ChromeBlackHigh = dark ? Colors.Black : c.Text,
                ChromeBlackMedium = black,
                ChromeBlackMediumLow = black,
                ChromeBlackLow = c.Border,
                ChromeDisabledHigh = c.Border,
                ChromeDisabledLow = c.MutedText,
                ChromeGray = c.MutedText,
                ChromeWhite = Colors.White,
                ListLow = c.Hover,
                ListMedium = Blend(c.Hover, c.Text, 0.12)
            };
        }

        private static void ApplyDock(IResourceDictionary resources, ResolvedColors c)
        {
            var dark = Luminance(c.Background) < 0.5;
            var accentMedium = Blend(c.Accent, dark ? Colors.White : Colors.Black, dark ? 0.2 : 0.15);
            var accentHigh = Blend(c.Accent, dark ? Colors.White : Colors.Black, dark ? 0.4 : 0.3);
            var onAccent = Luminance(c.Accent) < 0.55 ? Colors.White : Colors.Black;

            void Set(string key, Color color) => resources[key] = new SolidColorBrush(color);

            Set("DockApplicationAccentBrushLow", c.Accent);
            Set("DockApplicationAccentBrushMed", accentMedium);
            Set("DockApplicationAccentBrushHigh", accentHigh);
            Set("DockApplicationAccentForegroundBrush", onAccent);
            Set("DockApplicationAccentBrushIndicator", c.Accent);

            Set("DockThemeBorderLowBrush", c.Border);
            Set("DockThemeBackgroundBrush", c.Background);
            Set("DockThemeAccentBrush", c.Accent);
            Set("DockThemeForegroundBrush", c.Text);
            Set("DockThemeControlBackgroundBrush", c.Surface);

            Set("DockSurfaceWorkbenchBrush", c.Background);
            Set("DockSurfaceSidebarBrush", c.Panel);
            Set("DockSurfaceEditorBrush", c.Surface);
            Set("DockSurfacePanelBrush", c.Panel);
            Set("DockSurfaceHeaderBrush", c.Header);
            Set("DockSurfaceHeaderActiveBrush", c.Accent);

            Set("DockBorderSubtleBrush", c.Border);
            Set("DockBorderStrongBrush", c.Border);
            Set("DockSeparatorBrush", c.Border);
            Set("DockSplitterIdleBrush", c.Border);
            Set("DockSplitterHoverBrush", c.Accent);
            Set("DockSplitterDragBrush", accentMedium);

            Set("DockTabHoverBackgroundBrush", c.Hover);
            Set("DockTabActiveBackgroundBrush", c.Accent);
            Set("DockTabActiveIndicatorBrush", c.Accent);
            Set("DockTabForegroundBrush", c.Text);
            Set("DockTabSelectedForegroundBrush", accentMedium);
            Set("DockTabActiveForegroundBrush", onAccent);
            Set("DockTabCloseHoverBackgroundBrush", accentHigh);
            Set("DockDocumentTabSelectedForegroundBrush", c.Text);
            Set("DockDocumentTabPointerOverForegroundBrush", onAccent);
            Set("DockDocumentTabCloseSelectedForegroundBrush", onAccent);
            Set("DockDocumentTabClosePointerOverForegroundBrush", onAccent);

            Set("DockTargetIndicatorBrush", c.Accent);

            Set("DockChromeButtonHoverBackgroundBrush", c.Hover);
            Set("DockChromeButtonPressedBackgroundBrush", c.Border);
            Set("DockChromeButtonDangerHoverBrush", accentHigh);
        }

        // Files
        private static T Read<T>(string file) where T : class
        {
            try
            {
                return File.Exists(file) ? JsonSerializer.Deserialize<T>(File.ReadAllText(file)) : null;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void Write<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonSerializer.Serialize(value, JsonOptions));
        }
    }
}
