using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public sealed class NodeCatalogTests
{
    private static readonly string[] MvpNodeIds =
    [
        "event_start",
        "action_move_to",
        "action_harvest",
        "action_load",
        "action_unload",
        "flow_branch",
        "action_wait",
        "data_const_number",
        "data_compare",
        "sensor_cargo",
    ];

    public static TheoryData<string, ExpectedPort[], ExpectedPort[]> Month2PortTable => new()
    {
        {
            "event_start",
            [],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "action_move_to",
            [new ExpectedPort("in", PortKind.Exec, DataType.None), new ExpectedPort("target", PortKind.Data, DataType.Vector, true)],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "action_harvest",
            [new ExpectedPort("in", PortKind.Exec, DataType.None), new ExpectedPort("target", PortKind.Data, DataType.Vector, true)],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "action_load",
            [new ExpectedPort("in", PortKind.Exec, DataType.None)],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "action_unload",
            [new ExpectedPort("in", PortKind.Exec, DataType.None)],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "flow_branch",
            [new ExpectedPort("in", PortKind.Exec, DataType.None), new ExpectedPort("condition", PortKind.Data, DataType.Bool, true)],
            [new ExpectedPort("true", PortKind.Exec, DataType.None), new ExpectedPort("false", PortKind.Exec, DataType.None)]
        },
        {
            "action_wait",
            [new ExpectedPort("in", PortKind.Exec, DataType.None), new ExpectedPort("ticks", PortKind.Data, DataType.Number, true)],
            [new ExpectedPort("out", PortKind.Exec, DataType.None)]
        },
        {
            "data_const_number",
            [],
            [new ExpectedPort("value", PortKind.Data, DataType.Number)]
        },
        {
            "data_compare",
            [new ExpectedPort("a", PortKind.Data, DataType.Number, true), new ExpectedPort("b", PortKind.Data, DataType.Number, true)],
            [new ExpectedPort("result", PortKind.Data, DataType.Bool)]
        },
        {
            "sensor_cargo",
            [],
            [new ExpectedPort("count", PortKind.Data, DataType.Number)]
        },
    };

    [Fact]
    public void Catalog_ContainsExactlyMvpNodes()
    {
        Assert.Equal(MvpNodeIds.Order(), NodeCatalog.All.Keys.Where(id => id != "test_passthrough").Order());
        Assert.Equal("Test Passthrough (test-only)", NodeCatalog.All["test_passthrough"].DisplayName);
    }

    [Theory]
    [MemberData(nameof(Month2PortTable))]
    public void Catalog_PortsMatchMonth2Table(
        string typeId,
        ExpectedPort[] expectedInputs,
        ExpectedPort[] expectedOutputs)
    {
        var node = NodeCatalog.All[typeId];

        Assert.Equal(typeId, node.TypeId);
        Assert.NotEmpty(node.DisplayName);
        AssertPorts(expectedInputs, node.Inputs);
        AssertPorts(expectedOutputs, node.Outputs);
    }

    [Fact]
    public void EventNodes_HaveNoExecInput()
    {
        var eventNode = NodeCatalog.All["event_start"];

        Assert.True(eventNode.IsEvent);
        Assert.DoesNotContain(eventNode.Inputs, p => p.Kind == PortKind.Exec);
    }

    [Fact]
    public void NonEventNodes_AreNotMarkedAsEvents()
    {
        foreach (var typeId in MvpNodeIds.Where(id => id != "event_start"))
        {
            Assert.False(NodeCatalog.All[typeId].IsEvent);
        }
    }

    [Fact]
    public void Branch_HasRequiredBoolConditionAndTrueFalseExecOutputs()
    {
        var branch = NodeCatalog.All["flow_branch"];

        Assert.Contains(branch.Inputs, p => p is { Name: "condition", Kind: PortKind.Data, Type: DataType.Bool, Required: true });
        Assert.Contains(branch.Outputs, p => p is { Name: "true", Kind: PortKind.Exec, Type: DataType.None });
        Assert.Contains(branch.Outputs, p => p is { Name: "false", Kind: PortKind.Exec, Type: DataType.None });
    }

    private static void AssertPorts(ExpectedPort[] expected, IReadOnlyList<PortDef> actual)
    {
        Assert.Equal(expected.Length, actual.Count);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].Kind, actual[i].Kind);
            Assert.Equal(expected[i].Type, actual[i].Type);

            if (expected[i].Required is { } required)
                Assert.Equal(required, actual[i].Required);
        }
    }

    public sealed record ExpectedPort(string Name, PortKind Kind, DataType Type, bool? Required = null);
}
