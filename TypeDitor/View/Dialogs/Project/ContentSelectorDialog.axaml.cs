using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// A content browser limited to the given file extensions, used to pick the content for a content property.
    /// </summary>
    public partial class ContentSelectorDialog : Window, INotifyPropertyChanged
    {
        /// <summary>Represents a content file shown in the selection dialog.</summary>
        public class Item : INotifyPropertyChanged
        {
            private Bitmap _thumbnail;

            /// <summary>Raised when a displayed item property changes.</summary>
            public event PropertyChangedEventHandler PropertyChanged;
            /// <summary>Gets the file name displayed in the list.</summary>
            public string Title { get; init; }
            /// <summary>Gets the file path relative to the project's content directory.</summary>
            public string RelativePath { get; init; }
            /// <summary>Gets or sets the preview image for the content file.</summary>
            public Bitmap Thumbnail
            {
                get => _thumbnail;
                set
                {
                    _thumbnail = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                }
            }
        }

        private readonly List<Item> _items = new();
        private string _searchText = string.Empty;

        /// <summary>Gets the content items currently shown in the dialog.</summary>
        public ObservableCollection<Item> FilteredItems { get; } = new();

        /// <summary>
        /// The chosen path relative to the content folder, or null if the dialog was cancelled.
        /// </summary>
        public string SelectedPath { get; private set; }

        /// <summary>Creates a content selection dialog filtered to the specified file extensions.</summary>
        public ContentSelectorDialog(TypeD.Models.Data.Project project, IEnumerable<string> extensions)
        {
            InitializeComponent();
            DataContext = this;

            Opened += (_, _) => tbFilter.Focus();
            // Tunnel so the TextBox doesn't consume Up/Down first.
            tbFilter.AddHandler(KeyDownEvent, tbFilter_KeyDown, RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Close(false);
            }, RoutingStrategies.Tunnel);

            Load(project, extensions.Select(e => e.ToLowerInvariant()).ToHashSet());
        }

        private async void Load(TypeD.Models.Data.Project project, HashSet<string> extensions)
        {
            if (!Directory.Exists(project.ProjectContentPath))
                return;

            var contentProvider = ViewModelBase.ResourceModel.Get<IContentProvider>();
            var files = Directory.EnumerateFiles(project.ProjectContentPath, "*", SearchOption.AllDirectories)
                .Where(file => extensions.Contains(Path.GetExtension(file).TrimStart('.').ToLowerInvariant()))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                var item = new Item
                {
                    Title = Path.GetFileName(file),
                    RelativePath = Path.GetRelativePath(project.ProjectContentPath, file).Replace('\\', '/')
                };
                _items.Add(item);
            }
            UpdateFilter();

            foreach (var (file, item) in files.Zip(_items))
            {
                try
                {
                    item.Thumbnail = (await contentProvider.GetContentAsync(file)).Thumbnail;
                }
                catch (Exception)
                {
                }
            }
        }

        private void tbFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = tbFilter.Text;
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            var query = _searchText?.Trim();
            var matches = string.IsNullOrEmpty(query)
                ? _items
                : _items.Where(i => i.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredItems.Clear();
            foreach (var item in matches)
                FilteredItems.Add(item);
            SelectFirst();
        }

        private void SelectFirst()
        {
            if (FilteredItems.Count > 0)
                lbContent.SelectedIndex = 0;
        }

        private void MoveSelection(int delta)
        {
            if (FilteredItems.Count == 0) return;
            var index = Math.Clamp(lbContent.SelectedIndex + delta, 0, FilteredItems.Count - 1);
            lbContent.SelectedIndex = index;
            lbContent.ScrollIntoView(index);
        }

        private void tbFilter_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Right when e.KeyModifiers == KeyModifiers.Control:
                    MoveSelection(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                case Key.Left when e.KeyModifiers == KeyModifiers.Control:
                    MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    e.Handled = Confirm();
                    break;
            }
        }

        private void lbContent_DoubleTapped(object sender, TappedEventArgs e)
        {
            Confirm();
        }

        private bool Confirm()
        {
            if (lbContent.SelectedItem is not Item item)
                return false;
            SelectedPath = item.RelativePath;
            Close(true);
            return true;
        }
    }
}
