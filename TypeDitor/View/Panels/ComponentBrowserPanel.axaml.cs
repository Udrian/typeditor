using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TypeD.Models.Data;
using TypeD.Models;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels
{
    /// <summary>
    /// Interaction logic for ComponentBrowserPanel.xaml
    /// </summary>
    public partial class ComponentBrowserPanel : UserControl
    {
        // ViewModel
        ComponentBrowserViewModel ViewModel { get; set; }

        // Constructors
        /// <summary>Creates a component browser for the specified project.</summary>
        public ComponentBrowserPanel(Project project)
        {
            InitializeComponent();

            ViewModel = new ComponentBrowserViewModel(this, project, TreeView);
            DataContext = ViewModel;

            TreeView.AddHandler(PointerPressedEvent, TreeView_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            TreeView.AddHandler(PointerMovedEvent, TreeView_PointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            TreeView.AddHandler(PointerReleasedEvent, (_, _) => _pressed = null, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        }

        private PointerPressedEventArgs _pressed;
        private Component _dragComponent;
        private Point _pressedPosition;

        private void TreeView_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var node = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault()?.DataContext as ComponentBrowserViewModel.Node;
            _dragComponent = node?.Context.Item as Component;
            _pressed = _dragComponent != null && e.GetCurrentPoint(TreeView).Properties.IsLeftButtonPressed ? e : null;
            _pressedPosition = e.GetPosition(TreeView);
        }

        private async void TreeView_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_pressed == null || _dragComponent == null)
                return;
            if (!e.GetCurrentPoint(TreeView).Properties.IsLeftButtonPressed)
            {
                _pressed = null;
                return;
            }

            var delta = e.GetPosition(TreeView) - _pressedPosition;
            if (System.Math.Abs(delta.X) < 6 && System.Math.Abs(delta.Y) < 6)
                return;

            var pressed = _pressed;
            _pressed = null;

            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(_dragComponent.FullName));
            ViewerDragPayload.SetComponent(_dragComponent);
            try
            {
                await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Copy);
            }
            finally
            {
                ViewerDragPayload.Clear();
                _dragComponent = null;
            }
        }

        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ViewModel.ContextMenuOpened(sender as ContextMenu, TreeView.SelectedItem as ComponentBrowserViewModel.Node);
        }

        private void TreeViewItem_MouseDoubleClickEvent(object sender, TappedEventArgs e)
        {
            var item = ((Visual)e.Source!).GetSelfAndVisualAncestors()
            .OfType<TreeViewItem>()
            .FirstOrDefault();

            if (item is not null)
            {
                if (item.DataContext is ComponentBrowserViewModel.Node node)
                    ViewModel.DoubleClickItem(node);
            }
        }

        private void TreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectionChanged(TreeView.SelectedItem as ComponentBrowserViewModel.Node);
        }

        private void TreeView_GotFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.BrowserFocused();
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            ViewModel.BrowserFocused();
            var shortcut = TypeD.Models.Data.EditorShortcut.FromKey(
                e.Key.ToString(),
                e.KeyModifiers.HasFlag(KeyModifiers.Control),
                e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                e.KeyModifiers.HasFlag(KeyModifiers.Alt),
                e.KeyModifiers.HasFlag(KeyModifiers.Meta));

            e.Handled = ViewModel.TryExecuteShortcut(shortcut);
        }
    }
}
