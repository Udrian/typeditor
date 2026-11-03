using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TypeDitor.Models;

namespace TypeDitor.View.Dialogs.Tools
{
    /// <summary>Dialog to choose light or dark mode and the color scheme, for the editor and for a single project, and to create custom schemes.</summary>
    public partial class ThemeDialog : Window
    {
        public sealed class SchemeItem
        {
            public ThemeScheme Scheme { get; }
            public string Label => Scheme.IsBuiltIn ? Scheme.Name : $"{Scheme.Name} (custom)";
            public IBrush AccentBrush => new SolidColorBrush(ThemeManager.Resolve(Scheme.Light, false).Accent);
            public IBrush LightBrush => new SolidColorBrush(ThemeManager.Resolve(Scheme.Light, false).Background);
            public IBrush DarkBrush => new SolidColorBrush(ThemeManager.Resolve(Scheme.Dark, true).Background);

            public SchemeItem(ThemeScheme scheme)
            {
                Scheme = scheme;
            }
        }

        private static readonly (string Key, string Label, string Hint)[] ColorFields =
        {
            ("Accent", "Accent", "Buttons, selections and active tabs"),
            ("Background", "Window background", "The window and workspace"),
            ("Panel", "Panels", "Dock panels and sidebars"),
            ("Surface", "Surfaces", "Text boxes, lists and the editor area"),
            ("Header", "Headers", "Menus, toolbars and panel headers"),
            ("Text", "Text", "Normal text"),
            ("MutedText", "Muted text", "Secondary text and hints"),
            ("Border", "Borders", "Borders and separators"),
            ("Hover", "Hover and selection", "Hovered and selected items"),
        };

        private readonly Dictionary<(bool Dark, string Key), ColorPicker> Pickers = new();
        private readonly string ProjectLocation;
        private ProjectBranding Branding;
        // The image chosen in the dialog for a branding kind, null for the default; only kinds in here have been changed
        private readonly Dictionary<string, string> PendingBranding = new();
        private readonly ThemePreference Global;
        private ThemePreference Project;
        private readonly ThemePreference OriginalEffective;
        private bool Loading;
        private bool Saved;

        public ThemeDialog() : this(null) { }

        public ThemeDialog(string projectLocation)
        {
            InitializeComponent();
            ProjectLocation = projectLocation;
            Global = ThemeManager.LoadGlobal();
            Project = string.IsNullOrEmpty(projectLocation) ? null : ThemeManager.LoadProject(projectLocation);
            OriginalEffective = ThemeManager.Effective(Global, Project);

            BuildPickers(false, LightColors);
            BuildPickers(true, DarkColors);

            if (string.IsNullOrEmpty(projectLocation))
            {
                ScopeBox.IsEnabled = false;
            }

            ReloadSchemes();
            ScopeBox.SelectedIndex = 0;
            LoadBranding();
            Closed += (_, _) =>
            {
                if (!Saved)
                {
                    ThemeManager.Apply(OriginalEffective, ProjectLocation);
                    if (HasProject) BrandingManager.ApplyIcon(ProjectLocation);
                }
                (IconPreview.Source as IDisposable)?.Dispose();
                (SplashPreview.Source as IDisposable)?.Dispose();
            };
        }

        private bool HasProject => !string.IsNullOrEmpty(ProjectLocation);

        private bool ProjectScope => ScopeBox.SelectedIndex == 1;

        private ThemePreference Current => ProjectScope ? Project : Global;

        private SchemeItem SelectedScheme => SchemeBox.SelectedItem as SchemeItem;

        private void BuildPickers(bool dark, Panel host)
        {
            foreach (var (key, label, hint) in ColorFields)
            {
                var picker = new ColorPicker();
                Pickers[(dark, key)] = picker;

                var text = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = label });
                text.Children.Add(new TextBlock { Text = hint, FontSize = 11, Opacity = 0.65 });

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(text);
                Grid.SetColumn(picker, 1);
                row.Children.Add(picker);
                host.Children.Add(row);
            }
        }

        private void ReloadSchemes(string select = null)
        {
            Loading = true;
            SchemeBox.ItemsSource = ThemeManager.Schemes(ProjectLocation).Select(s => new SchemeItem(s)).ToList();
            Loading = false;
            if (select != null) SelectScheme(select);
        }

        private void SelectScheme(string name)
        {
            var items = (IEnumerable<SchemeItem>)SchemeBox.ItemsSource;
            SchemeBox.SelectedItem = items.FirstOrDefault(i => string.Equals(i.Scheme.Name, name, StringComparison.OrdinalIgnoreCase)) ?? items.First();
        }

        // Fills the editor with the colors of a scheme so it can be used as a starting point
        private void PopulateEditor(ThemeScheme scheme)
        {
            if (scheme == null) return;

            var wasLoading = Loading;
            Loading = true;
            foreach (var dark in new[] { false, true })
            {
                var c = ThemeManager.Resolve(dark ? scheme.Dark : scheme.Light, dark);
                Pickers[(dark, "Accent")].Color = c.Accent;
                Pickers[(dark, "Background")].Color = c.Background;
                Pickers[(dark, "Panel")].Color = c.Panel;
                Pickers[(dark, "Surface")].Color = c.Surface;
                Pickers[(dark, "Header")].Color = c.Header;
                Pickers[(dark, "Text")].Color = c.Text;
                Pickers[(dark, "MutedText")].Color = c.MutedText;
                Pickers[(dark, "Border")].Color = c.Border;
                Pickers[(dark, "Hover")].Color = c.Hover;
            }
            RadiusBox.Value = scheme.CornerRadius ?? 3;
            FontBox.Text = scheme.FontFamily ?? string.Empty;
            CustomName.Text = scheme.IsBuiltIn ? $"My {scheme.Name}" : scheme.Name;
            DeleteButton.IsEnabled = !scheme.IsBuiltIn;
            Loading = wasLoading;
        }

        // Updates the controls to the preference of the selected scope
        private void ShowScope()
        {
            Loading = true;
            var project = ProjectScope;
            OverrideCheck.IsVisible = project;
            OverrideCheck.IsChecked = project && Project != null;
            ChoicePanel.IsEnabled = !project || Project != null;

            var preference = Current ?? ThemeManager.Effective(Global, null);
            ModeBox.SelectedIndex = Math.Max(0, Array.IndexOf(ThemeManager.Modes, preference.Mode));
            SelectScheme(preference.Scheme);
            ScopeHint.Text = project
                ? "Switches to this theme whenever the project is loaded."
                : Project != null ? "This project has its own theme, so changes here are not visible while it is open." : "Applied when the editor starts and for projects without their own theme.";
            Loading = false;

            PopulateEditor(SelectedScheme?.Scheme);
            Preview();
        }

        private void OnScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Loading && ScopeBox != null && ModeBox != null) ShowScope();
        }

        private void OnOverrideChanged(object sender, RoutedEventArgs e)
        {
            if (Loading) return;
            Project = OverrideCheck.IsChecked == true
                ? new ThemePreference { Mode = ThemeManager.Modes[Math.Max(0, ModeBox.SelectedIndex)], Scheme = SelectedScheme?.Scheme.Name }
                : null;
            ChoicePanel.IsEnabled = Project != null;
            Preview();
        }

        private void OnModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Loading || Current == null) return;

            Current.Mode = ThemeManager.Modes[Math.Max(0, ModeBox.SelectedIndex)];
            Preview();
        }

        private void OnSchemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Loading || SelectedScheme == null) return;

            PopulateEditor(SelectedScheme.Scheme);
            if (Current == null) return;

            Current.Scheme = SelectedScheme.Scheme.Name;
            Preview();
        }

        private void Preview()
        {
            ThemeManager.Apply(ThemeManager.Effective(Global, Project), ProjectLocation);
        }

        private ThemeColors ReadColors(bool dark)
        {
            string Get(string key) => Pickers[(dark, key)].Color.ToString();
            return new ThemeColors
            {
                Accent = Get("Accent"), Background = Get("Background"), Panel = Get("Panel"), Surface = Get("Surface"), Header = Get("Header"),
                Text = Get("Text"), MutedText = Get("MutedText"), Border = Get("Border"), Hover = Get("Hover")
            };
        }

        private void OnSaveCustom(object sender, RoutedEventArgs e)
        {
            var name = CustomName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                CustomError.Text = "Enter a name for the theme.";
                return;
            }
            if (ThemeManager.BuiltInSchemes.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                CustomError.Text = "That name is used by a built in theme.";
                return;
            }

            var scheme = new ThemeScheme
            {
                Name = name,
                Light = ReadColors(false),
                Dark = ReadColors(true),
                CornerRadius = (int)(RadiusBox.Value ?? 3),
                FontFamily = string.IsNullOrWhiteSpace(FontBox.Text) ? null : FontBox.Text.Trim()
            };

            try
            {
                ThemeManager.SaveCustom(scheme);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CustomError.Text = $"Could not save the theme: {ex.Message}";
                return;
            }

            CustomError.Text = string.Empty;
            ReloadSchemes(name);
        }

        private void OnDeleteCustom(object sender, RoutedEventArgs e)
        {
            if (SelectedScheme is not { Scheme.IsBuiltIn: false } item) return;

            ThemeManager.DeleteCustom(item.Scheme.Name);
            foreach (var preference in new[] { Global, Project }.Where(p => p != null && string.Equals(p.Scheme, item.Scheme.Name, StringComparison.OrdinalIgnoreCase)))
            {
                preference.Scheme = ThemeManager.DefaultScheme;
            }
            ReloadSchemes(Current?.Scheme ?? ThemeManager.DefaultScheme);
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(ThemeManager.UserThemesFolder);
            Process.Start(new ProcessStartInfo { FileName = ThemeManager.UserThemesFolder, UseShellExecute = true });
        }

        // Branding
        private void LoadBranding()
        {
            BrandingPanel.IsVisible = HasProject;
            if (!HasProject) return;

            Branding = BrandingManager.Load(ProjectLocation);
            ShowBranding(BrandingManager.IconKind);
            ShowBranding(BrandingManager.SplashKind);
        }

        // The full path of the image currently chosen for a kind, null for the default
        private string BrandingFile(string kind)
        {
            if (PendingBranding.TryGetValue(kind, out var pending)) return pending;
            return BrandingManager.Resolve(ProjectLocation, kind == BrandingManager.IconKind ? Branding.Icon : Branding.Splash);
        }

        private void ShowBranding(string kind)
        {
            var file = BrandingFile(kind);
            var splash = kind == BrandingManager.SplashKind;
            var bitmap = BrandingManager.LoadBitmap(file, splash);
            var image = splash ? SplashPreview : IconPreview;
            (image.Source as IDisposable)?.Dispose();
            image.Source = bitmap;

            (splash ? SplashSourceText : IconSourceText).Text = file == null ? "Default" : Path.GetFileName(file);
            (splash ? SplashDefaultButton : IconDefaultButton).IsEnabled = file != null;
            if (!splash) BrandingManager.ApplyIcon(BrandingManager.LoadBitmap(file, false));
        }

        private async void ChooseBranding(string kind)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = kind == BrandingManager.IconKind ? "Choose the window icon" : "Choose the splash screen image",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Images") { Patterns = BrandingManager.ImageExtensions }]
            });
            var file = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (file == null) return;

            if (!BrandingManager.IsImageFile(file))
            {
                ShowBrandingError("Choose a png, jpg, bmp or ico image.");
                return;
            }
            ShowBrandingError(null);
            PendingBranding[kind] = file;
            ShowBranding(kind);
        }

        private void ShowBrandingError(string message)
        {
            BrandingError.Text = message ?? string.Empty;
            BrandingError.IsVisible = !string.IsNullOrEmpty(message);
        }

        private void OnChooseIcon(object sender, RoutedEventArgs e) => ChooseBranding(BrandingManager.IconKind);

        private void OnChooseSplash(object sender, RoutedEventArgs e) => ChooseBranding(BrandingManager.SplashKind);

        private void OnDefaultIcon(object sender, RoutedEventArgs e)
        {
            PendingBranding[BrandingManager.IconKind] = null;
            ShowBranding(BrandingManager.IconKind);
        }

        private void OnDefaultSplash(object sender, RoutedEventArgs e)
        {
            PendingBranding[BrandingManager.SplashKind] = null;
            ShowBranding(BrandingManager.SplashKind);
        }

        private void SaveBranding()
        {
            foreach (var (kind, file) in PendingBranding)
            {
                string relative = null;
                if (file == null) BrandingManager.Remove(ProjectLocation, kind);
                else relative = BrandingManager.Import(ProjectLocation, file, kind);

                if (kind == BrandingManager.IconKind) Branding.Icon = relative;
                else Branding.Splash = relative;
            }
            BrandingManager.Save(ProjectLocation, Branding);
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (HasProject && PendingBranding.Count > 0)
            {
                try
                {
                    SaveBranding();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ShowBrandingError($"Could not save the project branding: {ex.Message}");
                    return;
                }
            }

            ThemeManager.SaveGlobal(Global);
            if (!string.IsNullOrEmpty(ProjectLocation))
            {
                ThemeManager.SaveProject(ProjectLocation, Project);
            }
            Saved = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
