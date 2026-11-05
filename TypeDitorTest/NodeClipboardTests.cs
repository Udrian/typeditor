using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TypeD;
using TypeD.Components;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SaveContexts;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.DTO;
using TypeD.Models.Interfaces;
using TypeD.Models.Providers.Interfaces;
using TypeDCore.Commands.Data;
using TypeDCore.Components;
using TypeDCore.Models.Data.Hooks;
using TypeDCore.Models.Interfaces;
using TypeOEngine.Typedeaf.Core.Common;
using TypeOEngine.Typedeaf.Core.Entities;

namespace TypeDitorTest;

public class NodeClipboardTests
{
    [Fact]
    public void SnapshotPreservesNestedPropertiesFlagsOrderAndIndependentValues()
    {
        using var fixture = new Fixture();
        var source = fixture.Source;
        var inherited = Assert.Single(source.Children);
        inherited.Public = true;
        inherited.Removed = true;
        inherited.Name = "Edited inherited child";
        var extra = fixture.Provider.CreateInstance(fixture.Project, fixture.Detail);
        extra.ID = null;
        fixture.Components.AddExisting(fixture.Project, source, extra, 0);
        extra.Name = "Added child";
        source.Public = true;
        source.Name = "Copied name";
        var values = (List<int>)source.Properties.Single(p => p.Name == "Values").Value;
        values.Add(7);

        var snapshot = fixture.Provider.CaptureNode(source);
        values.Add(9);
        inherited.Name = "Changed after copy";
        var copy = fixture.Provider.RestoreNode(fixture.Project, snapshot);

        Assert.NotEqual(source.ID, copy.ID);
        Assert.Null(copy.ParentComponent);
        Assert.True(copy.Public);
        Assert.Equal("Copied name", copy.Name);
        Assert.Equal(new[] { extra.ID, inherited.ID }, copy.Children.Select(child => child.ID));
        Assert.All(copy.Children, child => Assert.Same(copy, child.ParentComponent));
        Assert.Equal("Added child", copy.Children[0].Name);
        Assert.True(copy.Children[1].Public);
        Assert.True(copy.Children[1].Removed);
        Assert.Equal("Edited inherited child", copy.Children[1].Name);
        Assert.Same(copy.DefinitionChildren.Single(), copy.Children[1]);
        var copiedValues = Assert.IsType<List<int>>(copy.Properties.Single(p => p.Name == "Values").Value);
        Assert.Equal(new[] { 1, 7 }, copiedValues);
        copiedValues.Add(11);
        Assert.Equal(new[] { 1, 7, 9 }, values);
    }

    [Fact]
    public async Task DuplicateInsertsSiblingAfterSourceWithoutChangingClipboardAndPersistsWithUndoRedo()
    {
        using var fixture = new Fixture();
        fixture.Text = "Unrelated clipboard text";
        var tail = fixture.Provider.CreateInstance(fixture.Project, fixture.Definition);
        tail.ID = null;
        fixture.Components.AddExisting(fixture.Project, fixture.Root, tail);
        fixture.Source.Name = "Original";

        Assert.True(fixture.Clipboard.CanDuplicate(fixture.Context(fixture.Source)));
        fixture.Clipboard.Duplicate(fixture.Context(fixture.Source, new Vec2(999, 999)));

        var copy = fixture.Root.Children[1];
        var id = copy.ID;
        Assert.Equal(new[] { fixture.Source, copy, tail }, fixture.Root.Children);
        Assert.Equal("Original", copy.Name);
        Assert.Equal(new Vec2(10, 20), copy.Properties.Single(p => p.Name == "Position").Value);
        Assert.Equal("Unrelated clipboard text", fixture.Text);
        Assert.Same(copy, fixture.Selection.SelectedNode);
        Assert.True(fixture.History.Undo());
        Assert.Equal(new[] { fixture.Source, tail }, fixture.Root.Children);
        Assert.Null(copy.ParentComponent);
        Assert.True(fixture.History.Redo());
        Assert.Equal(new[] { fixture.Source, copy, tail }, fixture.Root.Children);
        Assert.Equal(id, copy.ID);

        await fixture.Save.SaveAction(fixture.Project);
        var saved = JsonNode.Parse(File.ReadAllText(fixture.Provider.GetPath(fixture.Project, fixture.Root)));
        Assert.Equal(new[] { fixture.Source.ID, id, tail.ID },
            saved["Children"].AsArray().Select(node => node["ID"].GetValue<string>()));
    }

    [Fact]
    public async Task SystemClipboardSnapshotCanBeReadByAnotherModelAndPastedRepeatedly()
    {
        using var fixture = new Fixture();
        fixture.Source.Name = "At copy time";
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source));
        Assert.StartsWith("TypeO.NodeClipboard:1\n", fixture.Text);
        fixture.Source.Name = "Edited later";
        var otherClipboard = fixture.CreateClipboard();
        await otherClipboard.RefreshAsync();
        Assert.True(otherClipboard.CanPaste(fixture.Context(fixture.Root)));

        await otherClipboard.PasteAsync(fixture.Context(fixture.Root));
        await otherClipboard.PasteAsync(fixture.Context(fixture.Root));

        var first = fixture.Root.Children[1];
        var second = fixture.Root.Children[2];
        Assert.Equal("At copy time", first.Name);
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(3, fixture.Root.Children.Select(child => child.ID).Distinct().Count());
        Assert.NotSame(first.Properties.Single(p => p.Name == "Values").Value,
            second.Properties.Single(p => p.Name == "Values").Value);
        Assert.Same(fixture.Root, first.ParentComponent);
        Assert.True(fixture.History.Undo());
        Assert.DoesNotContain(second, fixture.Root.Children);
        Assert.True(fixture.History.Redo());
        Assert.Same(second, fixture.Root.Children.Last());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ViewerPasteUsesWorldPositionRelativeToParent(bool hasHitTest)
    {
        using var fixture = new Fixture();
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source.Children[0]));
        if (hasHitTest)
            fixture.Hooks.AddHook<ViewerHitTestHook>(hook =>
            {
                Assert.Same(fixture.Source, hook.Target);
                hook.TargetFound = true;
                hook.OriginX = 100;
                hook.OriginY = 200;
            });

        await fixture.Clipboard.PasteAsync(fixture.Context(fixture.Source, new Vec2(300, 400)));

        var pasted = fixture.Source.Children.Last();
        Assert.Equal(hasHitTest ? new Vec2(200, 200) : new Vec2(290, 380),
            pasted.Properties.Single(p => p.Name == "Position").Value);
        Assert.Equal(new Vec2(10, 20), fixture.Detail.Properties.Single(p => p.Name == "Position").Value);
    }

    [Fact]
    public async Task EmptyOrUnrelatedClipboardDisablesPasteAndRefreshDoesNotUseStaleSnapshot()
    {
        using var fixture = new Fixture();
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source));
        Assert.True(fixture.Clipboard.CanPaste(fixture.Context(fixture.Root)));
        foreach (var text in new[] { null, "", "ordinary text", "{}" })
        {
            fixture.Text = text;
            await fixture.Clipboard.RefreshAsync();
            Assert.False(fixture.Clipboard.CanPaste(fixture.Context(fixture.Root)));
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("""{"FormatVersions":{"TypeD":2},"Node":{}}""")]
    [InlineData("""{"FormatVersions":{"TypeD":1},"Node":{}}""")]
    public async Task InvalidOrUnsupportedClipboardIsRejectedAndClearsAvailability(string json)
    {
        using var fixture = new Fixture();
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source));
        fixture.Text = "TypeO.NodeClipboard:1\n" + json;
        var error = await Record.ExceptionAsync(() => fixture.Clipboard.RefreshAsync());
        Assert.True(error is JsonException or InvalidDataException);
        Assert.False(fixture.Clipboard.CanPaste(fixture.Context(fixture.Root)));
    }

    [Fact]
    public async Task PasteRejectsReadOnlyRemovedRecursiveAndMissingTypeTargets()
    {
        using var fixture = new Fixture();
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source));
        Assert.False(fixture.Clipboard.CanPaste(fixture.Context(fixture.Source)));
        fixture.Root.ReadOnly = true;
        Assert.False(fixture.Clipboard.CanPaste(fixture.Context(fixture.Root)));
        Assert.False(fixture.Clipboard.CanDuplicate(fixture.Context(fixture.Source)));
        fixture.Root.ReadOnly = false;
        fixture.Source.Removed = true;
        Assert.False(fixture.Clipboard.CanCopy(fixture.Context(fixture.Source)));
        fixture.Source.Removed = false;
        fixture.Save.Components.Remove(fixture.Definition);
        Assert.False(fixture.Clipboard.CanPaste(fixture.Context(fixture.Root)));
    }

    [Fact]
    public async Task ClosingComponentDuringClipboardReadPreventsInsertion()
    {
        using var fixture = new Fixture();
        await fixture.Clipboard.CopyAsync(fixture.Context(fixture.Source));
        var read = new TaskCompletionSource<string>();
        fixture.SetReader(fixture.Clipboard, () => read.Task);
        var paste = fixture.Clipboard.PasteAsync(fixture.Context(fixture.Root));
        fixture.Hooks.Shoot(new CloseComponentHook { Project = fixture.Project, Component = fixture.Root });
        read.SetResult(fixture.Text);

        await Assert.ThrowsAsync<InvalidOperationException>(() => paste);
        Assert.Single(fixture.Root.Children);
    }

    [Fact]
    public void CommandsRegisterExpectedShortcutsExecuteThroughRegistryAndUnregister()
    {
        using var fixture = new Fixture();
        var commands = fixture.RegisterCommands();
        var registry = fixture.Registry;
        try
        {
            Assert.Equal(new[] { "node.duplicate", "node.copy", "node.paste" }, registry.Commands.Select(c => c.Id));
            Assert.Equal(new[] { "Ctrl+D", "Ctrl+C", "Ctrl+V" }, registry.Commands.Select(c => c.DefaultShortcut));
            Assert.True(registry.TryExecuteShortcut("Ctrl+C", "global", fixture.Context(fixture.Source)));
            Assert.StartsWith("TypeO.NodeClipboard:1\n", fixture.Text);
            Assert.True(registry.TryExecuteShortcut("Ctrl+D", "global", fixture.Context(fixture.Source)));
            Assert.Equal(2, fixture.Root.Children.Count);
            Assert.True(registry.TryExecuteShortcut("Ctrl+V", "global", fixture.Context(fixture.Root)));
            Assert.Equal(3, fixture.Root.Children.Count);
        }
        finally
        {
            foreach (var command in commands) command.Dispose();
        }
        Assert.Empty(registry.Commands);
    }

    [Fact]
    public void PaletteCapturesSelectionWithoutRequiringTreeFocusAndUnsubscribesOnDispose()
    {
        using var fixture = new Fixture();
        var commands = fixture.RegisterCommands();
        try
        {
            fixture.Hooks.Shoot(new ComponentFocusHook { Project = fixture.Project, Component = fixture.Source, Root = false });
            Assert.False(fixture.Registry.TryExecuteShortcut("Ctrl+C", "global"));
            var context = fixture.Hooks.Shoot(new CommandPaletteContextHook());
            Assert.All(fixture.Registry.Commands, command => Assert.True(command.CanExecute(context.Parameters[command.Id])));
            fixture.Hooks.Shoot(new ComponentFocusHook { Project = fixture.Project, Component = fixture.Root, Root = false });

            Assert.True(fixture.Registry.Execute("node.copy", context.Parameters["node.copy"]));
            Assert.Contains(fixture.Source.FullName, fixture.Text);
            Assert.True(fixture.Registry.Execute("node.duplicate", context.Parameters["node.duplicate"]));
            Assert.Equal(2, fixture.Root.Children.Count);
            Assert.Equal(fixture.Source.FullName, fixture.Root.Children[1].FullName);
            fixture.Hooks.Shoot(new ComponentFocusHook { Project = fixture.Project, Component = fixture.Root, Root = false });
            Assert.True(fixture.Registry.Execute("node.paste", fixture.Hooks.Shoot(new CommandPaletteContextHook()).Parameters["node.paste"]));
            Assert.Equal(3, fixture.Root.Children.Count);
        }
        finally
        {
            foreach (var command in commands) command.Dispose();
        }
        Assert.Empty(fixture.Hooks.Shoot(new CommandPaletteContextHook()).Parameters);
    }

    [Fact]
    public void ShortcutContextUsesFocusedViewerAndWorldPointerButNotStaleTreeActivity()
    {
        using var fixture = new Fixture();
        var commands = fixture.RegisterCommands();
        try
        {
            fixture.Selection.SetComponentExplorerActive();
            Assert.False(fixture.Registry.TryExecuteShortcut("Ctrl+D", "global"));
            fixture.Hooks.Shoot(new ComponentFocusHook { Project = fixture.Project, Component = fixture.Source, Root = false });
            fixture.Hooks.AddHook<ViewerNodeContextHook>(hook =>
            {
                hook.Focused = true;
                hook.Position = new Vec2(400, 500);
            });
            Assert.True(fixture.Registry.TryExecuteShortcut("Ctrl+C", "global"));
            Assert.True(fixture.Registry.TryExecuteShortcut("Ctrl+V", "global"));
            Assert.Equal(2, fixture.Root.Children.Count);
            Assert.Equal(new Vec2(400, 500), fixture.Root.Children.Last().Properties.Single(p => p.Name == "Position").Value);
        }
        finally
        {
            foreach (var command in commands) command.Dispose();
        }
    }

    [Fact]
    public void ViewerPasteTargetsHoveredNodeRatherThanSelection()
    {
        using var fixture = new Fixture();
        var commands = fixture.RegisterCommands();
        try
        {
            Assert.True(fixture.Registry.Execute("node.copy", fixture.Context(fixture.Source.Children[0])));
            fixture.Hooks.AddHook<ViewerNodeContextHook>(hook =>
            {
                hook.Focused = true;
                hook.Position = new Vec2(400, 500);
            });
            fixture.Hooks.AddHook<ViewerHitTestHook>(hook =>
            {
                if (hook.Target != null)
                    return;
                Assert.Equal(400, hook.X);
                Assert.Equal(500, hook.Y);
                hook.Result = fixture.Source;
            });

            Assert.Same(fixture.Root, fixture.Selection.SelectedNode);
            Assert.True(fixture.Registry.TryExecuteShortcut("Ctrl+V", "global"));

            Assert.Single(fixture.Root.Children);
            Assert.Equal(2, fixture.Source.Children.Count);
            Assert.Equal(new Vec2(390, 480), fixture.Source.Children.Last().Properties.Single(p => p.Name == "Position").Value);
        }
        finally
        {
            foreach (var command in commands) command.Dispose();
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Resources Resources = new();
        private readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "TypeONodeTests-" + Guid.NewGuid());
        public Project Project { get; }
        public IComponentProvider Provider { get; }
        public IComponentModel Components { get; }
        public ComponentSaveContext Save { get; } = new();
        public IHookModel Hooks { get; }
        public INodeSelectionModel Selection { get; }
        public ICommandHistoryModel History { get; }
        public INodeClipboardModel Clipboard { get; }
        public Component Root { get; }
        public Component Definition { get; }
        public Component Detail { get; }
        public Component Source { get; }
        public string Text { get; set; }
        public IEditorCommandRegistry Registry => Resources.Get<IEditorCommandRegistry>();

        public Fixture()
        {
            Resources.Add(Stub.Create<IProjectModel>((_, _) => null));
            Resources.Add(Stub.Create<ISaveModel>((method, _) =>
                method.Name == "GetSaveContext" ? Save : null));
            Resources.Add(Stub.Create<IPropertyModel>((_, _) => null));
            Resources.Add(Stub.Create<ISettingModel>((method, _) =>
                method.Name == "GetContext" ? new MainWindowSettingContext() : null));
            TypeDInit.Init(Resources);
            var constructor = typeof(Project).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            Project = (Project)constructor.Invoke(new[]
            {
                DirectoryPath,
                JsonSerializer.Deserialize("""{"ProjectName":"Game","CSProjName":"Game"}""",
                    constructor.GetParameters()[1].ParameterType)
            });
            Provider = Resources.Get<IComponentProvider>();
            Components = Resources.Get<IComponentModel>();
            Hooks = Resources.Get<IHookModel>();
            Provider.Init(Resources);
            Components.Init(Resources);
            Save.Init(Resources, Project);
            Selection = CreateModel<INodeSelectionModel>("NodeSelectionModel");
            History = CreateModel<ICommandHistoryModel>("CommandHistoryModel");
            Clipboard = CreateClipboard();

            var entityBase = MakeComponent(nameof(Entity), typeof(Entity).Namespace, new EntityComponentTemplate());
            Detail = MakeComponent("Detail", "Game", new EntityComponentTemplate(), entityBase);
            Definition = MakeComponent("Item", "Game", new EntityComponentTemplate(), entityBase);
            Root = MakeComponent("MainScene", "Game", new SceneComponentTemplate());
            Root.Properties.RemoveAll(property => property.Name == "Position");
            Save.Components.AddRange(new[] { entityBase, Detail, Definition, Root });
            var inherited = Provider.CreateInstance(Project, Detail);
            Components.AddExisting(Project, Definition, inherited);
            Definition.DefinitionChildren.Add(inherited);
            Source = Provider.CreateInstance(Project, Definition);
            Components.AddExisting(Project, Root, Source);
            Hooks.Shoot(new ComponentFocusHook { Project = Project, Component = Root });
        }

        public NodeClipboardCommandData Context(Component node, Vec2? worldPosition = null) =>
            new() { Project = Project, Node = node, WorldPosition = worldPosition };

        public IReadOnlyList<IDisposable> RegisterCommands()
        {
            Registry.Init(Resources);
            return new[] { "DuplicateNodeCommand", "CopyNodeCommand", "PasteNodeCommand" }
                .Select(name => (IDisposable)Activator.CreateInstance(
                    typeof(CoreComponent).Assembly.GetType("TypeDCore.Commands." + name, true), Resources))
                .ToList();
        }

        public INodeClipboardModel CreateClipboard()
        {
            var model = CreateModel<INodeClipboardModel>("NodeClipboardModel");
            SetReader(model, () => Task.FromResult(Text));
            model.GetType().GetProperty("WriteText", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, new Func<string, Task>(text => { Text = text; return Task.CompletedTask; }));
            return model;
        }

        public void SetReader(INodeClipboardModel model, Func<Task<string>> read) =>
            model.GetType().GetProperty("ReadText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(model, read);

        private T CreateModel<T>(string name) where T : class, IModel
        {
            var model = (T)Activator.CreateInstance(typeof(CoreComponent).Assembly.GetType("TypeDCore.Models." + name, true));
            Resources.Add(model);
            model.Init(Resources);
            return model;
        }

        private static Component MakeComponent(string name, string ns, ComponentTemplate template, Component baseComponent = null)
        {
            var component = new Component
            {
                ClassName = name,
                Namespace = ns,
                Template = template,
                BaseInheritedComponent = baseComponent,
                Properties = new List<Property>
                {
                    new() { Name = "ID", Type = typeof(string), Value = Guid.NewGuid().ToString() },
                    new() { Name = "Name", Type = typeof(string), Value = name },
                    new() { Name = "Position", Type = typeof(Vec2), Value = new Vec2(10, 20) },
                    new() { Name = "Values", Type = typeof(List<int>), Value = new List<int> { 1 } }
                }
            };
            component.OveriddenProperties = component.Properties.Select(p => new Property(p)).ToList();
            typeof(ComponentTemplate).GetProperty("Component").SetValue(template, component);
            template.Init();
            return component;
        }

        public void Dispose()
        {
            ((IDisposable)Selection).Dispose();
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    public class Stub : DispatchProxy
    {
        private Func<MethodInfo, object[], object> Handler;
        public static T Create<T>(Func<MethodInfo, object[], object> handler) where T : class
        {
            var instance = Create<T, Stub>();
            ((Stub)(object)instance).Handler = handler;
            return instance;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args) => Handler(targetMethod, args);
    }

    private sealed class Resources : IResourceModel
    {
        private readonly List<object> Values = new();
        private readonly Dictionary<string, object> Keyed = new();
        public void Init(IResourceModel resources) { }
        public void Add(object value) => Values.Add(value);
        public void Add(List<object> values) => Values.AddRange(values);
        public void Add(string key, object value) => Keyed.Add(key, value);
        public void Add(List<Tuple<string, object>> values)
        {
            foreach (var (key, value) in values) Add(key, value);
        }
        public T Get<T>() where T : class => Values.OfType<T>().FirstOrDefault();
        public T Get<T>(string key) where T : class => Keyed.GetValueOrDefault(key) as T;
        public void Remove(string key) => Keyed.Remove(key);
    }
}
