using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TypeD.Models.Interfaces;

namespace TypeDitor.View.Toolbars
{
    /// <summary>
    /// Toolbar controls for the simulation of the viewer: play or pause the update loop, force the delta time
    /// and scale it with a speed multiplier.
    /// </summary>
    internal sealed class SimulationToolbar : StackPanel
    {
        private const string PlayIcon = "M8,5 L19,12 L8,19 Z";
        private const string PauseIcon = "M6,5 L10,5 L10,19 L6,19 Z M14,5 L18,5 L18,19 L14,19 Z";
        private const string ResetIcon = "M12,4 C7.58,4 4,7.58 4,12 C4,16.42 7.58,20 12,20 C15.73,20 18.84,17.45 19.73,14 L17.65,14 C16.83,16.33 14.61,18 12,18 C8.69,18 6,15.31 6,12 C6,8.69 8.69,6 12,6 C13.66,6 15.14,6.69 16.22,7.78 L13,11 L20,11 L20,4 L17.65,6.35 C16.2,4.9 14.21,4 12,4 Z";

        private readonly ISimulationModel _model;
        private readonly ToggleButton _playButton;
        private readonly PathIcon _playIcon;
        private readonly CheckBox _forceCheckBox;
        private readonly TextBox _forcedDtBox;
        private readonly TextBox _speedBox;
        private readonly Action _changedHandler;
        private bool _refreshPending;

        public SimulationToolbar(IResourceModel resourceModel)
        {
            _model = resourceModel.Get<ISimulationModel>();
            _changedHandler = QueueRefresh;

            Orientation = Orientation.Horizontal;
            Spacing = 4;
            VerticalAlignment = VerticalAlignment.Center;

            _playIcon = new PathIcon { Width = 16, Height = 16 };
            _playButton = new ToggleButton
            {
                Padding = new Thickness(5),
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = _playIcon
            };
            ToolTip.SetTip(_playButton, "Play or pause the update loop of the viewer");
            _playButton.Click += (_, _) => _model.IsPlaying = _playButton.IsChecked == true;
            Children.Add(_playButton);

            var resetButton = new Button
            {
                Padding = new Thickness(5),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new PathIcon { Data = Avalonia.Media.Geometry.Parse(ResetIcon), Width = 16, Height = 16 }
            };
            ToolTip.SetTip(resetButton, "Reset the simulation: reload and restart the current view");
            resetButton.Click += (_, _) => _model.RequestReset();
            Children.Add(resetButton);

            _forceCheckBox = new CheckBox
            {
                Content = "Force dt",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            ToolTip.SetTip(_forceCheckBox, "Replace the delta time of the update loop with a fixed number of seconds");
            _forceCheckBox.IsCheckedChanged += (_, _) => _model.ForceDeltaTime = _forceCheckBox.IsChecked == true;
            Children.Add(_forceCheckBox);

            _forcedDtBox = AddBox(60, "The delta time, in seconds, the update loop is run with",
                () => _model.ForcedDeltaTime, value => _model.ForcedDeltaTime = value);

            Children.Add(new TextBlock
            {
                Text = "Speed",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });
            _speedBox = AddBox(52, "Multiplier applied to the delta time of the update loop, 1 is normal speed",
                () => _model.SpeedMultiplier, value => _model.SpeedMultiplier = value);

            Refresh();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _model.Changed += _changedHandler;
            Refresh();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _model.Changed -= _changedHandler;
            base.OnDetachedFromVisualTree(e);
        }

        private TextBox AddBox(double width, string toolTip, Func<double> read, Action<double> write)
        {
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
            ToolTip.SetTip(box, toolTip);

            void Commit()
            {
                if (CameraToolbar.TryParse(box.Text, out var value))
                    write(value);
                box.Text = CameraToolbar.Format(read());
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
                    box.Text = CameraToolbar.Format(read());
                    box.SelectAll();
                    e.Handled = true;
                }
            };
            box.LostFocus += (_, _) => Commit();

            Children.Add(box);
            return box;
        }

        private void QueueRefresh()
        {
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
            _playButton.IsChecked = _model.IsPlaying;
            _playIcon.Data = Avalonia.Media.Geometry.Parse(_model.IsPlaying ? PauseIcon : PlayIcon);
            ToolTip.SetTip(_playButton, _model.IsPlaying ? "Pause the update loop of the viewer" : "Play the update loop of the viewer");
            _forceCheckBox.IsChecked = _model.ForceDeltaTime;
            _forcedDtBox.IsEnabled = _model.ForceDeltaTime;

            foreach (var box in new[] { _forcedDtBox, _speedBox })
            {
                // Don't overwrite what the user is typing
                if (!box.IsFocused)
                    box.Text = CameraToolbar.Format((box.Tag as Func<double>)?.Invoke());
            }
        }
    }
}
