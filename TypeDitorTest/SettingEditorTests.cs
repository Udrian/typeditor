using Avalonia;
using System.Reflection;
using System.Text.Json;
using TypeD.Models.Data;
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

    private sealed class Definitions(IPropertyDefinition definition) : IPropertyModel
    {
        public void Init(IResourceModel resourceModel) { }
        public void Register(IPropertyDefinition value) => throw new NotSupportedException();
        public void Unregister(Type type) => throw new NotSupportedException();
        public IReadOnlyList<IPropertyDefinition> List() => new[] { definition };
        public IPropertyDefinition Get(Type type) => definition.Type.IsAssignableFrom(type) ? definition : null;
    }
}
