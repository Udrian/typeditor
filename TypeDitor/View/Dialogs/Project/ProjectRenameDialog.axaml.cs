using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TypeDitor.Models;

namespace TypeDitor.View.Dialogs.Project
{
    public partial class ProjectRenameDialog : Window, INotifyPropertyChanged
    {
        private string _newName;
        private event PropertyChangedEventHandler _propertyChanged;

        public ProjectRenamePlan Plan { get; }
        public string NewName
        {
            get => _newName;
            set
            {
                if (_newName == value) return;
                _newName = value;
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewName)));
                _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRename)));
            }
        }

        public bool CanRename => ProjectRenameService.IsValidProjectName(NewName)
            && !string.Equals(NewName, Plan.ProjectName, StringComparison.OrdinalIgnoreCase);

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add => _propertyChanged += value;
            remove => _propertyChanged -= value;
        }

        public ProjectRenameDialog(ProjectRenamePlan plan)
        {
            Plan = plan;
            NewName = plan.ProjectName;
            InitializeComponent();
            DataContext = this;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (CanRename)
                Close(true);
        }
    }
}
