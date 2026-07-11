using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public sealed class CompilerValidationTests
{
    [Fact]
    public void Compile_ReportsUnknownNodeTypeAtNode()
    {
        var result = CircuitCompiler.Compile(Graph([Node(1, "not_a_node")]));

        AssertError(result, 1, "unknown_node_type");
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsMissingEventNode()
    {
        var result = CircuitCompiler.Compile(Graph([Node(1, "action_load")]));

        Assert.Contains(result.Errors, error => error.Code == "no_event_node");
    }

    [Fact]
    public void Compile_ReportsNumberToBoolMismatchAtDestinationNode()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number"), Node(3, "flow_branch")],
            [
                Connection(1, "out", 3, "in"),
                Connection(2, "value", 3, "condition"),
            ]));

        AssertError(result, 3, "type_mismatch");
    }

    [Fact]
    public void Compile_TypeMismatchedDataConnectionDoesNotAlsoReportRequiredInputMissing()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number"), Node(3, "flow_branch")],
            [
                Connection(1, "out", 3, "in"),
                Connection(2, "value", 3, "condition"),
            ]));

        AssertError(result, 3, "type_mismatch");
        Assert.DoesNotContain(result.Errors, error => error.NodeId == 3 && error.Code == "required_input_missing");
    }

    [Fact]
    public void Compile_ReportsExecInputConnectedFromData()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number"), Node(3, "action_wait")],
            [
                Connection(1, "out", 3, "in"),
                Connection(2, "value", 3, "in"),
            ]));

        AssertError(result, 3, "exec_input_from_data");
    }

    [Fact]
    public void Compile_ReportsInvalidConnectionWithMissingDestinationNodeAtExistingSource()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start")],
            [Connection(1, "out", 99, "in")]));

        AssertError(result, 1, "invalid_connection");
        Assert.Single(result.Errors.Where(error => error.Code == "invalid_connection"));
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsMissingSourceNodeAtExistingDestination()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(2, "action_load")],
            [Connection(99, "out", 2, "in")]));

        AssertError(result, 2, "invalid_connection");
        Assert.Single(result.Errors.Where(error => error.Code == "invalid_connection"));
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsBothMissingNodesAtTheirMissingEndpointIds()
    {
        var result = CircuitCompiler.Compile(Graph(
            [],
            [Connection(99, "out", 100, "in")]));

        AssertError(result, 99, "invalid_connection");
        AssertError(result, 100, "invalid_connection");
        Assert.Equal(2, result.Errors.Count(error => error.Code == "invalid_connection"));
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_UnknownTypeSourceConnectionDoesNotReportInvalidConnection()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "unknown_type"), Node(2, "action_load")],
            [Connection(1, "out", 2, "in")]));

        AssertError(result, 1, "unknown_node_type");
        Assert.DoesNotContain(result.Errors, error => error.Code == "invalid_connection");
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_UnknownTypeDestinationConnectionDoesNotReportInvalidConnection()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "unknown_type")],
            [Connection(1, "out", 2, "in")]));

        AssertError(result, 2, "unknown_node_type");
        Assert.DoesNotContain(result.Errors, error => error.Code == "invalid_connection");
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsInvalidConnectionWithMissingSourcePortAtSource()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_load")],
            [Connection(1, "not_a_port", 2, "in")]));

        AssertError(result, 1, "invalid_connection");
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsInvalidConnectionWithMissingDestinationPortAtDestination()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_load")],
            [Connection(1, "out", 2, "not_a_port")]));

        AssertError(result, 2, "invalid_connection");
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsEachMalformedEndpointOnOneConnection()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_load")],
            [Connection(1, "not_a_source_port", 2, "not_a_destination_port")]));

        AssertError(result, 1, "invalid_connection");
        AssertError(result, 2, "invalid_connection");
        Assert.Equal(2, result.Errors.Count(error => error.Code == "invalid_connection"));
        Assert.False(result.Success);
        Assert.Null(result.Circuit);
    }

    [Fact]
    public void Compile_ReportsMultipleExecOutputs()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_load"), Node(3, "action_unload")],
            [
                Connection(1, "out", 2, "in"),
                Connection(1, "out", 3, "in"),
            ]));

        AssertError(result, 1, "multi_exec_out");
    }

    [Fact]
    public void Compile_ReportsMultipleDataInputs()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number"), Node(3, "data_const_number"), Node(4, "action_wait")],
            [
                Connection(1, "out", 4, "in"),
                Connection(2, "value", 4, "ticks"),
                Connection(3, "value", 4, "ticks"),
            ]));

        AssertError(result, 4, "multi_data_in");
    }

    [Fact]
    public void Compile_ReportsRequiredDataInputWithoutConnectionOrInlineParameter()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_wait")],
            [Connection(1, "out", 2, "in")]));

        AssertError(result, 2, "required_input_missing");
    }

    [Fact]
    public void Compile_ReportsEveryNodeRemainingInDataCycle()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "test_passthrough"), Node(3, "test_passthrough"), Node(4, "action_wait")],
            [
                Connection(1, "out", 4, "in"),
                Connection(2, "value", 3, "value"),
                Connection(3, "value", 2, "value"),
                Connection(2, "value", 4, "ticks"),
            ]));

        AssertError(result, 2, "data_cycle");
        AssertError(result, 3, "data_cycle");
    }

    [Fact]
    public void Compile_ReportsNodeUnreachableFromEvents()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "action_load"), Node(3, "action_unload")],
            [Connection(1, "out", 2, "in")]));

        AssertError(result, 3, "unreachable_node");
    }

    [Fact]
    public void Compile_ValidEventWaitGraphWithNumberSupplier_ReturnsCircuit()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number", ("value", 10)), Node(3, "action_wait")],
            [
                Connection(1, "out", 3, "in"),
                Connection(2, "value", 3, "ticks"),
            ]));

        Assert.True(result.Success);
        Assert.NotNull(result.Circuit);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Compile_AllowsTransitiveDataSuppliersForReachableBranchButRejectsUnusedSupplier()
    {
        var result = CircuitCompiler.Compile(Graph(
            [
                Node(1, "event_start"),
                Node(2, "sensor_cargo"),
                Node(3, "data_const_number", ("value", 5)),
                Node(4, "data_compare"),
                Node(5, "data_const_number", ("value", 99)),
                Node(6, "flow_branch"),
            ],
            [
                Connection(1, "out", 6, "in"),
                Connection(2, "count", 4, "a"),
                Connection(3, "value", 4, "b"),
                Connection(4, "result", 6, "condition"),
            ]));

        Assert.DoesNotContain(result.Errors, error => error.NodeId == 2 && error.Code == "unreachable_node");
        Assert.DoesNotContain(result.Errors, error => error.NodeId == 3 && error.Code == "unreachable_node");
        Assert.DoesNotContain(result.Errors, error => error.NodeId == 4 && error.Code == "unreachable_node");
        AssertError(result, 5, "unreachable_node");
    }

    private static CircuitGraphData Graph(CircuitNodeData[] nodes, CircuitConnectionData[]? connections = null)
    {
        var graph = new CircuitGraphData { Nodes = [.. nodes] };
        if (connections is not null)
            graph.Connections.AddRange(connections);
        return graph;
    }

    private static CircuitNodeData Node(int nodeId, string typeId, params (string Name, double Value)[] parameters)
    {
        var node = new CircuitNodeData { NodeId = nodeId, TypeId = typeId };
        foreach (var parameter in parameters)
            node.InlineParams.Add(parameter.Name, parameter.Value);
        return node;
    }

    private static CircuitConnectionData Connection(int fromNode, string fromPort, int toNode, string toPort)
    {
        return new CircuitConnectionData
        {
            FromNode = fromNode,
            FromPort = fromPort,
            ToNode = toNode,
            ToPort = toPort,
        };
    }

    private static void AssertError(CompileResult result, int nodeId, string code)
    {
        Assert.Contains(result.Errors, error => error.NodeId == nodeId && error.Code == code);
    }
}
