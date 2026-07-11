using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public sealed class CompilerCodegenTests
{
    [Fact]
    public void Compile_StartToWait_EmitsWaitAndLoopsToStartEntry()
    {
        var result = CircuitCompiler.Compile(Graph(
            [Node(1, "event_start"), Node(2, "data_const_number", ("value", 10)), Node(3, "action_wait")],
            [
                Connection(1, "out", 3, "in"),
                Connection(2, "value", 3, "ticks"),
            ]));

        var circuit = Assert.IsType<CompiledCircuit>(result.Circuit);

        Assert.NotEqual(-1, circuit.StartEntry);
        Assert.Equal(new Instruction(OpCode.Jump, circuit.StartEntry, 0, 0, 0), circuit.Instructions[^1]);

        var loadConst = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.LoadConst));
        var wait = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.Wait));
        Assert.Equal(10, loadConst.Imm);
        Assert.Equal(loadConst.A, wait.A);
        Assert.Equal(0, wait.B);
        Assert.Equal(0, wait.C);
    }

    [Fact]
    public void Compile_BranchWithCargoComparison_EmitsControlFlowAndDataDependencies()
    {
        var circuit = CompileBranchGraph();

        var jumpIfFalse = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.JumpIfFalse));
        var compare = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.Compare));
        var sensor = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.ReadSensor));

        Assert.Equal(0, sensor.B);
        Assert.Equal(compare.A, jumpIfFalse.A);
        Assert.InRange(jumpIfFalse.B, 0, circuit.Instructions.Length - 1);
        Assert.Equal(2, circuit.Instructions.Count(instruction => instruction.Op == OpCode.Wait));
        Assert.Equal(OpCode.Load, circuit.Instructions[^2].Op);
    }

    [Fact]
    public void Compile_SameBranchGraph_ProducesEqualInstructions()
    {
        var first = CompileBranchGraph();
        var second = CompileBranchGraph();

        Assert.Equal(first.StartEntry, second.StartEntry);
        Assert.Equal(first.RegisterCount, second.RegisterCount);
        Assert.Equal(first.Instructions, second.Instructions);
    }

    [Fact]
    public void Compile_BranchComparisonIsEmittedBeforeJumpIfFalse()
    {
        var circuit = CompileBranchGraph();

        var compareIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.Compare);
        var branchIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.JumpIfFalse);

        Assert.True(compareIndex >= 0);
        Assert.True(branchIndex >= 0);
        Assert.True(compareIndex < branchIndex);
    }

    [Fact]
    public void Compile_BranchWithoutConvergence_JumpsPastFalseArmAfterTrueArm()
    {
        var result = CircuitCompiler.Compile(Graph(
            [
                Node(1, "event_start"),
                Node(2, "sensor_cargo"),
                Node(3, "data_const_number", ("value", 5)),
                Node(4, "data_compare"),
                Node(5, "flow_branch"),
                Node(6, "data_const_number", ("value", 10)),
                Node(7, "action_wait"),
                Node(8, "action_wait"),
            ],
            [
                Connection(1, "out", 5, "in"),
                Connection(2, "count", 4, "a"),
                Connection(3, "value", 4, "b"),
                Connection(4, "result", 5, "condition"),
                Connection(5, "true", 7, "in"),
                Connection(5, "false", 8, "in"),
                Connection(6, "value", 7, "ticks"),
                Connection(6, "value", 8, "ticks"),
            ]));

        var circuit = Assert.IsType<CompiledCircuit>(result.Circuit);
        var waitIndexes = circuit.Instructions
            .Select((instruction, index) => (instruction, index))
            .Where(pair => pair.instruction.Op == OpCode.Wait)
            .Select(pair => pair.index)
            .ToArray();

        Assert.Equal(2, waitIndexes.Length);
        var jump = circuit.Instructions[waitIndexes[0] + 1];
        Assert.Equal(OpCode.Jump, jump.Op);
        Assert.True(jump.A > waitIndexes[1]);
    }

    [Fact]
    public void Compile_MoveToWithInlineTargetComponents_EmitsComponentRegisters()
    {
        var result = CircuitCompiler.Compile(Graph(
            [
                Node(1, "event_start"),
                Node(2, "action_move_to", ("target_x", 1), ("target_y", 2), ("target_z", 3)),
            ],
            [Connection(1, "out", 2, "in")]));

        var circuit = Assert.IsType<CompiledCircuit>(result.Circuit);
        var moveTo = Assert.Single(circuit.Instructions.Where(instruction => instruction.Op == OpCode.MoveTo));
        var constants = circuit.Instructions
            .Where(instruction => instruction.Op == OpCode.LoadConst)
            .ToDictionary(instruction => instruction.A, instruction => instruction.Imm);

        Assert.Equal(1, constants[moveTo.A]);
        Assert.Equal(2, constants[moveTo.B]);
        Assert.Equal(3, constants[moveTo.C]);
    }

    [Fact]
    public void Compile_BranchConvergenceUsesEarliestCommonControlFlowNode()
    {
        var result = CircuitCompiler.Compile(Graph(
            [
                Node(1, "event_start"),
                Node(2, "sensor_cargo"),
                Node(3, "data_const_number", ("value", 5)),
                Node(4, "data_compare"),
                Node(5, "flow_branch"),
                Node(6, "action_unload"),
                Node(10, "action_load"),
                Node(11, "data_const_number", ("value", 10)),
                Node(12, "action_wait"),
            ],
            [
                Connection(1, "out", 5, "in"),
                Connection(2, "count", 4, "a"),
                Connection(3, "value", 4, "b"),
                Connection(4, "result", 5, "condition"),
                Connection(5, "true", 10, "in"),
                Connection(5, "false", 12, "in"),
                Connection(11, "value", 12, "ticks"),
                Connection(12, "out", 10, "in"),
                Connection(10, "out", 6, "in"),
            ]));

        var circuit = Assert.IsType<CompiledCircuit>(result.Circuit);
        var jumpIfFalseIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.JumpIfFalse);
        var loadIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.Load);
        var waitIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.Wait);
        var unloadIndex = Array.FindIndex(circuit.Instructions, instruction => instruction.Op == OpCode.Unload);

        Assert.Equal(1, circuit.Instructions.Count(instruction => instruction.Op == OpCode.Load));
        Assert.Equal(1, circuit.Instructions.Count(instruction => instruction.Op == OpCode.Unload));
        Assert.Equal(OpCode.Jump, circuit.Instructions[jumpIfFalseIndex + 1].Op);
        Assert.Equal(loadIndex, circuit.Instructions[jumpIfFalseIndex + 1].A);
        Assert.True(circuit.Instructions[jumpIfFalseIndex].B < waitIndex);
        Assert.True(waitIndex < loadIndex);
        Assert.True(loadIndex < unloadIndex);
    }

    private static CompiledCircuit CompileBranchGraph()
    {
        var result = CircuitCompiler.Compile(Graph(
            [
                Node(1, "event_start"),
                Node(2, "sensor_cargo"),
                Node(3, "data_const_number", ("value", 5)),
                Node(4, "data_compare"),
                Node(5, "flow_branch"),
                Node(6, "data_const_number", ("value", 10)),
                Node(7, "action_wait"),
                Node(8, "action_wait"),
                Node(9, "action_load"),
            ],
            [
                Connection(1, "out", 5, "in"),
                Connection(2, "count", 4, "a"),
                Connection(3, "value", 4, "b"),
                Connection(4, "result", 5, "condition"),
                Connection(5, "true", 7, "in"),
                Connection(5, "false", 8, "in"),
                Connection(6, "value", 7, "ticks"),
                Connection(6, "value", 8, "ticks"),
                Connection(7, "out", 9, "in"),
                Connection(8, "out", 9, "in"),
            ]));

        Assert.True(result.Success, string.Join(", ", result.Errors.Select(error => $"{error.NodeId}:{error.Code}")));
        return Assert.IsType<CompiledCircuit>(result.Circuit);
    }

    private static CircuitGraphData Graph(CircuitNodeData[] nodes, CircuitConnectionData[] connections)
    {
        return new CircuitGraphData { Nodes = [.. nodes], Connections = [.. connections] };
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
}
