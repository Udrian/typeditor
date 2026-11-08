using Avalonia.Controls;
using Avalonia.Threading;
using TypeDCore.Models;
using TypeDitor.View.Dialogs.Project;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TypeD.Models.Interfaces;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TypeD.Models.Data;
using TypeD.Models.Providers.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Panels
{
    internal partial class ContentPanelViewModel : ViewModelBase
    {
        public partial class ImageItem : ViewModelBase
        {
            [ObservableProperty]
            private string _id;
            [ObservableProperty]
            private string _title;
            [ObservableProperty]
            private string _path;
            [ObservableProperty]
            private Bitmap _thumbnail;
            [ObservableProperty]
            private Bitmap _typeIcon;
            [ObservableProperty]
            private string _editName;
            [ObservableProperty]
            private bool _isEditing;

            public Content Content { get; private set; }
            public DateTime LastWrite { get; set; }

            private Avalonia.Controls.Control _toolTipContent;
            public Avalonia.Controls.Control ToolTipContent => _toolTipContent ??= Content?.CreateToolTip();

            public void SetContent(Content content)
            {
                Content = content;
                _toolTipContent = null;
                OnPropertyChanged(nameof(ToolTipContent));
                Title = content.Name;
                Path = content.Path;
                Thumbnail = content.Thumbnail;
            }

            public void BeginRename()
            {
                EditName = Title;
                IsEditing = true;
            }

            public void CancelRename()
            {
                IsEditing = false;
            }
        }

        public List<ImageItem> Items { get; }

        [ObservableProperty]
        private ImageItem _selectedItem;

        public void SetNoTreeActive()
        {
            ResourceModel.Get<INodeSelectionModel>().SetNoTreeActive();
        }

        public async Task<bool> CommitRename(ImageItem item)
        {
            var newName = item.EditName?.Trim();
            item.CancelRename();
            if (string.IsNullOrEmpty(newName) || newName == item.Title)
                return false;

            var extension = System.IO.Path.GetExtension(item.Path);
            var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(item.Path), newName + extension);
            if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || System.IO.File.Exists(newPath))
            {
                ResourceModel.Get<ILogModel>().Log($"Could not rename content to '{newName}': invalid name or the file already exists.");
                return false;
            }

            var oldRelative = ContentReferenceUpdater.ToRelativePath(Project, item.Path);
            var newRelative = ContentReferenceUpdater.ToRelativePath(Project, newPath);
            var updater = new ContentReferenceUpdater(ResourceModel);
            var references = updater.Find(Project, oldRelative);
            if (references.Count > 0)
            {
                var result = await AskAboutReferences(
                    $"'{oldRelative}' is used by the following nodes. Confirm updates them to '{newRelative}', Ignore renames the file only.",
                    "Confirm", references);
                if (result == ContentReferencesResult.Cancel)
                    return false;
                if (result == ContentReferencesResult.Ignore)
                    references.Clear();
            }

            try
            {
                System.IO.File.Move(item.Path, newPath);
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not rename content '{item.Title}': {ex}");
                return false;
            }

            if (references.Count > 0)
                updater.Update(Project, references, newRelative);

            item.Content.Name = newName;
            item.Content.Path = newPath;
            item.SetContent(item.Content);
            UpdateFilter();
            return true;
        }

        public async Task<bool> Delete(ImageItem item)
        {
            var relative = ContentReferenceUpdater.ToRelativePath(Project, item.Path);
            var updater = new ContentReferenceUpdater(ResourceModel);
            var references = updater.Find(Project, relative);

            if (references.Count > 0)
            {
                var result = await AskAboutReferences(
                    $"'{relative}' is used by the following nodes. Confirm clears the content on them, Ignore deletes the file only.",
                    "Confirm", references);
                if (result == ContentReferencesResult.Cancel)
                    return false;
                if (result == ContentReferencesResult.Ignore)
                    references.Clear();
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Delete Content", $"Delete '{System.IO.Path.GetFileName(item.Path)}'? This can not be undone.", ButtonEnum.YesNo, Icon.Warning);
                if (await box.ShowWindowDialogAsync(MainWindow) != ButtonResult.Yes)
                    return false;
            }

            try
            {
                System.IO.File.Delete(item.Path);
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not delete content '{item.Title}': {ex}");
                return false;
            }

            if (references.Count > 0)
                updater.Update(Project, references, (string)null);

            Items.Remove(item);
            UpdateFilter();
            return true;
        }

        private async Task<ContentReferencesResult> AskAboutReferences(string message, string confirmText, List<ContentReference> references)
        {
            var dialog = new ContentReferencesDialog(message, confirmText, references.Select(r => $"{r.Root.FullName}: {r.NodePath}"));
            await dialog.ShowDialog(MainWindow);
            return dialog.Result;
        }

        public void ShowInExplorer(ImageItem item)
        {
            if (System.IO.File.Exists(item.Path))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
        }

        public ObservableCollection<ImageItem> FilteredItems { get; }

        string _searchText;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                UpdateFilter();
            }
        }

        public ICommand ClearSearchCommand { get; }

        private Project Project { get; set; }
        // Providers
        private IContentProvider ContentProvider { get; set; }

        // Constructors
        public ContentPanelViewModel(Control element, Project project) : base(element)
        {
            Project = project;
            Items = new List<ImageItem>();
            FilteredItems = new ObservableCollection<ImageItem>();
            ClearSearchCommand = new RelayCommand(() => { SearchText = string.Empty; });

            ContentProvider = ResourceModel.Get<IContentProvider>();
        }

        public async Task LoadAllContent()
        {
            if (_isLoaded)
                return;
            _isLoaded = true;
            _reloadContent = true;
            ContentProvider.ContentTypesChanged += ContentTypesChanged;
            StartWatching();
            await SyncWithDisk();
        }

        public void Unload()
        {
            _isLoaded = false;
            ContentProvider.ContentTypesChanged -= ContentTypesChanged;
            _debounce?.Stop();
            _watcher?.Dispose();
            _watcher = null;
        }

        private bool _isLoaded;
        private bool _reloadContent;

        private void ContentTypesChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isLoaded)
                    return;
                _reloadContent = true;
                _debounce.Stop();
                _debounce.Start();
            });
        }

        private readonly System.Threading.SemaphoreSlim _syncLock = new(1, 1);
        private System.IO.FileSystemWatcher _watcher;

        // The watcher (and sync tools like OneDrive) can briefly hold a folder open, so the watcher is paused and the operation retried.
        private async Task RunWithoutWatcher(Action operation)
        {
            var watching = _watcher != null && _watcher.EnableRaisingEvents;
            if (watching)
                _watcher.EnableRaisingEvents = false;
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        await Task.Run(operation);
                        return;
                    }
                    catch (Exception ex) when (attempt < 5 && (ex is System.IO.IOException || ex is UnauthorizedAccessException))
                    {
                        await Task.Delay(150 * attempt);
                    }
                }
            }
            finally
            {
                if (watching)
                    _watcher.EnableRaisingEvents = true;
            }
        }
        private DispatcherTimer _debounce;
        private int _nextId;

        private void StartWatching()
        {
            if (_watcher != null)
                return;

            // File events come in bursts, so they are collapsed into one sync on the UI thread.
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounce.Tick += async (_, _) =>
            {
                _debounce.Stop();
                await SyncWithDisk();
            };

            if (!System.IO.Directory.Exists(Project.ProjectContentPath))
                return;

            _watcher = new System.IO.FileSystemWatcher(Project.ProjectContentPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName | System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
            };
            System.IO.FileSystemEventHandler changed = (_, _) => Dispatcher.UIThread.Post(() => { _debounce.Stop(); _debounce.Start(); });
            _watcher.Created += changed;
            _watcher.Changed += changed;
            _watcher.Deleted += changed;
            _watcher.Renamed += (_, _) => Dispatcher.UIThread.Post(() => { _debounce.Stop(); _debounce.Start(); });
            _watcher.EnableRaisingEvents = true;
        }

        // Adds new files, removes missing ones and reloads files that changed on disk.
        private async Task SyncWithDisk()
        {
            await _syncLock.WaitAsync();

            try
            {
                if (!_isLoaded)
                    return;
                var reloadContent = _reloadContent;
                _reloadContent = false;
                var paths = System.IO.Directory.Exists(Project.ProjectContentPath)
                    ? ContentProvider.ListAllPaths(Project)
                    : new List<string>();
                var onDisk = paths.ToDictionary(p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var item in Items.Where(i => !onDisk.ContainsKey(i.Path)).ToList())
                    Items.Remove(item);

                foreach (var path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    var existing = Items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                    var lastWrite = System.IO.File.GetLastWriteTimeUtc(path);
                    if (!reloadContent && existing != null && existing.LastWrite == lastWrite)
                        continue;

                    try
                    {
                        var content = await ContentProvider.GetContentAsync(path);
                        if (!_isLoaded)
                            return;
                        if (existing != null)
                        {
                            existing.SetContent(content);
                            existing.TypeIcon = LoadTypeIcon(path);
                            existing.LastWrite = lastWrite;
                        }
                        else
                        {
                            var item = new ImageItem { Id = (_nextId++).ToString(), LastWrite = lastWrite };
                            item.SetContent(content);
                            item.TypeIcon = LoadTypeIcon(path);
                            Items.Add(item);
                        }
                    }
                    catch (Exception exception)
                    {
                        ResourceModel.Get<ILogModel>().Log($"Could not load content preview '{path}': {exception}");
                    }
                }

                Items.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
                RefreshFolders();
                UpdateFilter();
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private Bitmap LoadTypeIcon(string path)
        {
            var icon = ContentProvider.GetIcon(System.IO.Path.GetExtension(path));
            return icon == null ? null : IconCache.Get(Project, icon);
        }

        void UpdateFilter()
        {
            FilteredItems.Clear();
            var q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var folderPrefix = SelectedFolder == null || SelectedFolder.IsRoot
                ? null
                : SelectedFolder.FullPath.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            var filteredItems = Items
                .Where(i => folderPrefix == null || i.Path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                .Where(i => q == null || (i.Title ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            foreach(var item in filteredItems)
            {
                FilteredItems.Add(item);
            }
        }

        public partial class FolderNode : ViewModelBase
        {
            [ObservableProperty]
            private string _name;
            [ObservableProperty]
            private string _editName;
            [ObservableProperty]
            private bool _isEditing;

            public string FullPath { get; init; }
            public bool IsRoot { get; init; }
            public ObservableCollection<FolderNode> Nodes { get; } = new();

            public void BeginRename()
            {
                EditName = Name;
                IsEditing = true;
            }

            public void CancelRename()
            {
                IsEditing = false;
            }
        }

        private string _selectAfterRefresh;

        private static string WithSeparator(string path) => path.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;

        // Content files, known to the panel, inside a folder or any of its subfolders.
        private List<ImageItem> ItemsIn(string folderPath)
        {
            var prefix = WithSeparator(folderPath);
            return Items.Where(i => i.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public FolderNode CreateFolder(FolderNode parent)
        {
            parent ??= FolderNodes.FirstOrDefault();
            if (parent == null)
                return null;

            var name = "New Folder";
            for (var i = 2; System.IO.Directory.Exists(System.IO.Path.Combine(parent.FullPath, name)); i++)
                name = $"New Folder {i}";

            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(parent.FullPath, name));
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not create folder '{name}': {ex}");
                return null;
            }

            RefreshFolders(System.IO.Path.Combine(parent.FullPath, name));
            return SelectedFolder;
        }

        public async Task<bool> CommitFolderRename(FolderNode folder)
        {
            var newName = folder.EditName?.Trim();
            folder.CancelRename();
            if (folder.IsRoot || string.IsNullOrEmpty(newName) || newName == folder.Name)
                return false;

            var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folder.FullPath), newName);
            if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || System.IO.Directory.Exists(newPath) || System.IO.File.Exists(newPath))
            {
                ResourceModel.Get<ILogModel>().Log($"Could not rename folder to '{newName}': invalid name or it already exists.");
                return false;
            }

            var oldPrefix = ContentReferenceUpdater.ToRelativePath(Project, folder.FullPath) + "/";
            var newPrefix = ContentReferenceUpdater.ToRelativePath(Project, newPath) + "/";
            var references = await ConfirmReferences(
                ItemsIn(folder.FullPath),
                $"Content in '{oldPrefix}' is used by the following nodes. Confirm updates them to '{newPrefix}', Ignore renames the folder only.");
            if (references == null)
                return false;

            try
            {
                await RunWithoutWatcher(() => System.IO.Directory.Move(folder.FullPath, newPath));
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not rename folder '{folder.Name}': {ex}");
                return false;
            }

            if (references.Count > 0)
                new ContentReferenceUpdater(ResourceModel).Update(Project, references, path => newPrefix + path.Substring(oldPrefix.Length));

            var selectedPath = SelectedFolder?.FullPath;
            _selectAfterRefresh = selectedPath != null && selectedPath.StartsWith(WithSeparator(folder.FullPath), StringComparison.OrdinalIgnoreCase)
                ? newPath + selectedPath.Substring(folder.FullPath.Length)
                : ReferenceEquals(SelectedFolder, folder) ? newPath : selectedPath;
            await SyncWithDisk();
            return true;
        }

        public async Task<bool> DeleteFolder(FolderNode folder)
        {
            if (folder == null || folder.IsRoot)
                return false;

            var prefix = ContentReferenceUpdater.ToRelativePath(Project, folder.FullPath) + "/";
            var references = await ConfirmReferences(
                ItemsIn(folder.FullPath),
                $"Content in '{prefix}' is used by the following nodes. Confirm clears the content on them, Ignore deletes the folder only.",
                $"Delete folder '{folder.Name}' and everything in it? This can not be undone.");
            if (references == null)
                return false;

            try
            {
                await RunWithoutWatcher(() => System.IO.Directory.Delete(folder.FullPath, true));
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not delete folder '{folder.Name}': {ex}");
                return false;
            }

            if (references.Count > 0)
                new ContentReferenceUpdater(ResourceModel).Update(Project, references, (string)null);

            await SyncWithDisk();
            return true;
        }

        public async Task<bool> MoveItem(ImageItem item, FolderNode target)
        {
            if (item == null || target == null)
                return false;

            var newPath = System.IO.Path.Combine(target.FullPath, System.IO.Path.GetFileName(item.Path));
            if (string.Equals(newPath, item.Path, StringComparison.OrdinalIgnoreCase))
                return false;
            if (System.IO.File.Exists(newPath))
            {
                ResourceModel.Get<ILogModel>().Log($"Could not move '{item.Title}': a file with that name already exists in '{target.Name}'.");
                return false;
            }

            var oldRelative = ContentReferenceUpdater.ToRelativePath(Project, item.Path);
            var newRelative = ContentReferenceUpdater.ToRelativePath(Project, newPath);
            var references = await ConfirmReferences(
                new List<ImageItem> { item },
                $"'{oldRelative}' is used by the following nodes. Confirm updates them to '{newRelative}', Ignore moves the file only.");
            if (references == null)
                return false;

            try
            {
                System.IO.File.Move(item.Path, newPath);
            }
            catch (Exception ex)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not move content '{item.Title}': {ex}");
                return false;
            }

            if (references.Count > 0)
                new ContentReferenceUpdater(ResourceModel).Update(Project, references, newRelative);

            await SyncWithDisk();
            return true;
        }

        // Returns the references to update, an empty list for Ignore/no references, or null if the operation was cancelled.
        private async Task<List<ContentReference>> ConfirmReferences(List<ImageItem> items, string message, string confirmWithoutReferences = null)
        {
            var paths = items.Select(i => ContentReferenceUpdater.ToRelativePath(Project, i.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var references = paths.Count == 0
                ? new List<ContentReference>()
                : new ContentReferenceUpdater(ResourceModel).Find(Project, path => paths.Contains(path));

            if (references.Count > 0)
            {
                var result = await AskAboutReferences(message, "Confirm", references);
                if (result == ContentReferencesResult.Cancel)
                    return null;
                if (result == ContentReferencesResult.Ignore)
                    references.Clear();
            }
            else if (confirmWithoutReferences != null)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Delete Folder", confirmWithoutReferences, ButtonEnum.YesNo, Icon.Warning);
                if (await box.ShowWindowDialogAsync(MainWindow) != ButtonResult.Yes)
                    return null;
            }
            return references;
        }

        public ObservableCollection<FolderNode> FolderNodes { get; } = new();

        private FolderNode _selectedFolder;
        public FolderNode SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (ReferenceEquals(_selectedFolder, value)) return;
                _selectedFolder = value;
                OnPropertyChanged();
                UpdateFilter();
            }
        }

        private string _folderSignature;

        private void RefreshFolders(string selectPath = null)
        {
            var rootPath = Project.ProjectContentPath;
            var signature = System.IO.Directory.Exists(rootPath)
                ? string.Join("|", System.IO.Directory.EnumerateDirectories(rootPath, "*", System.IO.SearchOption.AllDirectories).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                : string.Empty;

            // Rebuilding would drop a folder name that is being edited, so only do it when something changed.
            if (selectPath == null && _selectAfterRefresh == null && signature == _folderSignature && FolderNodes.Count > 0)
                return;
            _folderSignature = signature;

            var wanted = selectPath ?? _selectAfterRefresh ?? SelectedFolder?.FullPath;
            _selectAfterRefresh = null;

            FolderNodes.Clear();
            var root = new FolderNode { Name = "Content", FullPath = rootPath, IsRoot = true };
            if (System.IO.Directory.Exists(rootPath))
                AddFolders(root);
            FolderNodes.Add(root);

            var selected = wanted == null ? root : Flatten(root).FirstOrDefault(n => string.Equals(n.FullPath, wanted, StringComparison.OrdinalIgnoreCase)) ?? root;
            _selectedFolder = null;
            SelectedFolder = selected;
        }

        private static void AddFolders(FolderNode parent)
        {
            foreach (var directory in System.IO.Directory.EnumerateDirectories(parent.FullPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var node = new FolderNode { Name = System.IO.Path.GetFileName(directory), FullPath = directory };
                AddFolders(node);
                parent.Nodes.Add(node);
            }
        }

        private static IEnumerable<FolderNode> Flatten(FolderNode node)
        {
            yield return node;
            foreach (var child in node.Nodes)
                foreach (var descendant in Flatten(child))
                    yield return descendant;
        }

        public void Add(ImageItem item)
        {
            Items.Add(item);
            UpdateFilter();
        }
    }
}
