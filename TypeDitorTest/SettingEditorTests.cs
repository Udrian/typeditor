using Avalonia;
using System.Reflection;
using System.Text.Json;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.View;
using TypeD.ViewModel;
using TypeDCore.Models.Data.PropertyDefinitions;
using TypeDCore.Models.Data;
using TypeDCore.Models.Data.SettingContexts;
using TypeDitor.ViewModel.Dialogs.Tools;
using TypeOEngine.Typedeaf.Core.Common;

namespace TypeDitorTest;

public class SettingEditorTests
{
    static SettingEditorTests()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
    }

    [Fact]
    public void SettingsUseRegisteredEditorsAndPassTypedValues()
    {
        Verify(new BooleanPropertyDefinition(), false, true, typeof(BooleanPropertyNode));
        Verify(new StringPropertyDefinition(), "Before", "After", typeof(StringPropertyNode));
        Verify(new Int32PropertyDefinition(), 1, 42, typeof(Int32PropertyNode));
        Verify(new DoublePropertyDefinition(), 1.5, 2.5, typeof(DoublePropertyNode));
        Verify(new EnumPropertyDefinition(), DayOfWeek.Monday, DayOfWeek.Friday, typeof(EnumPropertyNode));
        Verify(new ColorPropertyDefinition(), new Color(255, 0, 0, 0), new Color(255, 1, 2, 3), typeof(ColorPropertyNode));
        Verify(new Vec2PropertyDefinition(), new Vec2(1, 2), new Vec2(3, 4), typeof(Vec2PropertyNode));
    }

    [Fact]
    public void DisabledMenuDefinitionsProduceDisabledAvaloniaItems()
    {
        var menu = new Avalonia.Controls.ContextMenu();
        TypeD.Helpers.ViewHelper.InitMenu(menu,
            new MenuItem("Paste", _ => { }) { IsEnabled = false }, null);
        Assert.False(Assert.IsType<Avalonia.Controls.MenuItem>(Assert.Single(menu.Items)).IsEnabled);
    }

    [Theory]
    [InlineData("node.copy")]
    [InlineData("node.paste")]
    [InlineData("node.duplicate")]
    public void PaletteFiltersAndExecutesWithCapturedCommandParameters(string id)
    {
        var target = new object();
        var constructor = typeof(EditorCommand).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var command = (EditorCommand)constructor.Invoke(new object[]
        {
            id, id, "Node command", "global", "Ctrl+C",
            (Action<object>)(_ => { }), (Predicate<object>)(parameter => ReferenceEquals(parameter, target)),
            (Func<object>)(() => null), null
        });
        object executedTarget = null;
        var registry = NodeClipboardTests.Stub.Create<IEditorCommandRegistry>((method, args) =>
        {
            if (method.Name == "get_Commands")
                return new[] { command };
            if (method.Name == "Execute")
            {
                Assert.Equal(id, args[0]);
                executedTarget = args[1];
                return true;
            }
            throw new InvalidOperationException(method.Name);
        });
        var hooks = NodeClipboardTests.Stub.Create<IHookModel>((method, args) =>
        {
            Assert.Equal("Shoot", method.Name);
            var context = Assert.IsType<CommandPaletteContextHook>(args[0]);
            context.Parameters[id] = target;
            return context;
        });
        var palette = new TypeDitor.View.CommandPaletteWindow(registry, hooks);
        var refresh = palette.GetType().GetMethod("RefreshCommands", BindingFlags.Instance | BindingFlags.NonPublic);
        refresh.Invoke(palette, null);
        Assert.Same(command, Assert.Single(palette.FilteredCommands));

        palette.GetType().GetMethod("ExecuteCommand", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(palette, new object[] { command });

        Assert.Same(target, executedTarget);
    }

    [Fact]
    public void ViewerClearColorUsesColorEditorAndRevertsToOriginalBackground()
    {
        var context = new ViewerSettingContext();
        var option = Create(context.ClearColor, new ColorPropertyDefinition());
        var editor = Assert.IsType<ColorPropertyNode>(option.Editor);
        Assert.Equal(new Color(28, 3, 51), context.ClearColor.Value);
        Assert.True(context.SaveOnExit);

        editor.AvaloniaColor = Avalonia.Media.Color.FromArgb(128, 12, 34, 56);

        Assert.Equal(new Color(128, 12, 34, 56), context.ClearColor.Value);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ColorJsonConverter());
        var json = JsonSerializer.Serialize(context, options);
        var restored = JsonSerializer.Deserialize<ViewerSettingContext>(json, options);
        Assert.Equal(context.ClearColor.Value, restored.ClearColor.Value);
        Assert.Equal(typeof(Color), restored.ClearColor.GetValueType());

        option.Revert();

        Assert.Equal(new Color(28, 3, 51), context.ClearColor.Value);
        Assert.Equal(Avalonia.Media.Color.FromRgb(28, 3, 51), editor.AvaloniaColor);
        Assert.False(option.CanRevert);
    }

    [Fact]
    public void OptionSavesProjectDefaultRevertsToItAndRestoresBuiltInBehaviorOnRemoval()
    {
        using var fixture = new SettingsFixture();
        var context = fixture.Settings.GetContext<OptionTestContext>();
        var option = new OptionsDialogViewModel.Option(new SettingItem("Amount", "Amount", context.Amount, value =>
        {
            context.Amount.SetValue(value);
            fixture.Settings.SetContext(context);
        }), new Definitions(new Int32PropertyDefinition()), fixture.Settings, fixture.Project);
        option.Editor.Value = 42;
        CompleteOnUIThread(option.SetProjectDefaultAsync());
        Assert.True(option.HasProjectDefault);
        Assert.False(option.CanRevert);
        option.Editor.Value = 99;
        option.Revert();
        Assert.Equal(42, option.Editor.Value);
        Assert.False(fixture.Settings.HasUserOverride(context.Amount));
        CompleteOnUIThread(option.SetProjectDefaultAsync(remove: true));
        Assert.False(option.HasProjectDefault);
        Assert.Equal(5, option.Editor.Value);
        Assert.Equal(5, fixture.Settings.GetContext<OptionTestContext>().Amount.Value);
        Assert.False(option.CanRevert);
    }

    [Fact]
    public void RemovingProjectDefaultDoesNotDiscardUserValueAndShortcutRevertUsesProjectBinding()
    {
        using var fixture = new SettingsFixture();
        var context = fixture.Settings.GetContext<OptionTestContext>();
        CompleteOnUIThread(fixture.Settings.SetProjectDefaultAsync(fixture.Project, context.Amount, 42));
        context = fixture.Settings.GetContext<OptionTestContext>();
        var option = new OptionsDialogViewModel.Option(new SettingItem("Amount", "", context.Amount, value =>
        {
            context.Amount.SetValue(value);
            fixture.Settings.SetContext(context);
        }), new Definitions(new Int32PropertyDefinition()), fixture.Settings, fixture.Project);
        option.Editor.Value = 99;
        CompleteOnUIThread(option.SetProjectDefaultAsync(remove: true));
        Assert.Equal(99, option.Editor.Value);
        option.Revert();
        Assert.Equal(5, option.Editor.Value);

        var constructor = typeof(EditorCommand).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var command = (EditorCommand)constructor.Invoke(new object[]
        {
            "test.command", "Command", "", "global", "Ctrl+C", (Action<object>)(_ => { }), null, null, null
        });
        var shortcut = new OptionsDialogViewModel.Option(command, (target, value) =>
        {
            target.Shortcut = value;
            context.Shortcuts.Value[target.Id] = value;
            fixture.Settings.SetContext(context);
        }, fixture.Settings, fixture.Project, context.Shortcuts);
        shortcut.UpdateShortcut("Ctrl+J");
        CompleteOnUIThread(shortcut.SetProjectDefaultAsync());
        shortcut.UpdateShortcut("Ctrl+K");
        shortcut.Revert();
        Assert.Equal("Ctrl+J", command.Shortcut);
        Assert.False(fixture.Settings.HasUserOverride(context.Shortcuts, command.Id));
        CompleteOnUIThread(shortcut.SetProjectDefaultAsync(remove: true));
        Assert.Equal("Ctrl+C", command.Shortcut);
        Assert.False(shortcut.HasProjectDefault);
        Assert.False(shortcut.CanRevert);
    }

    [Fact]
    public void FilterMatchesTitlesDescriptionsCategoriesAndShortcutsWithinSelectedCategory()
    {
        using var fixture = new SettingsFixture();
        fixture.Hooks.AddHook<OptionsHook>(hook =>
        {
            hook.Items.Add(new SettingItem("Background", "Canvas color", new Setting<string>("purple"), _ => { }, "Viewer"));
            hook.Items.Add(new SettingItem("External Editor", "Executable path", new Setting<string>("code"), _ => { }, "General"));
        });
        var constructor = typeof(EditorCommand).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var command = (EditorCommand)constructor.Invoke(new object[]
        {
            "node.copy", "Copy node", "Copy a node", "global", "Ctrl+C", (Action<object>)(_ => { }), null, null, null
        });
        var registry = NodeClipboardTests.Stub.Create<IEditorCommandRegistry>((method, _) =>
            method.Name == "get_Commands" ? new[] { command } :
                throw new InvalidOperationException(method.Name));
        fixture.ReplaceRegistry(registry);
        var viewModel = new OptionsDialogViewModel(null, fixture.Project);
        viewModel.InitUI();
        Assert.Equal(3, viewModel.DisplayedCategories.Count);
        viewModel.FilterText = "VIEWER canvas";
        Assert.Equal("Background", Assert.Single(Assert.Single(viewModel.DisplayedCategories).VisibleOptions).Title);
        viewModel.SelectedCategory = viewModel.Categories.Single(category => category.Name == "General");
        Assert.True(viewModel.NoResults);
        viewModel.ShowAllCategories();
        Assert.False(viewModel.NoResults);
        viewModel.FilterText = "executable";
        Assert.Equal("External Editor", Assert.Single(Assert.Single(viewModel.DisplayedCategories).VisibleOptions).Title);
        viewModel.FilterText = "no matching setting";
        Assert.True(viewModel.NoResults);
        viewModel.FilterText = "ctrl+c";
        Assert.Same(command, Assert.Single(Assert.Single(viewModel.DisplayedCategories).VisibleOptions).Command);
        viewModel.FilterText = "node.copy";
        Assert.Same(command, Assert.Single(Assert.Single(viewModel.DisplayedCategories).VisibleOptions).Command);
        viewModel.FilterText = "";
        Assert.Equal(3, viewModel.DisplayedCategories.Count);
    }

    [Fact]
    public void OptionsDialogLoadsFilterLayoutAndDisablesProjectActionsWithoutProject()
    {
        using var fixture = new SettingsFixture();
        var dialog = new TypeDitor.View.Dialogs.Tools.OptionsDialog(fixture.Project);
        Assert.IsType<OptionsDialogViewModel>(dialog.DataContext);
        Assert.Equal(1040, dialog.Width);
        var context = fixture.Settings.GetContext<OptionTestContext>();
        var option = new OptionsDialogViewModel.Option(new SettingItem("Amount", "", context.Amount, _ => { }),
            new Definitions(new Int32PropertyDefinition()), fixture.Settings);
        Assert.False(option.CanSetProjectDefault);
        Assert.False(option.HasProjectDefault);
        dialog.Close();
    }

    [Fact]
    public void NullValuesKeepTheirDeclaredTypeAndCanBeReverted()
    {
        ISetting setting = new Setting<string>(null);
        Assert.Equal(typeof(string), setting.GetValueType());
        var option = Create(setting, new StringPropertyDefinition());

        Assert.Equal(typeof(string), option.Editor.Type);
        Assert.Equal(string.Empty, option.Editor.Value);
        Assert.Null(option.Editor.Property.Value);
        option.Editor.Value = "Edited";
        Assert.True(option.CanRevert);
        option.Revert();
        Assert.Equal(string.Empty, option.Editor.Value);
        Assert.Null(option.Editor.Property.Value);
        Assert.Null(setting.GetValue());
        Assert.False(option.CanRevert);
    }

    [Fact]
    public void NumericEditorConvertsTextBeforeCallingTheSettingCallback()
    {
        var setting = new Setting<int>(10);
        var option = Create(setting, new Int32PropertyDefinition());

        option.Editor.Value = "25";
        Assert.Equal(25, setting.Value);
        Assert.IsType<int>(option.Editor.Property.Value);
    }

    [Fact]
    public void EditorReadsBackTheValueAcceptedByTheCallback()
    {
        var setting = new Setting<int>(1);
        var option = new OptionsDialogViewModel.Option(
            new SettingItem("Limited", "Limited value", setting,
                value => setting.Value = Math.Clamp((int)value, 0, 10)),
            new Definitions(new Int32PropertyDefinition()));

        option.Editor.Value = 50;

        Assert.Equal(10, setting.Value);
        Assert.Equal(10, option.Editor.Value);
    }

    [Fact]
    public void RevertRefreshesCompositeEditorBindings()
    {
        var setting = new Setting<Vec2>(new Vec2(1, 2));
        var option = Create(setting, new Vec2PropertyDefinition());
        var editor = Assert.IsType<Vec2PropertyNode>(option.Editor);
        editor.ValueX = 12;
        editor.ValueY = 34;
        var notifications = new List<string>();
        editor.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        option.Revert();

        Assert.Equal(1, editor.ValueX);
        Assert.Equal(2, editor.ValueY);
        Assert.Contains(nameof(editor.ValueX), notifications);
        Assert.Contains(nameof(editor.ValueY), notifications);
        Assert.False(option.CanRevert);
    }

    [Fact]
    public void ComponentlessEditorsRespectPropertyReadOnly()
    {
        var node = new PropertyNode(null, new Property(), (_, _) => { });

        Assert.False(node.ReadOnly);
        node.Property.ReadOnly = true;
        Assert.True(node.ReadOnly);
        var componentNode = new PropertyNode(new Component { ReadOnly = true }, new Property(), (_, _) => { });
        Assert.True(componentNode.ReadOnly);
    }

    [Fact]
    public void ShortcutsKeepCaptureClearRevertAndConflictRejection()
    {
        var constructor = typeof(EditorCommand).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var command = (EditorCommand)constructor.Invoke(new object[]
        {
            "test", "Test", "Test shortcut", "global", "Ctrl+S",
            (Action<object>)(_ => { }), null, null, null
        });
        var option = new OptionsDialogViewModel.Option(command, (target, shortcut) =>
        {
            if (shortcut != "Ctrl+X")
                target.Shortcut = shortcut;
        });
        Assert.True(option.IsShortcut);
        Assert.Null(option.Editor);
        Assert.Null(option.EditorTemplate);
        Assert.False(option.CanRevert);

        option.UpdateShortcut("Ctrl+Shift+S");
        Assert.Equal("Ctrl+Shift+S", option.Value);
        Assert.True(option.CanRevert);
        option.UpdateShortcut("Ctrl+X");
        Assert.Equal("Ctrl+Shift+S", option.Value);
        option.UpdateShortcut(string.Empty);
        Assert.Equal(string.Empty, option.Value);
        option.Revert();
        Assert.Equal("Ctrl+S", option.Value);
        Assert.False(option.CanRevert);
    }

    private static void Verify<T>(IPropertyDefinition definition, T initial, T edited, Type nodeType)
    {
        var setting = new Setting<T>(initial);
        var option = Create(setting, definition);
        Assert.False(option.IsShortcut);
        Assert.Equal(typeof(T), option.Editor.Type);
        Assert.Equal(nodeType, option.Editor.GetType());
        Assert.NotNull(option.EditorTemplate);
        Assert.False(option.Editor.ReadOnly);
        Assert.False(option.CanRevert);
        var notifications = new List<string>();
        option.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        option.Editor.Value = edited;

        Assert.Equal(edited, setting.Value);
        Assert.Equal(edited, option.Editor.Property.Value);
        Assert.True(option.CanRevert);
        Assert.Contains(nameof(option.CanRevert), notifications);

        option.Revert();

        Assert.Equal(initial, setting.Value);
        Assert.Equal(initial, option.Editor.Property.Value);
        Assert.False(option.CanRevert);
    }

    private static OptionsDialogViewModel.Option Create(ISetting setting, IPropertyDefinition definition)
    {
        return new OptionsDialogViewModel.Option(
            new SettingItem("Test setting", "Description", setting, setting.SetValue),
            new Definitions(definition));
    }

    private static void CompleteOnUIThread(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
        Assert.True(task.IsCompleted, "Project-default operation did not complete.");
        task.GetAwaiter().GetResult();
    }

    private sealed class Definitions(IPropertyDefinition definition) : IPropertyModel
    {
        public void Init(IResourceModel resourceModel) { }
        public void Register(IPropertyDefinition value) => throw new NotSupportedException();
        public void Unregister(Type type) => throw new NotSupportedException();
        public IReadOnlyList<IPropertyDefinition> List() => new[] { definition };
        public IPropertyDefinition Get(Type type) => definition.Type.IsAssignableFrom(type) ? definition : null;
    }

    [Name("OptionsProjectDefaultTests-82a4c5")]
    public class OptionTestContext : SettingContext
    {
        public Setting<int> Amount { get; set; } = new(5);
        public Setting<ShortcutBindingOverrides> Shortcuts { get; set; } = new(new());
        public OptionTestContext() { SaveOnExit = true; }
    }

    private sealed class SettingsFixture : IDisposable
    {
        private readonly List<object> Models = new();
        private readonly string Folder = Path.Combine(Path.GetTempPath(), "OptionsDefaultTest-" + Guid.NewGuid());
        public ISettingModel Settings { get; }
        public TypeD.Models.Data.Project Project { get; }
        public IHookModel Hooks { get; }

        public SettingsFixture()
        {
            Models.Add(new Definitions(new StringPropertyDefinition()));
            var resources = NodeClipboardTests.Stub.Create<IResourceModel>((method, args) =>
            {
                if (method.Name == "Add")
                {
                    if (args[0] is List<object> values) Models.AddRange(values);
                    else Models.Add(args[0]);
                    return null;
                }
                if (method.Name == "Get")
                    return Models.FirstOrDefault(value => method.GetGenericArguments()[0].IsInstanceOfType(value));
                throw new InvalidOperationException(method.Name);
            });
            TypeD.TypeDInit.Init(resources);
            Settings = resources.Get<ISettingModel>();
            Settings.Init(resources);
            Hooks = resources.Get<IHookModel>();
            var constructor = typeof(TypeD.Models.Data.Project).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            Project = (TypeD.Models.Data.Project)constructor.Invoke(new[]
            {
                Folder, JsonSerializer.Deserialize($$"""{"ProjectName":"OptionsTests-{{Guid.NewGuid():N}}"}""",
                    constructor.GetParameters()[1].ParameterType)
            });
            Settings.InitContext<OptionTestContext>(Project);
        }

        public void ReplaceRegistry(IEditorCommandRegistry registry) => Models.Insert(0, registry);
        public void Dispose()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }
    }
}
