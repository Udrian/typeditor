using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using TypeD.Models.Data;
using TypeDitor.ViewModel.Panels;
using TypeD.ViewModel;

namespace TypeDitor.View.Panels
{
    /// <summary>Displays editable properties for components in a project.</summary>
    public partial class PropertiesPanel : UserControl
    {
        // ViewModel
        PropertiesViewModel ViewModel { get; set; }
        private Border _pressedHeader;
        private Point _pressPosition;
        private bool _dragging;

        /// <summary>Creates a properties panel for the specified project.</summary>
        public PropertiesPanel(Project project)
        {
            InitializeComponent();
            DataContext = ViewModel = new PropertiesViewModel(this, project);
        }

        private void GroupHeader_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Border header || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
                return;

            _pressedHeader = header;
            _pressPosition = e.GetPosition(PropertiesList);
            _dragging = false;
            e.Pointer.Capture(header);
            e.Handled = true;
        }

        private void GroupHeader_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_pressedHeader == null)
                return;

            var position = e.GetPosition(PropertiesList);
            if (!_dragging && Math.Abs(position.Y - _pressPosition.Y) + Math.Abs(position.X - _pressPosition.X) >= 5)
            {
                _dragging = true;
                _pressedHeader.Opacity = 0.6;
            }
            e.Handled = true;
        }

        private void GroupHeader_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            var sourceContext = _pressedHeader?.DataContext;
            if (sourceContext is not PropertyGroup && sourceContext is not PropertyNode)
                return;

            var dragging = _dragging;
            var position = e.GetPosition(PropertiesList);
            ResetDrag();
            e.Pointer.Capture(null);
            e.Handled = true;

            if (sourceContext is PropertyNode property)
            {
                if (dragging)
                    DropProperty(property, position);
                return;
            }

            var source = (PropertyGroup)sourceContext;
            if (!dragging)
            {
                source.IsExpanded = !source.IsExpanded;
                return;
            }

            var groups = PropertiesList.GetVisualDescendants().OfType<StackPanel>()
                .Where(panel => panel.Classes.Contains("PropertyGroup"))
                .Select(panel => new { Group = panel.DataContext as PropertyGroup, Position = panel.TranslatePoint(default, PropertiesList), Height = panel.Bounds.Height })
                .Where(item => item.Group != null && item.Position.HasValue)
                .OrderBy(item => item.Position.Value.Y)
                .ToList();
            var target = groups.FirstOrDefault(item => position.Y < item.Position.Value.Y + item.Height)
                ?? groups.LastOrDefault();
            if (target != null)
                ViewModel.MoveGroup(source.Name, target.Group.Name, position.Y > target.Position.Value.Y + target.Height / 2);
        }

        private void DropProperty(PropertyNode source, Point position)
        {
            var groupPanel = PropertiesList.GetVisualDescendants().OfType<StackPanel>()
                .FirstOrDefault(panel => panel.Classes.Contains("PropertyGroup")
                    && panel.DataContext is PropertyGroup group && group.Name == source.Group);
            var groupPosition = groupPanel?.TranslatePoint(default, PropertiesList);
            if (groupPanel == null || !groupPosition.HasValue
                || position.Y < groupPosition.Value.Y || position.Y > groupPosition.Value.Y + groupPanel.Bounds.Height)
                return;

            var labels = groupPanel.GetVisualDescendants().OfType<Border>()
                .Where(label => label.Classes.Contains("PropertyLabel") && label.DataContext is PropertyNode)
                .Select(label => new
                {
                    Node = (PropertyNode)label.DataContext,
                    Position = label.TranslatePoint(default, PropertiesList),
                    Height = label.Bounds.Height
                })
                .Where(item => item.Position.HasValue)
                .OrderBy(item => item.Position.Value.Y)
                .ToList();
            var target = labels.FirstOrDefault(item => position.Y < item.Position.Value.Y + item.Height)
                ?? labels.LastOrDefault();
            if (target != null)
                ViewModel.MoveProperty(source, target.Node, position.Y > target.Position.Value.Y + target.Height / 2);
        }

        private void GroupHeader_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e) => ResetDrag();

        private void ResetDrag()
        {
            if (_pressedHeader != null)
                _pressedHeader.Opacity = 1;
            _pressedHeader = null;
            _dragging = false;
        }
    }
}