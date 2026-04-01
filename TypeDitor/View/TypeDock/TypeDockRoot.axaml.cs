using Avalonia.Controls;
using Dock.Model;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Avalonia.Core;
using Dock.Model.Core;
using TypeD.Models.Data.SettingContexts;

namespace TypeDitor.View.TypeDock
{
    /// <summary>
    /// Interaction logic for TypeDockRoot.xaml
    /// </summary>
    public partial class TypeDockRoot : UserControl
    {
        // Properties

        // Constructors
        public TypeDockRoot()
        {
            DataContext = this;
            InitializeComponent();
        }

        // Functions
        public void AddPanel(TypeD.View.Panel panel, MainWindowSettingContext.Panel panelSetting)
        {
            //TODO: I should rewrite this whole thing and use the Avalonia Dock layout save and plugin system.
            var tool = new Tool()
            {
                Id = panel.ID,
                Title = panel.Title,
                Content = panel.PanelView
            };
            var toolDock = new ToolDock()
            {
                Id = $"{panel.ID}-dock",
                Alignment = panelSetting.Dock,
                Proportion = panelSetting.Length,
                VisibleDockables = DockControl.Factory.CreateList<IDockable>(tool),
                ActiveDockable = tool
            };

            if(string.IsNullOrEmpty(panelSetting.Parent))
            {
                var proportionalDock = new ProportionalDock()
                {
                    Orientation = Orientation.Horizontal,
                    VisibleDockables = DockControl.Factory.CreateList<IDockable>(toolDock)
                };
                toolDock.Owner = proportionalDock;
                proportionalDock.Owner = DockMain;
                DockMain.Add(proportionalDock);
            }
            else
            {
                var parentDock = DockControl.Factory.FindDockable(DockRoot, d => d.Id == $"{panelSetting.Parent}-dock") as ToolDock;
                if(parentDock != null )
                {
                    if (panelSetting.Dock == Alignment.Unset)
                    {
                        parentDock.VisibleDockables.Add(tool);
                    }
                    else
                    {
                        var ownerDock = parentDock.Owner as ProportionalDock;
                        if (ownerDock == null) return;
                        if (((panelSetting.Dock == Alignment.Left || panelSetting.Dock == Alignment.Right) && ownerDock.Orientation == Orientation.Horizontal) ||
                            ((panelSetting.Dock == Alignment.Top || panelSetting.Dock == Alignment.Bottom) && ownerDock.Orientation == Orientation.Vertical))
                        {
                            if (panelSetting.Dock == Alignment.Right || panelSetting.Dock == Alignment.Bottom)
                            {
                                ownerDock.VisibleDockables.Add(new ProportionalDockSplitter());
                                ownerDock.VisibleDockables.Add(toolDock);
                            }
                            else
                            {
                                ownerDock.VisibleDockables.Insert(0, new ProportionalDockSplitter());
                                ownerDock.VisibleDockables.Insert(0, toolDock);
                            }
                            toolDock.Owner = ownerDock;
                        }
                        else
                        {
                            var proportionalDock = new ProportionalDock()
                            {
                                Orientation = (panelSetting.Dock == Alignment.Left || panelSetting.Dock == Alignment.Right) ? Orientation.Horizontal : Orientation.Vertical
                            };
                            if (panelSetting.Dock == Alignment.Right || panelSetting.Dock == Alignment.Bottom)
                            {
                                proportionalDock.VisibleDockables = DockControl.Factory.CreateList<IDockable>(
                                    ownerDock,
                                    new ProportionalDockSplitter(),
                                    toolDock);
                            }
                            else
                            {
                                proportionalDock.VisibleDockables = DockControl.Factory.CreateList<IDockable>(
                                    toolDock,
                                    new ProportionalDockSplitter(),
                                    ownerDock);
                            }

                            toolDock.Owner = proportionalDock;
                            proportionalDock.Owner = ownerDock.Owner;
                            ownerDock.Owner = proportionalDock;

                            for(int i = 0; i < (proportionalDock.Owner as DockBase).VisibleDockables.Count; i++)
                            {
                                if((proportionalDock.Owner as DockBase).VisibleDockables[i] == ownerDock)
                                {
                                    (proportionalDock.Owner as DockBase).VisibleDockables[i] = proportionalDock;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
