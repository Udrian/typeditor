using System.Collections;
using System.Reflection;
using Avalonia;
using TypeD.Models.Data;
using TypeD.View.TreeNodes;
using TypeDCore.Models.Data.PropertyDefinitions;
using TypeDitor.View.Panels;

namespace TypeDitorTest;

public class ComponentBrowserTests
{
    [Theory]
    [InlineData("A,B", "B")]
    [InlineData("A,B", "B,A")]
    [InlineData("A,B,C", "C,A")]
    [InlineData("A", "B,A")]
    [InlineData("A,B", "")]
    [InlineData("A", "A,B")]
    public void Rebuild_MatchesRequestedKeysWithoutDuplicates(string original, string updated)
    {
        var viewModelType = typeof(ComponentBrowserPanel).Assembly.GetType(
            "TypeDitor.ViewModel.Panels.ComponentBrowserViewModel", throwOnError: true);
        var nodeType = viewModelType.GetNestedType("Node", BindingFlags.Public);
        var listType = typeof(List<>).MakeGenericType(nodeType);

        IList CreateNodes(string keys)
        {
            var nodes = (IList)Activator.CreateInstance(listType);
            var tree = new Tree();
            foreach (var key in keys.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                tree.AddNode(key, key, key);
                var node = Activator.CreateInstance(nodeType);
                nodeType.GetProperty("Context").SetValue(node, tree.Get(key));
                nodeType.GetProperty("Nodes").SetValue(node, Activator.CreateInstance(
                    typeof(System.Collections.ObjectModel.ObservableCollection<>).MakeGenericType(nodeType)));
                nodes.Add(node);
            }
            return nodes;
        }

        var nodes = CreateNodes(original);
        var expected = CreateNodes(updated);
        var rebuild = viewModelType.GetMethod("Buildtree", BindingFlags.NonPublic | BindingFlags.Static);

        rebuild.Invoke(null, new object[] { expected, nodes });
        rebuild.Invoke(null, new object[] { expected, nodes });

        Assert.Equal(updated.Split(',', StringSplitOptions.RemoveEmptyEntries),
            nodes.Cast<object>().Select(node => (string)nodeType.GetProperty("Key").GetValue(node)));
        for (var index = 0; index < expected.Count; index++)
            Assert.Same(expected[index], nodes[index]);
    }

    [Fact]
    public void PropertyTemplates_LoadFromEditorAssembly()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        IPropertyDefinition[] definitions =
        [
            new BooleanPropertyDefinition(),
            new StringPropertyDefinition(),
            new Int32PropertyDefinition(),
            new DoublePropertyDefinition(),
            new EnumPropertyDefinition(),
            new ColorPropertyDefinition(),
            new Vec2PropertyDefinition(),
            new ContentPropertyDefinition(null)
        ];

        foreach (var definition in definitions)
            Assert.NotNull(definition.CreateTemplate());
    }
}