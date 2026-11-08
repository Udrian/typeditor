using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TypeD.Models.Data;
using TypeD.Models;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Lets the user pick the node another node is moved into.
    /// </summary>
    public partial class ComponentMoveTargetDialog : Window
    {
        /// <summary>Represents a component target and its children in the move dialog.</summary>
        public class TargetNode
        {
            /// <summary>Gets the component represented by this node.</summary>
            public Component Component { get; init; }
            /// <summary>Gets the component name displayed in the tree.</summary>
            public string Title { get; init; }
            /// <summary>Gets whether this component is a valid move target.</summary>
            public bool IsEnabled { get; init; }
            /// <summary>Gets the child targets displayed beneath this node.</summary>
            public List<TargetNode> Children { get; } = new List<TargetNode>();
        }

        /// <summary>Gets the component selected as the move destination.</summary>
        public Component SelectedComponent { get; private set; }

        private readonly TargetNode _root;

        internal ComponentMoveTargetDialog(Component root, Component nodeToMove)
        {
            InitializeComponent();
            var name = nodeToMove.Name;
            tbMessage.Text = $"Move '{(string.IsNullOrEmpty(name) ? nodeToMove.ClassName : name)}' into:";
            _root = Build(root, nodeToMove);
            tvNodes.ItemsSource = new[] { _root };

            Opened += (_, _) =>
            {
                var first = Flatten(_root).FirstOrDefault(node => node.IsEnabled);
                tvNodes.SelectedItem = first;
                Dispatcher.UIThread.Post(() =>
                {
                    var container = tvNodes.GetSelfAndVisualDescendants()
                        .OfType<TreeViewItem>()
                        .FirstOrDefault(item => ReferenceEquals(item.DataContext, first));
                    (container ?? (Control)tvNodes).Focus();
                }, DispatcherPriority.Background);
            };
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                    Close(false);
            };
        }

        private static TargetNode Build(Component component, Component nodeToMove)
        {
            var name = component.Name;
            var node = new TargetNode
            {
                Component = component,
                Title = (string.IsNullOrEmpty(name) ? component.ClassName : name) + (component.Removed ? " (removed)" : string.Empty),
                IsEnabled = ComponentMoveRules.TryResolve(nodeToMove, component, MovePosition.Child, out _, out _)
            };
            foreach (var child in component.Children.Where(c => c.IsVisible))
                node.Children.Add(Build(child, nodeToMove));
            return node;
        }

        private static IEnumerable<TargetNode> Flatten(TargetNode node)
        {
            yield return node;
            foreach (var child in node.Children.SelectMany(Flatten))
                yield return child;
        }

        private void Accept()
        {
            if (tvNodes.SelectedItem is not TargetNode { IsEnabled: true } selected)
                return;
            SelectedComponent = selected.Component;
            Close(true);
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => Accept();

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);

        private void Tree_DoubleTapped(object sender, TappedEventArgs e) => Accept();

        private void Tree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            Accept();
            e.Handled = true;
        }
    }
}
