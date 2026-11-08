using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.View.Viewer;

namespace TypeDitor.View.Toolbars
{
    /// <summary>
    /// Toolbar controls for the viewer camera: navigation speed, position (X, Y), zoomed out distance (Z),
    /// and buttons to reset the view and teleport to the selection.
    /// </summary>
    internal sealed class CameraToolbar : StackPanel
    {
        private const string ResetIcon = "M12,3 L2,12 L5,12 L5,21 L10,21 L10,15 L14,15 L14,21 L19,21 L19,12 L22,12 Z";
        private const string TeleportIcon = "M11,2 L13,2 L13,5.1 A7,7 0 0 1 18.9,11 L22,11 L22,13 L18.9,13 A7,7 0 0 1 13,18.9 L13,22 L11,22 L11,18.9 A7,7 0 0 1 5.1,13 L2,13 L2,11 L5.1,11 A7,7 0 0 1 11,5.1 Z M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7 Z M12,10 A2,2 0 1 0 12,14 A2,2 0 1 0 12,10 Z";

        private readonly IViewerCameraModel _cameraModel;
        private readonly INodeSelectionModel _selectionModel;
        private readonly IHookModel _hookModel;
        private readonly IEditorCommandRegistry _commandRegistry;

        private readonly TextBox _speedBox;
        private readonly TextBox _xBox;
        private readonly TextBox _yBox;
        private readonly TextBox _zBox;
        private readonly Button _resetButton;
        private readonly Button _teleportButton;

        private readonly Action<ComponentFocusHook> _componentFocusedHandler;
        private readonly Action<CloseComponentHook> _componentClosedHandler;
        private readonly Action<ComponentRemovedHook> _componentRemovedHandler;
        private bool _refreshPending;

        public CameraToolbar(IResourceModel resourceModel)
        {
            _cameraModel = resourceModel.Get<IViewerCameraModel>();
            _selectionModel = resourceModel.Get<INodeSelectionModel>();
            _hookModel = resourceModel.Get<IHookModel>();
            _commandRegistry = resourceModel.Get<IEditorCommandRegistry>();

            _componentFocusedHandler = _ => QueueRefresh();
            _componentClosedHandler = _ => QueueRefresh();
            _componentRemovedHandler = _ => QueueRefresh();

            Orientation = Orientation.Horizontal;
            Spacing = 4;
            VerticalAlignment = VerticalAlignment.Center;

            _speedBox = AddField("Speed", "Keyboard navigation speed of the camera, in viewer units per second (hold Shift for faster)", 60,
                () => _cameraModel.Speed,
                value => _cameraModel.Speed = value);
            _xBox = AddField("X", "Camera X position, the world point at the left edge of the viewer", 64,
                () => _cameraModel.Camera?.X,
                value => _cameraModel.Camera?.Set(value, _cameraModel.Camera.Y, _cameraModel.Camera.Zoom));
            _yBox = AddField("Y", "Camera Y position, the world point at the top edge of the viewer", 64,
                () => _cameraModel.Camera?.Y,
                value => _cameraModel.Camera?.Set(_cameraModel.Camera.X, value, _cameraModel.Camera.Zoom));
            _zBox = AddField("Z", $"How far the camera is zoomed out: 1 is 100%, 2 shows twice as much ({1 / ViewerCamera.MaxZoom:0.###} to {1 / ViewerCamera.MinZoom:0.##})", 52,
                () => _cameraModel.Camera?.ZoomOut,
                value => _cameraModel.Camera?.SetZoomOut(value));

            _resetButton = AddButton(ResetIcon, "viewer.reset-view", "Reset the camera view");
            _teleportButton = AddButton(TeleportIcon, "viewer.teleport-to-selection", "Teleport the camera to the selected node");

            Refresh();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _cameraModel.Changed += QueueRefresh;
            _hookModel.AddHook(_componentFocusedHandler);
            _hookModel.AddHook(_componentClosedHandler);
            _hookModel.AddHook(_componentRemovedHandler);
            Refresh();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _cameraModel.Changed -= QueueRefresh;
            _hookModel.RemoveHook(_componentFocusedHandler);
            _hookModel.RemoveHook(_componentClosedHandler);
            _hookModel.RemoveHook(_componentRemovedHandler);
            base.OnDetachedFromVisualTree(e);
        }

        private TextBox AddField(string label, string toolTip, double width, Func<double?> read, Action<double> write)
        {
            var text = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Children.Count == 0 ? 2 : 4, 0, 0, 0)
            };
            var box = new TextBox
            {
                Width = width,
                MinHeight = 0,
                MinWidth = 0,
                Padding = new Thickness(4, 2),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = read
            };
            ToolTip.SetTip(text, toolTip);
            ToolTip.SetTip(box, toolTip);

            void Commit()
            {
                if (TryParse(box.Text, out var value))
                    write(value);
                box.Text = Format(read());
            }

            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Commit();
                    box.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    box.Text = Format(read());
                    box.SelectAll();
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => Commit();

            Children.Add(text);
            Children.Add(box);
            return box;
        }

        private Button AddButton(string iconData, string commandId, string fallbackToolTip)
        {
            var button = new Button
            {
                Padding = new Thickness(5),
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new PathIcon
                {
                    Data = Avalonia.Media.Geometry.Parse(iconData),
                    Width = 16,
                    Height = 16
                }
            };

            string ToolTipText()
            {
                var command = _commandRegistry.Commands.FirstOrDefault(command => command.Id == commandId);
                if (command == null)
                    return fallbackToolTip;
                return string.IsNullOrEmpty(command.Shortcut) ? command.Name : $"{command.Name} ({command.Shortcut})";
            }

            ToolTip.SetTip(button, ToolTipText());
            // Shortcuts can be changed in the options, so refresh the tooltip before it shows
            button.PointerEntered += (_, _) => ToolTip.SetTip(button, ToolTipText());
            button.Click += (_, _) => _commandRegistry.Execute(commandId);

            Children.Add(button);
            return button;
        }

        private void QueueRefresh()
        {
            // The camera can move every rendered frame, so update the fields at most once per UI cycle
            if (_refreshPending)
                return;

            _refreshPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _refreshPending = false;
                Refresh();
            }, DispatcherPriority.Background);
        }

        private void Refresh()
        {
            var hasCamera = _cameraModel.Camera != null;
            foreach (var box in new[] { _speedBox, _xBox, _yBox, _zBox })
            {
                // Don't overwrite what the user is typing
                if (!box.IsFocused)
                    box.Text = Format((box.Tag as Func<double?>)?.Invoke());
            }

            _xBox.IsEnabled = hasCamera;
            _yBox.IsEnabled = hasCamera;
            _zBox.IsEnabled = hasCamera;
            _resetButton.IsEnabled = hasCamera;
            _teleportButton.IsEnabled = hasCamera && HasSelection();
        }

        private bool HasSelection()
        {
            var node = _selectionModel.SelectedNode;
            return node is { Removed: false } && _selectionModel.OpenedComponent != null && !ReferenceEquals(node, _selectionModel.OpenedComponent);
        }

        internal static string Format(double? value)
        {
            if (value is not double number)
                return string.Empty;
            return Math.Round(number, 3).ToString("0.###", CultureInfo.InvariantCulture);
        }

        internal static bool TryParse(string text, out double value)
        {
            // Accept both decimal separators so the fields work the same with any keyboard layout
            var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        }
    }
}
