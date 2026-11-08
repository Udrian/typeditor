using Avalonia.Controls;
using TypeD.Helpers;
using TypeDCore.Models.Data.Hooks;
using Avalonia.Threading;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.View.Viewer;
using TypeD.ViewModel;
using Avalonia.Input;
using TypeDCore.Models;
using TypeDitor.View.Panels;

namespace TypeDitor.ViewModel.Panels
{
    internal class ViewerViewModel : ViewModelBase
    {
        // Models
        IHookModel HookModel { get; set; }
        IPanelModel PanelModel { get; set; }
        ISettingModel SettingModel { get; set; }
        IEditorCommandRegistry EditorCommandRegistry { get; set; }
        IViewerCameraModel ViewerCameraModel { get; set; }

        // Data
        ViewerPanel ViewerPanel { get; set; }
        Project Project { get; set; }
        /// <summary>
        /// The project displayed in the viewer, used as the click parameter of context menu items.
        /// </summary>
        public Project LoadedProject => Project;
        IViewer Viewer { get; set; }
        Component ComponentToReload { get; set; }
        bool IsBuildInProgress { get; set; }
        bool IsGameStopped { get; set; }
        (double X, double Y, double Zoom)? CameraToRestore { get; set; }
        string CameraComponent { get; set; }
        Button ZoomButton { get; set; }
        ViewerCamera AttachedCamera { get; set; }

        // Constructors
        public ViewerViewModel(Project project, ViewerPanel viewerPanel) : base(viewerPanel)
        {
            ViewerPanel = viewerPanel;
            Project = project;

            HookModel = ResourceModel.Get<IHookModel>();
            PanelModel = ResourceModel.Get<IPanelModel>();
            SettingModel = ResourceModel.Get<ISettingModel>();
            EditorCommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            ViewerCameraModel = ResourceModel.Get<IViewerCameraModel>();

            HookModel.AddHook<OpenComponentHook>(ComponentOpened);
            HookModel.AddHook<CloseComponentHook>(ComponentClosed);
            HookModel.AddHook<ComponentFocusHook>(ComponentFocus);
            HookModel.AddHook<BuildStatusChangedHook>(BuildStatusChanged);
            HookModel.AddHook<ViewerContextMenuRequestedHook>(ContextMenuRequested);
            HookModel.AddHook<GameStopHook>(GameStop);
            HookModel.AddHook<GameStartHook>(GameStart);
        }

        // Functions
        void ContextMenuRequested(ViewerContextMenuRequestedHook hook)
        {
            if (hook.Project != Project || hook.Component == null)
                return;

            Dispatcher.UIThread.Post(() => ShowContextMenu(hook.Component));
        }

        void ShowContextMenu(Component component)
        {
            if (Viewer is not Control viewerControl)
                return;

            var menuHook = new ComponentContextMenuHook()
            {
                Menu = new TypeD.View.Menu(),
                Project = Project,
                OpenedComponent = Viewer.Component,
                SelectedComponent = component
            };
            HookModel.Shoot(menuHook);

            var contextMenu = new ContextMenu() { Placement = PlacementMode.Pointer };
            foreach (var menu in menuHook.Menu.Items)
            {
                ViewHelper.InitMenu(contextMenu, menu, this);
            }

            if (contextMenu.Items.Count > 0)
                contextMenu.Open(viewerControl);
        }

        public void Load()
        {
            if (Viewer == null)
            {
                IsGameStopped = false;
                var setting = SettingModel.GetContext<MainWindowSettingContext>();

                Viewer = PanelModel.CreateViewer(Project, setting.ViewerType.Value);
                var viewerControl = Viewer as Control;
                if (viewerControl == null) return;
                Grid.SetRow(viewerControl, 1);
                ViewerPanel.ViewerHost.Children.Add(viewerControl);

                Viewer.Init();
                AttachCamera();
                if (DropHandler == null)
                    AttachDrop(viewerControl);
            }
        }

        void AttachCamera()
        {
            DetachCamera();
            var camera = Viewer?.Camera;
            if (camera == null)
            {
                if (ZoomButton != null)
                    ZoomButton.IsVisible = false;
                return;
            }

            // The game is recreated on every reload, the view should stay where the user left it
            if (CameraToRestore is { } restore)
                camera.Set(restore.X, restore.Y, restore.Zoom);
            CameraToRestore = null;

            if (ZoomButton == null)
            {
                ZoomButton = new Button
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                    Margin = new Avalonia.Thickness(6),
                    Padding = new Avalonia.Thickness(6, 2),
                    FontSize = 11,
                    Opacity = 0.8,
                    Focusable = false,
                    ZIndex = 1
                };
                ToolTip.SetTip(ZoomButton, CameraHelpText());
                // Shortcuts can be changed in the options, so refresh the help when it's about to show
                ZoomButton.PointerEntered += (_, _) => ToolTip.SetTip(ZoomButton, CameraHelpText());
                ZoomButton.Click += (_, _) => Viewer?.Camera?.Reset();
                ViewerPanel.ViewerHost.Children.Add(ZoomButton);
            }
            ZoomButton.IsVisible = true;

            AttachedCamera = camera;
            camera.Changed += CameraChanged;
            ViewerCameraModel?.Attach(camera);
            CameraChanged();
        }

        string CameraHelpText()
        {
            var text = "Click to reset the view\n\n" +
                "Pan: middle mouse drag, Space + left drag, or WASD / arrow keys (hold Shift for faster)\n" +
                "Zoom: mouse wheel at the pointer, or + / -";
            foreach (var id in new[] { "viewer.teleport-to-selection", "viewer.reset-view" })
            {
                var command = EditorCommandRegistry.Commands.FirstOrDefault(command => command.Id == id);
                if (command != null)
                    text += $"\n{command.Name}: {(string.IsNullOrEmpty(command.Shortcut) ? "no shortcut" : command.Shortcut)}";
            }
            return text;
        }

        void DetachCamera()
        {
            if (AttachedCamera != null)
            {
                AttachedCamera.Changed -= CameraChanged;
                ViewerCameraModel?.Detach(AttachedCamera);
            }
            AttachedCamera = null;
        }

        void CameraChanged()
        {
            RunOnUIThread(() =>
            {
                if (ZoomButton != null && AttachedCamera != null)
                    ZoomButton.Content = $"{Math.Round(AttachedCamera.Zoom * 100)}%";
            });
        }

        private ViewerDropHandler DropHandler { get; set; }

        void AttachDrop(Control viewerControl)
        {
            DropHandler = new ViewerDropHandler(ResourceModel);
            ViewerPanel.ViewerHost.SetValue(DragDrop.AllowDropProperty, true);
            ViewerPanel.ViewerHost.AddHandler(DragDrop.DragOverEvent, (_, e) =>
            {
                var canDrop = Viewer?.Component != null && !Viewer.Component.ReadOnly && DropHandler.CanDrop(Project, Viewer.Component);
                e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            });
            ViewerPanel.ViewerHost.AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                e.Handled = true;
                var root = Viewer?.Component;
                if (root == null || root.ReadOnly)
                    return;

                var position = e.GetPosition(Viewer as Control ?? viewerControl);
                var (x, y) = Viewer.Camera?.ScreenToWorld(position.X, position.Y) ?? (position.X, position.Y);
                DropHandler.Drop(Project, root, x, y);
            });
        }

        public void Unload()
        {
            HookModel.RemoveHook<OpenComponentHook>(ComponentOpened);
            HookModel.RemoveHook<CloseComponentHook>(ComponentClosed);
            HookModel.RemoveHook<ComponentFocusHook>(ComponentFocus);
            HookModel.RemoveHook<BuildStatusChangedHook>(BuildStatusChanged);
            HookModel.RemoveHook<ViewerContextMenuRequestedHook>(ContextMenuRequested);
            HookModel.RemoveHook<GameStopHook>(GameStop);
            HookModel.RemoveHook<GameStartHook>(GameStart);

            DetachCamera();
            if (Viewer != null)
            {
                Viewer.Unload();
            }
        }

        void GameStop(GameStopHook hook)
        {
            if (hook.Project != Project)
                return;

            RunOnUIThread(() =>
            {
                IsGameStopped = true;
                if (Viewer == null)
                    return;

                if (Viewer.Component is Component component)
                    ComponentToReload = component;
                if (Viewer.Camera is ViewerCamera camera)
                    CameraToRestore = (camera.X, camera.Y, camera.Zoom);
                DetachCamera();

                // Removing the viewer shuts down its game instance, a new viewer with a new game is created on start
                Viewer.Unload();
                ViewerPanel.ViewerHost.Children.Remove(Viewer as Control);
                Viewer = null;
            });
        }

        void GameStart(GameStartHook hook)
        {
            if (hook.Project != Project)
                return;

            RunOnUIThread(RestartGame);
        }

        void RestartGame()
        {
            var component = ComponentToReload;
            ComponentToReload = null;

            Load();
            if (component != null && !component.Removed)
                Viewer.Load(component);
        }

        static void RunOnUIThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        void ComponentOpened(OpenComponentHook hook)
        {
        }

        void ComponentClosed(CloseComponentHook hook)
        {
            if (ComponentToReload != null && ComponentToReload.FullName == hook.Component.FullName)
                ComponentToReload = null;

            if (Viewer != null)
            {
                Viewer.Unload();
            }
        }

        void ComponentFocus(ComponentFocusHook hook)
        {
            if (!hook.Root)
                return;

            if (IsBuildInProgress || IsGameStopped)
            {
                ComponentToReload = hook.Component;
                return;
            }

            if (Viewer != null)
            {
                // Opening another component starts at its origin, reopening the same one keeps the view
                if (CameraComponent != hook.Component?.FullName)
                    Viewer.Camera?.Reset();
                CameraComponent = hook.Component?.FullName;
                Viewer.Load(hook.Component);
            }
        }

        void BuildStatusChanged(BuildStatusChangedHook hook)
        {
            if (hook.Project != Project)
                return;

            if (hook.IsBuilding)
            {
                // Progress updates of a running build are published as building as well
                if (IsBuildInProgress)
                    return;

                Action beginBuild = () =>
                {
                    IsBuildInProgress = true;
                    if (Viewer?.Component is Component component)
                        ComponentToReload = component;
                };

                if (Dispatcher.UIThread.CheckAccess())
                    beginBuild();
                else
                    Dispatcher.UIThread.InvokeAsync(beginBuild).GetAwaiter().GetResult();
                return;
            }

            if (!hook.IsComplete)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                IsBuildInProgress = false;

                // A stopped game keeps the component to reload until it starts, a successful build starts it again
                if (IsGameStopped)
                {
                    if (hook.Succeeded)
                        RestartGame();
                    return;
                }

                var component = ComponentToReload;
                ComponentToReload = null;

                if (!hook.Succeeded || component == null || Viewer == null)
                    return;

                Viewer.Unload();
                Viewer.Load(component);
            });
        }
    }
}
