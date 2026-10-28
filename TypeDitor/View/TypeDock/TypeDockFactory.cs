using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;

namespace TypeDitor.View.TypeDock
{
    internal sealed class TypeDockFactory : Factory
    {
        private IResourceModel ResourceModel { get; set; }
        private IPanelModel PanelModel { get; set; }
        private IHookModel HookModel { get; set; }

        private const string CenterDockId = "typed_main_center";
        private readonly Dictionary<string, Tool> _tools = new(StringComparer.Ordinal);
        private IRootDock _layout;

        public TypeDockFactory(IResourceModel resourceModel) : base()
        {
            ResourceModel = resourceModel;
            PanelModel = ResourceModel.Get<IPanelModel>();
            HookModel = ResourceModel.Get<IHookModel>();

            HookModel.AddHook<PanelStatusChangedHook>(OnPanelStatusChanged);
        }

        private void OnPanelStatusChanged(PanelStatusChangedHook hook)
        {
            if (hook.Panel == null)
                return;
            if (hook.Status == PanelStatusChangedHook.PanelStatus.Opened || hook.Status == PanelStatusChangedHook.PanelStatus.Attached)
            {
                var tool = FindTool(hook.Panel.ID);
                if (tool == null)
                {
                    AddPanel(hook.Panel);
                }
            }
            else if (hook.Status == PanelStatusChangedHook.PanelStatus.Closed || hook.Status == PanelStatusChangedHook.PanelStatus.Detached)
            {
                var tool = FindTool(hook.Panel.ID);
                if (tool != null)
                    RemovePanel(hook.Panel.ID);
            }
        }

        public void AddPanel(TypeD.View.Panel panel)
        {
            var tool = CreatePanelTool(panel);
            if (_layout == null)
                return;

            var targetDock = FindToolDock(panel.DefaultDockPosition) ?? CreateToolDockAtDefaultPosition(panel.DefaultDockPosition);
            AddDockable(targetDock, tool);
        }

        public void RemovePanel(string panelId)
        {
            var tool = FindTool(panelId);
            if (tool?.Owner is IDock)
                RemoveDockable(tool, collapse: true);

            _tools.Remove(panelId);
        }

        public void InitializeLayout(IRootDock layout)
        {
            var panels = PanelModel.GetPanels();
            _layout = layout;
            _tools.Clear();
            InitLayout(layout);

            foreach (var staleTool in EnumerateDockables(layout)
                         .OfType<Tool>()
                         .Where(tool => !panels.Any(p => p.ID == tool.Id))
                         .ToList())
            {
                RemovePanel(staleTool.Id);
            }

            foreach (var panel in panels)
            {
                var existing = FindTool(panel.ID);
                if (existing != null)
                {
                    PanelModel.SetPanelStatusWithoutNotify(panel.ID, existing.IsOpen);
                    existing.Content = panel.PanelView;
                    _tools[panel.ID] = existing;
                }
                else
                {
                    AddPanel(panel);
                }
            }
        }

        public override IRootDock CreateLayout()
        {
            _tools.Clear();
            var panels = PanelModel.GetPanels();
            foreach(var panel in panels)
            {
                CreatePanelTool(panel);
            }

            var leftTools = panels
                .Where(panel => panel.DefaultDockPosition == Alignment.Left)
                .Select(panel => _tools[panel.ID])
                .ToList();
            var bottomTools = panels
                .Where(panel => panel.DefaultDockPosition == Alignment.Bottom)
                .Select(panel => _tools[panel.ID])
                .ToList();
            var rightTools = panels
                .Where(panel => panel.DefaultDockPosition == Alignment.Right)
                .Select(panel => _tools[panel.ID])
                .ToList();
            var centerTools = panels
                .Where(panel => panel.DefaultDockPosition == Alignment.Unset)
                .Select(panel => _tools[panel.ID])
                .ToList();

            var centerDock = CreateToolDock(CenterDockId, Alignment.Unset, centerTools);
            IDock centerArea = centerDock;
            if (bottomTools.Count > 0 || rightTools.Count > 0)
            {
                centerDock.Proportion = 0.7;
                IDock lowerArea;
                if (bottomTools.Count > 0 && rightTools.Count > 0)
                {
                    var bottomDock = CreateToolDock("typed_main_bottom", Alignment.Bottom, bottomTools);
                    var rightDock = CreateToolDock("typed_main_right", Alignment.Right, rightTools);
                    lowerArea = new ProportionalDock
                    {
                        Id = "typed_main_bottom_layout",
                        Orientation = Orientation.Horizontal,
                        VisibleDockables = CreateList<IDockable>(
                            bottomDock,
                            new ProportionalDockSplitter { CanResize = true },
                            rightDock)
                    };
                }
                else if (bottomTools.Count > 0)
                {
                    lowerArea = CreateToolDock("typed_main_bottom", Alignment.Bottom, bottomTools);
                }
                else
                {
                    lowerArea = CreateToolDock("typed_main_right", Alignment.Right, rightTools);
                }
                lowerArea.Proportion = 0.3;

                centerArea = new ProportionalDock
                {
                    Id = "typed_main_center_layout",
                    Orientation = Orientation.Vertical,
                    VisibleDockables = CreateList<IDockable>(
                        centerDock,
                        new ProportionalDockSplitter { CanResize = true },
                        lowerArea)
                };
            }

            IDock mainLayout = centerArea;
            if (leftTools.Count > 0)
            {
                var leftDock = CreateToolDock("typed_main_left", Alignment.Left, leftTools);
                centerArea.Proportion = 0.75;
                mainLayout = new ProportionalDock
                {
                    Id = "typed_main_layout",
                    Orientation = Orientation.Horizontal,
                    VisibleDockables = CreateList<IDockable>(
                        leftDock,
                        new ProportionalDockSplitter { CanResize = true },
                        centerArea)
                };
            }

            return new RootDock
            {
                Id = "typed_main_root",
                Title = "TypeO",
                VisibleDockables = CreateList<IDockable>(mainLayout),
                ActiveDockable = mainLayout
            };
        }

        private ToolDock CreateToolDock(string id, Alignment alignment, IReadOnlyList<Tool> tools)
        {
            return new ToolDock
            {
                Id = id,
                Alignment = alignment,
                Proportion = alignment == Alignment.Left ? 0.25 : alignment == Alignment.Bottom ? 0.3 : 1,
                VisibleDockables = CreateList<IDockable>(tools.Cast<IDockable>().ToArray()),
                ActiveDockable = tools.FirstOrDefault(),
            };
        }

        private Tool CreatePanelTool(TypeD.View.Panel panel)
        {
            var tool = new Tool
            {
                Id = panel.ID,
                Title = panel.Title,
                Content = panel.PanelView,
            };
            tool.PropertyChanged += (sender, e) =>
            {
                if (e.Property.Name == nameof(Tool.IsOpen))
                {
                    var panels = PanelModel.GetPanels();
                    if (panels.First(p => p.ID == panel.ID).Open != (bool)e.NewValue)
                        PanelModel.SetPanelStatusWithoutNotify(panel.ID, (bool)e.NewValue);
                }
            };
            _tools[panel.ID] = tool;
            return tool;
        }

        private Tool FindTool(string panelId)
        {
            if (_layout != null)
                return EnumerateDockables(_layout).OfType<Tool>().FirstOrDefault(tool => tool.Id == panelId);

            return _tools.TryGetValue(panelId, out var registeredTool) ? registeredTool : null;
        }

        private IToolDock GetCenterDock()
        {
            if (_layout == null)
                throw new InvalidOperationException("The Dock layout has not been initialized.");

            if (FindDockable(_layout, dockable => dockable.Id == CenterDockId) is IToolDock centerDock)
                return centerDock;

            if (FindDockable(_layout, dockable => dockable is IToolDock) is IToolDock firstToolDock)
                return firstToolDock;

            throw new InvalidOperationException("The Dock layout does not contain a tool dock for panels.");
        }

        private IToolDock FindToolDock(Alignment alignment)
        {
            if (alignment == Alignment.Unset)
                return GetCenterDock();

            var dockId = alignment switch
            {
                Alignment.Left => "typed_main_left",
                Alignment.Right => "typed_main_right",
                Alignment.Top => "typed_main_top",
                Alignment.Bottom => "typed_main_bottom",
                _ => null
            };

            if (dockId != null &&
                EnumerateDockables(_layout).OfType<IToolDock>().FirstOrDefault(dock => dock.Id == dockId) is { } namedDock)
            {
                return namedDock;
            }

            return EnumerateDockables(_layout)
                .OfType<IToolDock>()
                .FirstOrDefault(dock => dock.Alignment == alignment);
        }

        private IToolDock CreateToolDockAtDefaultPosition(Alignment alignment)
        {
            if (alignment == Alignment.Unset)
                return GetCenterDock();

            var dockId = alignment switch
            {
                Alignment.Left => "typed_main_left",
                Alignment.Right => "typed_main_right",
                Alignment.Top => "typed_main_top",
                Alignment.Bottom => "typed_main_bottom",
                _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unsupported default dock position.")
            };
            var toolDock = CreateToolDock(dockId, alignment, Array.Empty<Tool>());
            var rootDockables = _layout.VisibleDockables;
            if (rootDockables == null || rootDockables.Count == 0)
                throw new InvalidOperationException("The Dock layout does not contain a root layout to attach a panel dock.");

            var existingDockables = rootDockables.ToArray();
            var mainLayout = CreateProportionalDock();
            mainLayout.Id = $"{dockId}_existing_layout";
            mainLayout.Proportion = alignment is Alignment.Left or Alignment.Right ? 0.75 : 0.7;
            mainLayout.VisibleDockables = CreateList<IDockable>(existingDockables);
            foreach (var dockable in existingDockables)
                dockable.Owner = mainLayout;

            var splitter = CreateProportionalDockSplitter();
            var beforeExistingLayout = alignment is Alignment.Left or Alignment.Top;
            var proportionalDock = CreateProportionalDock();
            proportionalDock.Id = $"{dockId}_layout";
            proportionalDock.Orientation = alignment is Alignment.Left or Alignment.Right
                ? Orientation.Horizontal
                : Orientation.Vertical;
            proportionalDock.VisibleDockables = beforeExistingLayout
                ? CreateList<IDockable>(toolDock, splitter, mainLayout)
                : CreateList<IDockable>(mainLayout, splitter, toolDock);
            toolDock.Proportion = alignment is Alignment.Left or Alignment.Right ? 0.25 : 0.3;
            toolDock.Owner = proportionalDock;
            splitter.Owner = proportionalDock;
            mainLayout.Owner = proportionalDock;
            proportionalDock.Owner = _layout;

            rootDockables.Clear();
            rootDockables.Add(proportionalDock);
            _layout.ActiveDockable = proportionalDock;
            return toolDock;
        }

        private IEnumerable<IDockable> EnumerateDockables(IDockable dockable)
        {
            return EnumerateDockables(dockable, new HashSet<IDockable>());
        }

        private IEnumerable<IDockable> EnumerateDockables(IDockable dockable, HashSet<IDockable> visited)
        {
            if (!visited.Add(dockable))
                yield break;

            yield return dockable;
            if (dockable is not IDock dock)
                yield break;

            if (dock.VisibleDockables != null)
            {
                foreach (var child in dock.VisibleDockables)
                {
                    foreach (var descendant in EnumerateDockables(child, visited))
                        yield return descendant;
                }
            }

            if (dock is ISplitViewDock splitViewDock)
            {
                if (splitViewDock.PaneDockable != null &&
                    dock.VisibleDockables?.Contains(splitViewDock.PaneDockable) != true)
                {
                    foreach (var descendant in EnumerateDockables(splitViewDock.PaneDockable, visited))
                        yield return descendant;
                }

                if (splitViewDock.ContentDockable != null &&
                    !ReferenceEquals(splitViewDock.ContentDockable, splitViewDock.PaneDockable) &&
                    dock.VisibleDockables?.Contains(splitViewDock.ContentDockable) != true)
                {
                    foreach (var descendant in EnumerateDockables(splitViewDock.ContentDockable, visited))
                        yield return descendant;
                }
            }

            if (dock is IRootDock rootDock)
            {
                foreach (var hiddenDockable in rootDock.HiddenDockables ?? Array.Empty<IDockable>())
                {
                    foreach (var descendant in EnumerateDockables(hiddenDockable, visited))
                        yield return descendant;
                }

                foreach (var pinnedDockable in rootDock.LeftPinnedDockables ?? Array.Empty<IDockable>())
                {
                    foreach (var descendant in EnumerateDockables(pinnedDockable, visited))
                        yield return descendant;
                }

                foreach (var pinnedDockable in rootDock.RightPinnedDockables ?? Array.Empty<IDockable>())
                {
                    foreach (var descendant in EnumerateDockables(pinnedDockable, visited))
                        yield return descendant;
                }

                foreach (var pinnedDockable in rootDock.TopPinnedDockables ?? Array.Empty<IDockable>())
                {
                    foreach (var descendant in EnumerateDockables(pinnedDockable, visited))
                        yield return descendant;
                }

                foreach (var pinnedDockable in rootDock.BottomPinnedDockables ?? Array.Empty<IDockable>())
                {
                    foreach (var descendant in EnumerateDockables(pinnedDockable, visited))
                        yield return descendant;
                }

                if (rootDock.PinnedDock != null)
                {
                    foreach (var descendant in EnumerateDockables(rootDock.PinnedDock, visited))
                        yield return descendant;
                }

                foreach (var window in rootDock.Windows ?? Array.Empty<IDockWindow>())
                {
                    if (window.Layout == null)
                        continue;

                    foreach (var descendant in EnumerateDockables(window.Layout, visited))
                        yield return descendant;
                }
            }
        }
    }
}
