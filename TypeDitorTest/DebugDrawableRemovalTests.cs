using System.Reflection;
using TypeD.Models.Data;
using TypeDBasic2d.Components;
using TypeDBasic2d.Typedeaf.Core.Entities.Drawables;
using TypeDCore.Models.Data.Hooks;
using TypeOEngine.Typedeaf.Core;
using TypeOEngine.Typedeaf.Core.Engine;
using TypeOEngine.Typedeaf.Core.Common;
using TypeOEngine.Typedeaf.Core.Engine.Hardwares;
using TypeOEngine.Typedeaf.Desktop.Engine.Hardwares.Interfaces;
using TypeOEngine.Typedeaf.Desktop.Engine.Services;
using TypeOEngine.Typedeaf.TypeOBasic2d.Entities;

namespace TypeDitorTest;

public class DebugDrawableRemovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeferredRemovalDestroysOutlineAfterComponentOrAncestorIsDetached(bool nested)
    {
        var typeO = Assert.IsType<TypeO>(TypeO.Create<TestGame>("debug-outline-removal"));
        typeO.AddHardware<IMouseHardware, TestMouseHardware>();
        typeO.Context.AddService<MouseInputService>();
        typeO.Start();
        var game = (TestGame)typeO.Context.Game;
        var initializerType = typeof(Basic2dComponent).Assembly.GetType("TypeDBasic2d.TypeDBasic2dInitializer", true);
        var initializer = Activator.CreateInstance(initializerType);
        var map = new Dictionary<string, Debug2DDrawable>();
        var mapProperty = initializerType.GetProperty("DebugDrawablesMap", BindingFlags.Instance | BindingFlags.NonPublic);
        var focusedProperty = initializerType.GetProperty("CurrentFocusedDebug2DDrawable", BindingFlags.Instance | BindingFlags.NonPublic);
        mapProperty.SetValue(initializer, map);
        var add = initializerType.GetMethod("AddDebugDrawableComponent", BindingFlags.Instance | BindingFlags.NonPublic);
        var remove = initializerType.GetMethod("RemoveDebugDrawableComponent", BindingFlags.Instance | BindingFlags.NonPublic);

        var root = Node("root");
        var parent = Node("parent", root);
        var component = nested ? Node("child", parent) : parent;
        var runtime = game.Scene.Entities.Create<TestEntity>("original");
        var originalPath = component.FullParentPath + component.ID;
        add.Invoke(initializer, new object[]
        {
            new TypeOObjectAddedToViewHook { Context = typeO.Context, Component = component, TypeOObject = runtime }
        });
        var overlay = Assert.Single(game.Scene.Drawables.Get<Debug2DDrawable>());
        focusedProperty.SetValue(initializer, overlay);
        Assert.Same(overlay, map[originalPath]);

        typeof(Component).GetProperty(nameof(Component.ParentComponent)).SetValue(parent, null);
        Assert.NotEqual(originalPath, component.FullParentPath + component.ID);
        var removal = new TypeOObjectRemovedFromViewHook
        {
            Context = typeO.Context, Component = component, TypeOObject = runtime
        };
        remove.Invoke(initializer, new object[] { removal });

        Assert.Empty(map);
        Assert.Empty(game.Scene.Drawables.Get<Debug2DDrawable>());
        Assert.False(overlay.Initialized);
        Assert.Null(focusedProperty.GetValue(initializer));

        // A delayed notification for the old object must not remove a replacement's outline.
        typeof(Component).GetProperty(nameof(Component.ParentComponent)).SetValue(parent, root);
        var replacement = game.Scene.Entities.Create<TestEntity>("replacement");
        add.Invoke(initializer, new object[]
        {
            new TypeOObjectAddedToViewHook { Context = typeO.Context, Component = component, TypeOObject = replacement }
        });
        var replacementOverlay = Assert.Single(game.Scene.Drawables.Get<Debug2DDrawable>());
        remove.Invoke(initializer, new object[] { removal });
        Assert.Same(replacementOverlay, Assert.Single(map).Value);
        Assert.True(replacementOverlay.Initialized);
        Assert.Same(replacementOverlay, Assert.Single(game.Scene.Drawables.Get<Debug2DDrawable>()));
        remove.Invoke(initializer, new object[]
        {
            new TypeOObjectRemovedFromViewHook { Context = typeO.Context, Component = component, TypeOObject = replacement }
        });
        Assert.Empty(map);
        Assert.Empty(game.Scene.Drawables.Get<Debug2DDrawable>());
    }

    private static Component Node(string id, Component parent = null)
    {
        var component = new Component
        {
            Properties = new List<Property> { new() { Name = "ID", Type = typeof(string), Value = id } }
        };
        typeof(Component).GetProperty(nameof(Component.ParentComponent)).SetValue(component, parent);
        return component;
    }

    public class TestEntity : Entity2d { }

    public class TestMouseHardware : Hardware, IMouseHardware
    {
        public Vec2 CurrentMousePosition { get; set; }
        public Vec2 OldMousePosition { get; set; }
        public Vec2 CurrentWheelPosition { get; set; }
        public Vec2 OldWheelPosition { get; set; }
        public bool CurrentButtonDownEvent(object key) => false;
        public bool CurrentButtonUpEvent(object key) => true;
        public bool OldButtonDownEvent(object key) => false;
        public bool OldButtonUpEvent(object key) => true;
        public void SetState(object state) { }
        protected override void Initialize() { }
        protected override void Cleanup() { }
    }

    public class TestGame : Game
    {
        public TestScene Scene { get; private set; }
        protected override void Initialize()
        {
            base.Initialize();
            Scene = Scenes.SetScene<TestScene>();
        }
        protected override void Cleanup() { }
        public override void Update(double dt) => Exit();
        public override void Draw() { }
    }

    public class TestScene : Scene
    {
        protected override void Initialize() => base.Initialize();
        protected override void Cleanup() { }
        public override void Update(double dt) { }
        public override void Draw() { }
        public override void OnEnter(Scene from) { }
        public override void OnExit(Scene to) { }
    }
}
