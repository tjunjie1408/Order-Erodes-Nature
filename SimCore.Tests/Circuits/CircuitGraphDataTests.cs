using System.Text.Json;
using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public sealed class CircuitGraphDataTests
{
    [Fact]
    public void JsonRoundTrip_PreservesNodesConnectionsInlineParamsAndEditorPosition()
    {
        var graph = new CircuitGraphData
        {
            Nodes =
            {
                new CircuitNodeData
                {
                    NodeId = 1,
                    TypeId = "event_start",
                    EditorX = 12.5f,
                    EditorY = -3.25f,
                },
                new CircuitNodeData
                {
                    NodeId = 2,
                    TypeId = "action_wait",
                    InlineParams = { ["ticks"] = 10 },
                    EditorX = 128,
                    EditorY = 64,
                },
            },
            Connections =
            {
                new CircuitConnectionData
                {
                    FromNode = 1,
                    FromPort = "out",
                    ToNode = 2,
                    ToPort = "in",
                },
            },
        };

        var json = JsonSerializer.Serialize(graph);
        var restored = JsonSerializer.Deserialize<CircuitGraphData>(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Nodes.Count);
        Assert.Single(restored.Connections);

        Assert.Equal("event_start", restored.Nodes[0].TypeId);
        Assert.Equal(12.5f, restored.Nodes[0].EditorX);
        Assert.Equal(-3.25f, restored.Nodes[0].EditorY);

        Assert.Equal("action_wait", restored.Nodes[1].TypeId);
        Assert.Equal(10, restored.Nodes[1].InlineParams["ticks"]);

        var connection = restored.Connections[0];
        Assert.Equal(1, connection.FromNode);
        Assert.Equal("out", connection.FromPort);
        Assert.Equal(2, connection.ToNode);
        Assert.Equal("in", connection.ToPort);
    }
}
