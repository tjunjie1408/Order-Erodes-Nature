namespace SimCore.Circuits;

public static class CircuitCompiler
{
    public static CompileResult Compile(CircuitGraphData graph)
    {
        var errors = new List<CompileError>();
        var nodes = new Dictionary<int, NodeInfo>();
        var nodeIds = new HashSet<int>();

        foreach (var node in graph.Nodes)
        {
            nodeIds.Add(node.NodeId);

            if (!NodeCatalog.All.TryGetValue(node.TypeId, out var definition))
            {
                errors.Add(new CompileError(node.NodeId, "unknown_node_type", $"Unknown node type '{node.TypeId}'."));
                continue;
            }

            nodes[node.NodeId] = new NodeInfo(node, definition);
        }

        var eventNodes = new List<int>();
        foreach (var pair in nodes)
        {
            if (pair.Value.Definition.IsEvent)
                eventNodes.Add(pair.Key);
        }

        if (eventNodes.Count == 0)
            errors.Add(new CompileError(0, "no_event_node", "The circuit has no event node."));

        var validDataConnections = new List<CircuitConnectionData>();
        var validExecConnections = new List<CircuitConnectionData>();
        var execOutputCounts = new Dictionary<PortKey, int>();
        var dataInputCounts = new Dictionary<PortKey, int>();
        var connectedDataInputs = new HashSet<PortKey>();

        foreach (var connection in graph.Connections)
        {
            var hasFromNodeId = nodeIds.Contains(connection.FromNode);
            var hasToNodeId = nodeIds.Contains(connection.ToNode);
            var hasFromNode = nodes.TryGetValue(connection.FromNode, out var fromNode);
            var hasToNode = nodes.TryGetValue(connection.ToNode, out var toNode);
            var hasInvalidEndpoint = false;
            PortDef? fromPort = null;
            PortDef? toPort = null;

            if (!hasFromNodeId)
            {
                var nodeId = hasToNodeId ? connection.ToNode : connection.FromNode;
                errors.Add(new CompileError(nodeId, "invalid_connection", "Connection references a node that does not exist."));
                hasInvalidEndpoint = true;
            }
            else if (hasFromNode)
            {
                fromPort = FindPort(fromNode!.Definition.Outputs, connection.FromPort);
                if (fromPort is null)
                {
                    errors.Add(new CompileError(connection.FromNode, "invalid_connection", "Connection references a source port that does not exist."));
                    hasInvalidEndpoint = true;
                }
            }

            if (!hasToNodeId)
            {
                var nodeId = hasFromNodeId ? connection.FromNode : connection.ToNode;
                errors.Add(new CompileError(nodeId, "invalid_connection", "Connection references a node that does not exist."));
                hasInvalidEndpoint = true;
            }
            else if (hasToNode)
            {
                toPort = FindPort(toNode!.Definition.Inputs, connection.ToPort);
                if (toPort is null)
                {
                    errors.Add(new CompileError(connection.ToNode, "invalid_connection", "Connection references a destination port that does not exist."));
                    hasInvalidEndpoint = true;
                }
            }

            if (hasInvalidEndpoint || !hasFromNode || !hasToNode)
                continue;

            var validFromPort = fromPort!;
            var validToPort = toPort!;

            if (validFromPort.Kind == PortKind.Exec)
                Increment(execOutputCounts, new PortKey(connection.FromNode, connection.FromPort));
            if (validToPort.Kind == PortKind.Data)
                Increment(dataInputCounts, new PortKey(connection.ToNode, connection.ToPort));

            if (validFromPort.Kind != validToPort.Kind)
            {
                errors.Add(new CompileError(connection.ToNode, "exec_input_from_data", "Exec and Data ports cannot be connected."));
                continue;
            }

            if (validFromPort.Kind == PortKind.Data)
            {
                connectedDataInputs.Add(new PortKey(connection.ToNode, connection.ToPort));

                if (validFromPort.Type != validToPort.Type)
                {
                    errors.Add(new CompileError(connection.ToNode, "type_mismatch", "Connected data ports have different types."));
                    continue;
                }

                validDataConnections.Add(connection);
            }
            else
            {
                validExecConnections.Add(connection);
            }
        }

        foreach (var pair in execOutputCounts)
        {
            if (pair.Value > 1)
                errors.Add(new CompileError(pair.Key.NodeId, "multi_exec_out", "An Exec output may have only one outgoing connection."));
        }

        foreach (var pair in dataInputCounts)
        {
            if (pair.Value > 1)
                errors.Add(new CompileError(pair.Key.NodeId, "multi_data_in", "A Data input may have only one incoming connection."));
        }

        foreach (var pair in nodes)
        {
            foreach (var input in pair.Value.Definition.Inputs)
            {
                if (input is { Kind: PortKind.Data, Required: true } &&
                    !connectedDataInputs.Contains(new PortKey(pair.Key, input.Name)) &&
                    !pair.Value.Data.InlineParams.ContainsKey(input.Name))
                {
                    errors.Add(new CompileError(pair.Key, "required_input_missing", $"Required Data input '{input.Name}' is missing."));
                }
            }
        }

        AddDataCycleErrors(nodes, validDataConnections, errors);
        AddUnreachableErrors(nodes, eventNodes, validExecConnections, validDataConnections, errors);

        return new CompileResult
        {
            Errors = errors,
            Circuit = errors.Count == 0 ? new CompiledCircuit() : null,
        };
    }

    private static void AddDataCycleErrors(
        Dictionary<int, NodeInfo> nodes,
        List<CircuitConnectionData> dataConnections,
        List<CompileError> errors)
    {
        var incomingCounts = new Dictionary<int, int>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (var nodeId in nodes.Keys)
            incomingCounts[nodeId] = 0;

        foreach (var connection in dataConnections)
        {
            incomingCounts[connection.ToNode]++;
            if (!outgoing.TryGetValue(connection.FromNode, out var destinations))
            {
                destinations = new List<int>();
                outgoing.Add(connection.FromNode, destinations);
            }

            destinations.Add(connection.ToNode);
        }

        var ready = new Queue<int>();
        foreach (var pair in incomingCounts)
        {
            if (pair.Value == 0)
                ready.Enqueue(pair.Key);
        }

        while (ready.Count > 0)
        {
            var nodeId = ready.Dequeue();
            if (!outgoing.TryGetValue(nodeId, out var destinations))
                continue;

            foreach (var destination in destinations)
            {
                incomingCounts[destination]--;
                if (incomingCounts[destination] == 0)
                    ready.Enqueue(destination);
            }
        }

        foreach (var pair in incomingCounts)
        {
            if (pair.Value > 0)
                errors.Add(new CompileError(pair.Key, "data_cycle", "Data connections contain a cycle."));
        }
    }

    private static void AddUnreachableErrors(
        Dictionary<int, NodeInfo> nodes,
        List<int> eventNodes,
        List<CircuitConnectionData> execConnections,
        List<CircuitConnectionData> dataConnections,
        List<CompileError> errors)
    {
        var reachable = new HashSet<int>(eventNodes);
        var queue = new Queue<int>(eventNodes);
        var outgoingExec = new Dictionary<int, List<int>>();

        foreach (var connection in execConnections)
        {
            if (!outgoingExec.TryGetValue(connection.FromNode, out var destinations))
            {
                destinations = new List<int>();
                outgoingExec.Add(connection.FromNode, destinations);
            }

            destinations.Add(connection.ToNode);
        }

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!outgoingExec.TryGetValue(nodeId, out var destinations))
                continue;

            foreach (var destination in destinations)
            {
                if (reachable.Add(destination))
                    queue.Enqueue(destination);
            }
        }

        foreach (var pair in nodes)
        {
            if (reachable.Contains(pair.Key) || IsConsumedDataSupplier(pair.Key, pair.Value.Definition, dataConnections, reachable))
                continue;

            errors.Add(new CompileError(pair.Key, "unreachable_node", "Node is not reachable from an event node."));
        }
    }

    private static bool IsConsumedDataSupplier(
        int nodeId,
        NodeDef definition,
        List<CircuitConnectionData> dataConnections,
        HashSet<int> reachable)
    {
        foreach (var input in definition.Inputs)
        {
            if (input.Kind == PortKind.Exec)
                return false;
        }

        foreach (var output in definition.Outputs)
        {
            if (output.Kind == PortKind.Exec)
                return false;
        }

        foreach (var connection in dataConnections)
        {
            if (connection.FromNode == nodeId && reachable.Contains(connection.ToNode))
                return true;
        }

        return false;
    }

    private static PortDef? FindPort(IReadOnlyList<PortDef> ports, string name)
    {
        foreach (var port in ports)
        {
            if (port.Name == name)
                return port;
        }

        return null;
    }

    private static void Increment(Dictionary<PortKey, int> counts, PortKey key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private sealed record NodeInfo(CircuitNodeData Data, NodeDef Definition);
    private readonly record struct PortKey(int NodeId, string PortName);
}
