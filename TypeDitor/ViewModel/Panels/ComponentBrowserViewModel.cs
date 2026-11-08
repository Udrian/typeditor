using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TypeD.Models;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Panels
{
    internal class ComponentBrowserViewModel : ViewModelBase
    {
        public class Node
        {
            public TypeD.Models.Data.Project Project { get; set; }
            public Bitmap Icon
            {
                get
                {
                    if (Context.Item is Component component)
                        return IconCache.Get(Project, component.ResolvedIcon);
                    return NamespaceIcon.Value;
                }
            }

            private static readonly Lazy<Bitmap> NamespaceIcon = new Lazy<Bitmap>(() =>
            {
                try
                {
                    return new Bitmap(AssetLoader.Open(new Uri("avares://TypeDitor/Icons/namespace.png")));
                }
                catch (Exception)
                {
                    return null;
                }
            });
            public string Name { get { return Context.Name; } }
            public string Key { get { return Context.Key; } }
            public string Component { 
                get {
                    var component = Context.Item as Component;
                    return component == null ? Context.Item.ToString() : component.RootBaseComponent.ClassName; 
                } 
            }
            public TypeD.View.TreeNodes.Node Context { get; set; }
            public ObservableCollection<Node> Nodes { get; set; }
        }

        // Models
        IHookModel HookModel { get; set; }
        IEditorCommandRegistry EditorCommandRegistry { get; set; }
        INodeSelectionModel NodeSelectionModel { get; set; }

        // View
        TreeView TreeView { get; set; }

        // Data
        public Project LoadedProject { get; private set; }
        ObservableCollection<Node> Nodes { get; set; }

        // Commands

        // Constructors
        public ComponentBrowserViewModel(Control element, Project loadedProject, TreeView treeView) : base(element)
        {
            HookModel = ResourceModel.Get<IHookModel>();
            EditorCommandRegistry = ResourceModel.Get<IEditorCommandRegistry>();
            NodeSelectionModel = ResourceModel.Get<INodeSelectionModel>();
            LoadedProject = loadedProject;
            TreeView = treeView;

            HookModel.AddHook<ComponentTreeBuiltHook>(BuildTree);
            Nodes = TreeToNodeList(LoadedProject.ComponentTree.Nodes);
            TreeView.ItemsSource = Nodes;

        }

        // Functions
        private ObservableCollection<Node> TreeToNodeList(IList<TypeD.View.TreeNodes.Node> treeNodes)
        {
            var nodes = new ObservableCollection<Node>();

            foreach(var treeNode in treeNodes)
            {
                nodes.Add(new Node()
                {
                    Project = LoadedProject,
                    Context = treeNode,
                    Nodes = TreeToNodeList(treeNode.Nodes)
                });
            }

            return nodes;
        }

        public void DoubleClickItem(Node node)
        {
            var component = node.Context.Item as Component;
            if (component == null) return;
            EditorCommandRegistry.Execute("component.open", new OpenComponentCommandData() { Project = LoadedProject, Component = component });
        }

        public void ContextMenuOpened(ContextMenu contextMenu, Node node)
        {
            NodeSelectionModel.SetComponentBrowserActive(LoadedProject, node?.Context.Item as Component);

            var componentTypeBrowserContextMenuOpenedHook = new ComponentTypeBrowserContextMenuOpenedHook(node?.Context);
            HookModel.Shoot(componentTypeBrowserContextMenuOpenedHook);

            contextMenu.Items.Clear();
            foreach (var menu in componentTypeBrowserContextMenuOpenedHook.Menu.Items)
            {
                ViewHelper.InitMenu(contextMenu, menu, this);
            }
        }

        public void SelectionChanged(Node node)
        {
            NodeSelectionModel.SetComponentBrowserActive(LoadedProject, node?.Context.Item as Component);
        }

        public void BrowserFocused()
        {
            NodeSelectionModel.SetComponentBrowserActive(
                LoadedProject,
                (TreeView.SelectedItem as Node)?.Context.Item as Component);
        }

        public bool TryExecuteShortcut(string shortcut)
        {
            return EditorCommandRegistry.TryExecuteShortcut(shortcut, "global");
        }

        private void BuildTree(ComponentTreeBuiltHook hook)
        {
            if (hook.Tree != LoadedProject.ComponentTree)
                return;

            Dispatcher.UIThread.Invoke(() =>
            {
                var treeNodes = TreeToNodeList(LoadedProject.ComponentTree.Nodes);
                Buildtree(treeNodes, Nodes);
            });
        }

        private static void Buildtree(IList<Node> treeNodes, IList<Node> nodes)
        {
            for (var i = 0; i < treeNodes.Count; i++)
            {
                var treeNode = treeNodes[i];
                var existingIndex = -1;
                for (var j = i; j < nodes.Count; j++)
                {
                    if (nodes[j].Key == treeNode.Key)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex == i)
                {
                    nodes[i] = treeNode;
                }
                else
                {
                    if (existingIndex >= 0)
                        nodes.RemoveAt(existingIndex);
                    nodes.Insert(i, treeNode);
                }
            }

            while (nodes.Count > treeNodes.Count)
                nodes.RemoveAt(nodes.Count - 1);
        }
    }
}
