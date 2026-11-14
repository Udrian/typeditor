using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.PropertyTemplates
{
    public sealed class FolderListPropertyNode : PropertyNode
    {
        private static readonly StringComparer PathComparer =
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private string _selectedFolder;

        public ObservableCollection<string> Folders { get; } = new();
        public ICommand AddFolderCommand { get; }
        public ICommand RemoveFolderCommand { get; }

        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (_selectedFolder == value) return;
                _selectedFolder = value;
                OnPropertyChanged(nameof(SelectedFolder));
                OnPropertyChanged(nameof(HasSelectedFolder));
                OnPropertyChanged(nameof(CanRemoveFolder));
            }
        }

        public bool HasSelectedFolder => !string.IsNullOrEmpty(SelectedFolder);
        public bool CanRemoveFolder => HasSelectedFolder && !ReadOnly;
        public string ErrorMessage { get; private set; } = string.Empty;
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public FolderListPropertyNode(Component component, Property property, Action<PropertyNode, object> valueChanged)
            : base(component, property, valueChanged)
        {
            AddFolderCommand = new AsyncRelayCommand(AddFoldersAsync);
            RemoveFolderCommand = new RelayCommand(RemoveSelectedFolder);
            RefreshValue();
        }

        public override object Value
        {
            get => base.Value;
            set => base.Value = ConvertValue(value);
        }

        public override void RefreshValue()
        {
            var selected = SelectedFolder;
            Folders.Clear();
            foreach (var folder in ReadFolders(base.Value))
                Folders.Add(folder);
            SelectedFolder = Folders.Contains(selected) ? selected : null;
            base.RefreshValue();
        }

        private async Task AddFoldersAsync()
        {
            if (ReadOnly) return;

            try
            {
                var window = ViewModelBase.MainWindow;
                var selected = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Add folder",
                    AllowMultiple = true
                });
                if (selected.Count == 0) return;

                var folders = Folders.ToList();
                foreach (var folder in selected)
                {
                    var path = folder.Path.LocalPath;
                    if (string.IsNullOrWhiteSpace(path))
                        throw new InvalidOperationException("The selected folder does not have a local filesystem path.");
                    folders.Add(Path.GetFullPath(path));
                }

                SetFolders(folders);
                SetError(string.Empty);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or InvalidOperationException)
            {
                SetError(exception.Message);
                ResourceModel.Get<ILogModel>()?.Log($"Could not add folder property value: {exception}");
            }
        }

        private void RemoveSelectedFolder()
        {
            if (ReadOnly || string.IsNullOrWhiteSpace(SelectedFolder)) return;

            try
            {
                SetFolders(Folders.Where(folder => !PathComparer.Equals(folder, SelectedFolder)));
                SetError(string.Empty);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or InvalidOperationException)
            {
                SetError(exception.Message);
                ResourceModel.Get<ILogModel>()?.Log($"Could not remove folder property value: {exception}");
            }
        }

        private void SetFolders(IEnumerable<string> folders)
        {
            var normalized = new List<string>();
            foreach (var folder in folders)
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                var path = Path.GetFullPath(folder);
                if (!normalized.Contains(path, PathComparer))
                    normalized.Add(path);
            }

            object value;
            if (Property.Type == typeof(string))
                value = JsonSerializer.Serialize(normalized);
            else if (Property.Type.IsAssignableFrom(typeof(List<string>)))
                value = normalized;
            else
                throw new InvalidOperationException($"Folder list template does not support property type '{Property.Type}'.");

            Value = value;
        }

        private object ConvertValue(object value)
        {
            if (Property.Type == typeof(string))
            {
                if (value is IEnumerable<string> folders)
                    return JsonSerializer.Serialize(folders);
                return value?.ToString() ?? string.Empty;
            }

            if (Property.Type.IsAssignableFrom(typeof(List<string>)))
                return value as List<string> ?? ReadFolders(value);

            return value;
        }

        private static List<string> ReadFolders(object value)
        {
            if (value is IEnumerable<string> folders)
                return folders.ToList();
            if (value is not string serialized || string.IsNullOrWhiteSpace(serialized))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(serialized) ?? new List<string>();
            }
            catch (JsonException)
            {
                return new List<string> { serialized };
            }
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(HasError));
        }
    }
}
