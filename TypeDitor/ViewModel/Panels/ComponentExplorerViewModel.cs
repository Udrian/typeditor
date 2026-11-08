using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeD.Models;

namespace TypeDitor.ViewModel.Panels
{
    internal partial class ComponentExplorerViewModel : ViewModelBase
    {
        // Definitions
        public partial class Node : ViewModelBase
        {
            public Component Component { get; private set; }

            [ObservableProperty]
            private string _title;

            [ObservableProperty]
            private string _editName;

            [ObservableProperty]
            private bool _isEditing;

            public void UpdateTitle() {
                Title = Component.Name;
            }

            public void BeginRename()
            {
                EditName = Component.Name;
                IsEditing = true;
            }

            public void CancelRename()
            {
                IsEditing = false;
            }

            [ObservableProperty]
            private ObservableCollection<Node> _nodes;

            [ObservableProperty]
            private bool _isExpanded;

            [ObservableProperty]
            private bool _isRemoved;

            [ObservableProperty]
            [NotifyPropertyChangedFor(nameof(VisibilityIcon))]
            [NotifyPropertyChangedFor(nameof(ToolTipText))]
            private bool _isPublic;

            public string ToolTipText
            {
                get
                {
                    var lines = new List<string>
                    {
                        $"Class: {Component.ClassName}",
                        $"Namespace: {(string.IsNullOrEmpty(Component.Namespace) ? "-" : Component.Namespace)}"
                    };
                    if (Component.ParentComponent != null)
                    {
                        lines.Add($"Instanced in: {Component.ParentComponent.Name} ({Component.ParentComponent.FullName})");
                        lines.Add($"Visibility: {(IsPublic ? "Public" : "Private")}");
                    }
                    return string.Join(Environment.NewLine, lines);
                }
            }

            public string VisibilityIcon => IsPublic ? "\U0001F310" : "\U0001F512";

            public Avalonia.Media.Imaging.Bitmap Icon =>
                IconCache.Get(ResourceModel.Get<INodeSelectionModel>().Project, Component.ResolvedIcon);

            public void RefreshIcon() => OnPropertyChanged(nameof(Icon));

            public bool ShowVisibilityIcon => Component.ParentComponent != null;

            [ObservableProperty]
            private bool _isDropBefore;

            [ObservableProperty]
            private bool _isDropAfter;

            [ObservableProperty]
            private bool _isDropChild;

            public void SetDrop(MovePosition? position)
            {
                IsDropBefore = position == MovePosition.Before;
                IsDropAfter = position == MovePosition.After;
                IsDropChild = position == MovePosition.Child;
            }

            public ObservableCollection<Node> VisibleNodes { get; } = new ObservableCollection<Node>();

            private bool _isMatch = true;
            public bool IsMatch => _isMatch;

            public Node(Component component) : base()
            {
                Component = component;
                IsRemoved = component.Removed;
                IsPublic = component.Public;
                Nodes = new ObservableCollection<Node>(Component.Children.Where(c => c.IsVisible).Select(c => new Node(c)));
                foreach (var node in Nodes)
                    VisibleNodes.Add(node);
                UpdateTitle();
            }

            public bool ApplyFilter(string filter)
            {
                var childMatch = false;
                foreach (var child in Nodes)
                    childMatch |= child.ApplyFilter(filter);

                _isMatch = string.IsNullOrEmpty(filter) || MatchesFilter(filter);
                SyncVisible(VisibleNodes, Nodes.Where(n => n._isMatch || n.HasVisibleDescendants).ToList());
                if (!string.IsNullOrEmpty(filter) && VisibleNodes.Count > 0)
                    IsExpanded = true;
                return _isMatch || childMatch;
            }

            private bool HasVisibleDescendants => VisibleNodes.Count > 0;

            private bool MatchesFilter(string filter)
            {
                if (Component.FullName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                    return true;
                return Component.Properties.Any(p =>
                    (p.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (p.Value?.ToString()?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            public static void SyncVisible(ObservableCollection<Node> target, List<Node> desired)
            {
                for (var i = target.Count - 1; i >= 0; i--)
                    if (!desired.Contains(target[i]))
                        target.RemoveAt(i);

                for (var i = 0; i < desired.Count; i++)
                {
                    if (i < target.Count && ReferenceEquals(target[i], desired[i]))
                        continue;
                    var existing = target.IndexOf(desired[i]);
                    if (existing >= 0)
                        target.Move(existing, i);
                    else
                        target.Insert(i, desired[i]);
                }
            }
        }

        // Models
        public IHookModel HookModel { get; set; }
        private INodeSelectionModel NodeSelectionModel { get; set; }
        IEditorCommandRegistry EditorCommandRegistry { get; set; }

        // Data
        public Project LoadedProject { get; set; }
        public Component SelectedComponent => NodeSelectionModel.SelectedNode;

        // Properties
        public Component Component => NodeSelectionModel.OpenedComponent;
        public ObservableCollection<Node> Nodes { get; set; }
        public ObservableCollection<Node> VisibleNodes { get; } = new ObservableCollection<Node>();

        [ObservableProperty]
        private string _filterText = "";

        partial void OnFilterTextChanged(string value) => RefreshFilter();

        private void RefreshFilter()
        {
            var filter = FilterText?.Trim();
            foreach (var node in Nodes)
                node.ApplyFilter(filter);
            Node.SyncVisible(VisibleNodes, Nodes.Where(n => n.IsMatch || n.VisibleNodes.Count > 0).ToList());
        }

        [ObservableProperty]
        private Node _selectedNode;

        /// <summary>
        /// Raised when something other than the tree itself changed the selection, the last node is the focused one.
        /// </summary>
        public event Action<IReadOnlyList<Node>> SelectionRequested;

        private bool _publishingSelection;

        public IReadOnlyList<Component> SelectedComponents => NodeSelectionModel.SelectedNodes;

        // Constructors
        public ComponentExplorerViewModel(Control element, Project project) : base(element)
        {
            LoadedProject = project;
            HookModel = ResourceModel.Get<IHookModel>();
            NodeSelectionModel = ResourceModel.Get<INodeSelectionModel>();
            EditorCommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            Nodes = new ObservableCollection<Node>();

            HookModel.AddHook<ComponentFocusHook>((hook) =>
            {
                if (hook.Root && ReferenceEquals(hook.Project, LoadedProject))
                {
                    Nodes.Clear();
                    if (Component != null)
                    {
                        Nodes.Add(new Node(Component) { IsExpanded = true });
                        SelectedNode = Nodes[0];
                    }
                    RefreshFilter();
                }
                else if (!_publishingSelection
                    && ReferenceEquals(hook.Project, LoadedProject)
                    && Component != null
                    && ReferenceEquals(GetRootComponent(hook.Component), Component))
                {
                    var all = Nodes.Flatten(node => node.Nodes).ToList();
                    var focused = all.FirstOrDefault(node => ReferenceEquals(node.Component, hook.Component));
                    var others = hook.Selection == null
                        ? Enumerable.Empty<Node>()
                        : all.Where(node => !ReferenceEquals(node, focused) && hook.Selection.Contains(node.Component));
                    SelectionRequested?.Invoke(others.Append(focused).Where(node => node != null).ToList());
                }
            });

            HookModel.AddHook<CloseComponentHook>((hook) =>
            {
                if (ReferenceEquals(hook.Project, LoadedProject) && NodeSelectionModel.OpenedComponent == null)
                    Nodes.Clear();
                    RefreshFilter();
            });

            HookModel.AddHook<ComponentAddedHook>((hook) =>
            {
                var node = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Parent));
                if (node != null && hook.Child.IsVisible)
                {
                    var newNode = new Node(hook.Child);
                    var nodeIndex = hook.Index < 0
                        ? node.Nodes.Count
                        : node.Nodes.TakeWhile(n => hook.Parent.Children.IndexOf(n.Component) < hook.Index).Count();
                    node.Nodes.Insert(nodeIndex, newNode);
                    node.IsExpanded = true;
                    RefreshFilter();
                }
            });

            HookModel.AddHook<ComponentMovedHook>((hook) =>
            {
                var node = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Parent));
                var childNode = node?.Nodes.FirstOrDefault(n => ReferenceEquals(n.Component, hook.Child));
                if (childNode != null)
                {
                    var oldIndex = node.Nodes.IndexOf(childNode);
                    var target = node.Nodes.Where(n => n != childNode)
                        .TakeWhile(n => hook.Parent.Children.IndexOf(n.Component) < hook.Index).Count();
                    node.Nodes.Move(oldIndex, target);
                    RefreshFilter();
                }
            });

            HookModel.AddHook<ComponentPublicStateChangedHook>((hook) =>
            {
                var childNode = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Child));
                if (childNode != null)
                    childNode.IsPublic = hook.Child.Public;
            });

            HookModel.AddHook<ComponentIconChangedHook>((hook) =>
            {
                foreach (var node in Nodes.Flatten(n => n.Nodes))
                {
                    for (var component = node.Component; component != null; component = component.BaseInheritedComponent)
                    {
                        if (component.FullName == hook.Component.FullName)
                            component.Icon = hook.Component.Icon;
                    }
                    node.RefreshIcon();
                }
            });
            
            HookModel.AddHook<ComponentRemovedHook>((hook) =>
            {
                var node = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Parent));
                if (node != null)
                {
                    var childNode = node.Nodes.FirstOrDefault(n => ReferenceEquals(n.Component, hook.Child));
                    if (childNode != null)
                    {
                        node.Nodes.Remove(childNode);
                        RefreshFilter();
                    }
                }
            });

            HookModel.AddHook<ComponentRemovedStateChangedHook>((hook) =>
            {
                var childNode = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Child));
                if (childNode != null)
                    childNode.IsRemoved = hook.Child.Removed;
            });

            HookModel.AddHook<PropertyChangedHook>((hook) =>
            {
                if (hook.Property.Name == "Name")
                {
                    var node = Nodes.Flatten(n => n.Nodes).FirstOrDefault(n => ReferenceEquals(n.Component, hook.Component));
                    if (node != null)
                    {
                        node.UpdateTitle();
                    }
                }
                if (!string.IsNullOrWhiteSpace(FilterText))
                    RefreshFilter();
            });
        }

        // Functions
        public async System.Threading.Tasks.Task ContextMenuOpened(ContextMenu contextMenu, Node node)
        {
            NodeSelectionModel.SetComponentExplorerActive();
            if (node != null && !SelectedComponents.Contains(node.Component))
                SelectionChanged(node.Component);
            else if (node != null && !ReferenceEquals(SelectedComponent, node.Component))
                SelectionChanged(SelectedComponents, node.Component);
            var openedComponent = NodeSelectionModel.OpenedComponent;
            contextMenu.Items.Clear();
            try
            {
                await ResourceModel.Get<INodeClipboardModel>().RefreshAsync();
            }
            catch (Exception exception) when (exception is System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException or NotSupportedException)
            {
                ResourceModel.Get<ILogModel>().Log($"Could not read the node clipboard: {exception}");
            }
            if (NodeSelectionModel.OpenedComponent != openedComponent)
                return;

            var componentContextMenuHook = CreateComponentContextMenu(node?.Component);

            contextMenu.Items.Clear();
            foreach (var menu in componentContextMenuHook.Menu.Items)
            {
                ViewHelper.InitMenu(contextMenu, menu, this);
            }
        }

        public bool TryExecuteShortcut(string shortcut, Component selectedComponent)
        {
            if (selectedComponent != null && !ReferenceEquals(SelectedComponent, selectedComponent))
            {
                if (SelectedComponents.Contains(selectedComponent))
                    SelectionChanged(SelectedComponents, selectedComponent);
                else
                    SelectionChanged(selectedComponent);
            }
            else
                NodeSelectionModel.SetComponentExplorerActive();

            return EditorCommandRegistry.TryExecuteShortcut(shortcut, "global");
        }

        public void SelectionChanged(Component component)
        {
            SelectionChanged(new[] { component }, component);
        }

        /// <summary>
        /// Publishes the tree's own selection. The focused component stays the same while it is still selected.
        /// </summary>
        public void SelectionChanged(IReadOnlyList<Component> selection, Component focused)
        {
            if (focused == null)
                return;

            var all = Nodes.Flatten(node => node.Nodes).Select(node => node.Component).Where(selection.Contains).ToList();
            if (!all.Contains(focused))
                all.Add(focused);

            NodeSelectionModel.SetComponentExplorerActive();
            _publishingSelection = true;
            try
            {
                HookModel.Shoot(new ComponentFocusHook() { Project = LoadedProject, Component = focused, Root = false, Selection = all });
            }
            finally
            {
                _publishingSelection = false;
            }
        }

        private static Component GetRootComponent(Component component)
        {
            while (component?.ParentComponent != null)
                component = component.ParentComponent;
            return component;
        }

        public void SetExplorerActive()
        {
            NodeSelectionModel.SetComponentExplorerActive();
        }

        public bool MoveNodes(IReadOnlyList<Component> nodes, Component primary, Component target, MovePosition position)
        {
            return EditorCommandRegistry.Execute("node.move", new ComponentMoveCommandData
            {
                Project = LoadedProject,
                Node = primary,
                Nodes = nodes,
                Target = target,
                Position = position
            });
        }

        public enum MoveDirection { Up, Down, Left, Right }

        public bool MoveSelected(MoveDirection direction)
        {
            var selected = SelectedComponents;
            var ordered = Nodes.Flatten(node => node.Nodes).Select(node => node.Component).Where(selected.Contains).ToList();
            ordered = ordered.Where(node => !ordered.Any(other => !ReferenceEquals(other, node) && IsWithin(node, other))).ToList();
            if (direction is MoveDirection.Down or MoveDirection.Left)
                ordered.Reverse();

            var moves = new List<ComponentMove>();
            foreach (var node in ordered)
            {
                var parent = node.ParentComponent;
                if (parent == null)
                    continue;

                var siblings = parent.Children;
                var index = siblings.IndexOf(node);
                bool Free(Component sibling) => sibling.IsVisible && !ordered.Contains(sibling) && !selected.Contains(sibling);
                switch (direction)
                {
                    case MoveDirection.Up:
                        var previous = siblings.Take(index).LastOrDefault(Free);
                        if (previous != null)
                            moves.Add(new ComponentMove { Node = node, Target = previous, Position = MovePosition.Before });
                        break;
                    case MoveDirection.Down:
                        var next = siblings.Skip(index + 1).FirstOrDefault(Free);
                        if (next != null)
                            moves.Add(new ComponentMove { Node = node, Target = next, Position = MovePosition.After });
                        break;
                    case MoveDirection.Left:
                        if (parent.ParentComponent != null && !selected.Contains(parent))
                            moves.Add(new ComponentMove { Node = node, Target = parent, Position = MovePosition.After });
                        break;
                    default:
                        var below = siblings.Skip(index + 1).FirstOrDefault(sibling => Free(sibling) && !sibling.Removed);
                        if (below != null)
                        {
                            moves.Add(new ComponentMove { Node = node, Target = below, Position = MovePosition.FirstChild });
                            break;
                        }
                        var above = siblings.Take(index).LastOrDefault(sibling => Free(sibling) && !sibling.Removed);
                        if (above != null)
                            moves.Add(new ComponentMove { Node = node, Target = above, Position = MovePosition.Child });
                        break;
                }
            }

            if (direction == MoveDirection.Right)
            {
                // Nodes moving into the node above must keep their order, the ones moving below were queued in reverse order.
                moves = moves.Where(m => m.Position == MovePosition.FirstChild).Reverse()
                    .Concat(moves.Where(m => m.Position == MovePosition.Child)).ToList();
            }

            if (moves.Count == 0)
                return false;

            return EditorCommandRegistry.Execute("node.move", new ComponentMoveCommandData
            {
                Project = LoadedProject,
                Node = NodeSelectionModel.SelectedNode,
                Moves = moves
            });
        }

        private static bool IsWithin(Component component, Component ancestor)
        {
            for (; component != null; component = component.ParentComponent)
            {
                if (ReferenceEquals(component, ancestor))
                    return true;
            }
            return false;
        }

        public bool CanDrop(IReadOnlyList<Component> nodes, Component target, MovePosition position)
        {
            return nodes.Any(node => ComponentMoveRules.TryResolve(node, target, position, out _, out _));
        }

        public bool CommitRename(Node node)
        {
            var nameProperty = node.Component.Properties.FirstOrDefault(property => property.Name == "Name");
            if (nameProperty == null || nameProperty.ReadOnly || node.Component.ReadOnly)
            {
                node.CancelRename();
                return false;
            }

            var executed = EditorCommandRegistry.Execute("component.change-property", new ChangePropertyCommandData
            {
                Project = LoadedProject,
                Property = nameProperty,
                Value = node.EditName,
                ApplyToSelection = true
            });
            if (executed)
                node.CancelRename();
            return executed;
        }

        public void BeginRename(Node node)
        {
            if (node == null || !ReferenceEquals(SelectedComponent, node.Component))
                return;

            NodeSelectionModel.SetComponentExplorerActive();
            node.BeginRename();
        }

        private ComponentContextMenuHook CreateComponentContextMenu(Component selectedComponent)
        {
            var hook = new ComponentContextMenuHook()
            {
                Menu = new TypeD.View.Menu(),
                Project = LoadedProject,
                OpenedComponent = Component,
                SelectedComponent = selectedComponent
            };
            HookModel.Shoot(hook);
            return hook;
        }

    }
}
