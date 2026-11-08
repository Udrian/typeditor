using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeDCore.Commands.Data;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels
{
    /// <summary>
    /// Interaction logic for ComponentPanel.xaml
    /// </summary>
    public partial class ComponentExplorerPanel : UserControl, IComponentExplorerView
    {
        // ViewModel
        ComponentExplorerViewModel ViewModel { get; set; }
        
        // Constructors
        /// <summary>Creates a component explorer for the specified project.</summary>
        public ComponentExplorerPanel(Project project)
        {
            DataContext = ViewModel = new ComponentExplorerViewModel(this, project);
            InitializeComponent();

            DragDrop.SetAllowDrop(ComponentsTree, true);
            ViewModel.SelectionRequested += ApplySelection;
            ComponentsTree.AddHandler(PointerPressedEvent, ComponentsTree_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            ComponentsTree.AddHandler(PointerMovedEvent, ComponentsTree_PointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            ComponentsTree.AddHandler(PointerReleasedEvent, (_, _) => { _pressed = null; _dragNode = null; }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            ComponentsTree.AddHandler(KeyDownEvent, ComponentsTree_MoveKeyDown, RoutingStrategies.Tunnel);
            ComponentsTree.AddHandler(KeyDownEvent, ComponentsTree_KeyDown, RoutingStrategies.Tunnel);
            DragDrop.AddDragOverHandler(ComponentsTree, ComponentsTree_DragOver);
            DragDrop.AddDropHandler(ComponentsTree, ComponentsTree_Drop);
        }

        private PointerPressedEventArgs _pressed;
        private Point _pressedPosition;
        private ComponentExplorerViewModel.Node _dragNode;
        private ComponentExplorerViewModel.Node _dropNode;
        private System.Collections.Generic.List<Component> _dragSelection;
        private Component _pressedComponent;
        private bool _applyingSelection;

        private ComponentExplorerViewModel.Node PrimaryNode()
        {
            return ComponentsTree.SelectedItems?.OfType<ComponentExplorerViewModel.Node>()
                    .FirstOrDefault(node => ReferenceEquals(node.Component, ViewModel.SelectedComponent))
                ?? ComponentsTree.SelectedItem as ComponentExplorerViewModel.Node;
        }

        private void ApplySelection(System.Collections.Generic.IReadOnlyList<ComponentExplorerViewModel.Node> nodes)
        {
            _applyingSelection = true;
            try
            {
                ComponentsTree.SelectedItems.Clear();
                foreach (var node in nodes)
                    ComponentsTree.SelectedItems.Add(node);
            }
            finally
            {
                _applyingSelection = false;
            }
        }

        private void ComponentsTree_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var node = NodeAt(e, out var row);
            var isLeft = e.GetCurrentPoint(ComponentsTree).Properties.IsLeftButtonPressed;
            var isTextBox = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<TextBox>().Any() == true;
            var canDrag = node is { IsEditing: false } && row != null && !isTextBox && isLeft && node.Component.ParentComponent != null;
            _pressed = canDrag ? e : null;
            _dragNode = canDrag ? node : null;
            _pressedComponent = isLeft && row != null ? node?.Component : null;
            _pressedPosition = e.GetPosition(ComponentsTree);

            // Pressing an already selected node must not shrink the selection that is about to be dragged.
            _dragSelection = null;
            if (canDrag && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == 0)
            {
                var selected = ComponentsTree.SelectedItems.OfType<ComponentExplorerViewModel.Node>().Select(n => n.Component).ToList();
                if (selected.Contains(node.Component))
                    _dragSelection = selected;
            }
        }

        private async void ComponentsTree_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_pressed == null || _dragNode == null)
                return;
            if (!e.GetCurrentPoint(ComponentsTree).Properties.IsLeftButtonPressed)
            {
                _pressed = null;
                return;
            }

            var delta = e.GetPosition(ComponentsTree) - _pressedPosition;
            if (System.Math.Abs(delta.X) < 6 && System.Math.Abs(delta.Y) < 6)
                return;

            var pressed = _pressed;
            _pressed = null;

            if (_dragSelection == null)
            {
                var selected = ComponentsTree.SelectedItems.OfType<ComponentExplorerViewModel.Node>().Select(n => n.Component).ToList();
                _dragSelection = selected.Contains(_dragNode.Component) ? selected : new System.Collections.Generic.List<Component> { _dragNode.Component };
            }

            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(_dragNode.Component.ID ?? string.Empty));
            await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Move);
            ClearDropIndicator();
            _dragNode = null;
            _dragSelection = null;
        }

        private static ComponentExplorerViewModel.Node NodeAt(RoutedEventArgs e, out Control row)
        {
            row = null;
            if (e.Source is not Visual source)
                return null;

            foreach (var visual in source.GetSelfAndVisualAncestors())
            {
                if (row == null && visual is Control control && control.Classes.Contains("noderow"))
                    row = control;
                if (visual is TreeViewItem item)
                    return item.DataContext as ComponentExplorerViewModel.Node;
            }
            return null;
        }

        private static MovePosition PositionAt(DragEventArgs e, Control row)
        {
            if (row == null || row.Bounds.Height <= 0)
                return MovePosition.Child;

            var y = e.GetPosition(row).Y;
            if (y < row.Bounds.Height * 0.25)
                return MovePosition.Before;
            if (y > row.Bounds.Height * 0.75)
                return MovePosition.After;
            return MovePosition.Child;
        }

        private void ClearDropIndicator()
        {
            _dropNode?.SetDrop(null);
            _dropNode = null;
        }

        private void ComponentsTree_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            var target = NodeAt(e, out var row);
            var position = PositionAt(e, row);
            if (_dragNode == null || _dragSelection == null || target == null
                || _dragSelection.Contains(target.Component)
                || !ViewModel.CanDrop(_dragSelection, target.Component, position))
            {
                ClearDropIndicator();
                e.DragEffects = DragDropEffects.None;
                return;
            }

            if (!ReferenceEquals(_dropNode, target))
                ClearDropIndicator();
            _dropNode = target;
            target.SetDrop(position);
            e.DragEffects = DragDropEffects.Move;
        }

        private void ComponentsTree_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            var node = _dragNode;
            var selection = _dragSelection;
            var target = NodeAt(e, out var row);
            var position = PositionAt(e, row);
            ClearDropIndicator();
            if (node == null || selection == null || target == null || selection.Contains(target.Component))
                return;

            ViewModel.MoveNodes(selection, node.Component, target.Component, position);
            FocusTree();
        }

        private void ComponentsTree_MoveKeyDown(object sender, KeyEventArgs e)
        {
            var modifiers = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);
            if (modifiers != (KeyModifiers.Control | KeyModifiers.Shift) || e.Source is TextBox)
                return;
            if (PrimaryNode() is not { IsEditing: false })
                return;

            ComponentExplorerViewModel.MoveDirection direction;
            switch (e.Key)
            {
                case Key.Up: direction = ComponentExplorerViewModel.MoveDirection.Up; break;
                case Key.Down: direction = ComponentExplorerViewModel.MoveDirection.Down; break;
                case Key.Left: direction = ComponentExplorerViewModel.MoveDirection.Left; break;
                case Key.Right: direction = ComponentExplorerViewModel.MoveDirection.Right; break;
                default: return;
            }

            e.Handled = true;
            if (ViewModel.MoveSelected(direction))
                FocusTree();
        }

        private bool OpeningPreparedContextMenu;

        private async void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (OpeningPreparedContextMenu)
                return;
            e.Cancel = true;
            var menu = (ContextMenu)sender;
            await ViewModel.ContextMenuOpened(menu, PrimaryNode());
            if (menu.Items.Count == 0)
                return;
            OpeningPreparedContextMenu = true;
            try
            {
                menu.Open(ComponentsTree);
            }
            finally
            {
                OpeningPreparedContextMenu = false;
            }
        }

        private void ComponentsTree_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applyingSelection)
                return;

            var selected = ComponentsTree.SelectedItems.OfType<ComponentExplorerViewModel.Node>().Select(node => node.Component).ToList();
            if (selected.Count == 0)
                return;

            var added = e.AddedItems.OfType<ComponentExplorerViewModel.Node>().Select(node => node.Component).ToList();
            var current = ViewModel.SelectedComponent;
            var focused = _pressedComponent != null && added.Contains(_pressedComponent) ? _pressedComponent
                : added.LastOrDefault()
                ?? (current != null && selected.Contains(current) ? current : selected.Last());
            ViewModel.SelectionChanged(selected, focused);
        }

        /// <summary>Moves keyboard focus to the selected component in the tree.</summary>
        public void FocusTree()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var items = ComponentsTree.GetSelfAndVisualDescendants()
                    .OfType<TreeViewItem>()
                    .Where(treeItem => treeItem.IsSelected)
                    .ToList();
                var item = items.FirstOrDefault(treeItem => treeItem.DataContext is ComponentExplorerViewModel.Node node
                        && ReferenceEquals(node.Component, ViewModel.SelectedComponent))
                    ?? items.FirstOrDefault();
                (item ?? (Control)ComponentsTree).Focus();
            }, DispatcherPriority.Background);
        }

        private void ComponentsTree_GotFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.SetExplorerActive();
        }

        private void ComponentsTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Source is TextBox || PrimaryNode() is not { } selectedItem)
                return;
            if (selectedItem.IsEditing)
                return;

            if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
            {
                SelectAllNodes();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F2)
            {
                ViewModel.BeginRename(selectedItem);
                Dispatcher.UIThread.Post(() => FocusNameEditor(selectedItem));
                e.Handled = true;
                return;
            }

            var shortcut = TypeD.Models.Data.EditorShortcut.FromKey(
                e.Key.ToString(),
                e.KeyModifiers.HasFlag(KeyModifiers.Control),
                e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                e.KeyModifiers.HasFlag(KeyModifiers.Alt),
                e.KeyModifiers.HasFlag(KeyModifiers.Meta));

            e.Handled = ViewModel.TryExecuteShortcut(shortcut, selectedItem.Component);
        }

        private void SelectAllNodes()
        {
            var primary = PrimaryNode();
            var nodes = ViewModel.VisibleNodes
                .Flatten(node => node.IsExpanded ? node.VisibleNodes : System.Linq.Enumerable.Empty<ComponentExplorerViewModel.Node>())
                .ToList();
            if (primary == null || nodes.Count == 0)
                return;

            _pressedComponent = null;
            ComponentsTree.SelectedItems.Clear();
            foreach (var node in nodes)
                ComponentsTree.SelectedItems.Add(node);
            ViewModel.SelectionChanged(nodes.Select(node => node.Component).ToList(), primary.Component);
        }

        private void NodeName_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not ComponentExplorerViewModel.Node node)
                return;

            if (e.Key == Key.Enter)
            {
                ViewModel.CommitRename(node);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                node.CancelRename();
                e.Handled = true;
            }
        }

        private void FocusNameEditor(ComponentExplorerViewModel.Node node)
        {
            var item = ComponentsTree.GetSelfAndVisualDescendants()
                .OfType<TreeViewItem>()
                .FirstOrDefault(treeItem => ReferenceEquals(treeItem.DataContext, node));
            var textBox = item?.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault();
            textBox?.Focus();
            textBox?.SelectAll();
        }

    }
}
