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
                    !HasInlineValue(pair.Value.Data, input))
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
            Circuit = errors.Count == 0 ? GenerateCircuit(nodes, validDataConnections, validExecConnections) : null,
        };
    }

    private static CompiledCircuit GenerateCircuit(
        Dictionary<int, NodeInfo> nodes,
        List<CircuitConnectionData> dataConnections,
        List<CircuitConnectionData> execConnections)
    {
        return new CodeGenerator(nodes, dataConnections, execConnections).Generate();
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

        var acceptedDataSuppliers = FindAcceptedDataSuppliers(nodes, dataConnections, reachable);
        foreach (var pair in nodes)
        {
            if (reachable.Contains(pair.Key) || acceptedDataSuppliers.Contains(pair.Key))
                continue;

            errors.Add(new CompileError(pair.Key, "unreachable_node", "Node is not reachable from an event node."));
        }
    }

    private static HashSet<int> FindAcceptedDataSuppliers(
        Dictionary<int, NodeInfo> nodes,
        List<CircuitConnectionData> dataConnections,
        HashSet<int> reachable)
    {
        var incomingData = new Dictionary<int, List<int>>();
        foreach (var connection in dataConnections)
        {
            if (!incomingData.TryGetValue(connection.ToNode, out var suppliers))
            {
                suppliers = new List<int>();
                incomingData.Add(connection.ToNode, suppliers);
            }

            suppliers.Add(connection.FromNode);
        }

        var accepted = new HashSet<int>();
        var pendingConsumers = new Queue<int>(reachable);
        while (pendingConsumers.Count > 0)
        {
            var consumerNodeId = pendingConsumers.Dequeue();
            if (!incomingData.TryGetValue(consumerNodeId, out var suppliers))
                continue;

            foreach (var supplierNodeId in suppliers)
            {
                if (!IsPureDataNode(nodes[supplierNodeId].Definition) || !accepted.Add(supplierNodeId))
                    continue;

                pendingConsumers.Enqueue(supplierNodeId);
            }
        }

        return accepted;
    }

    private static bool IsPureDataNode(NodeDef definition)
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

        return true;
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

    private static bool HasInlineValue(CircuitNodeData node, PortDef input)
    {
        if (node.InlineParams.ContainsKey(input.Name))
            return true;

        return input.Type == DataType.Vector &&
            node.InlineParams.ContainsKey($"{input.Name}_x") &&
            node.InlineParams.ContainsKey($"{input.Name}_y") &&
            node.InlineParams.ContainsKey($"{input.Name}_z");
    }

    private static void Increment(Dictionary<PortKey, int> counts, PortKey key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private sealed record NodeInfo(CircuitNodeData Data, NodeDef Definition);
    private readonly record struct PortKey(int NodeId, string PortName);

    private sealed class CodeGenerator
    {
        private readonly Dictionary<int, NodeInfo> _nodes;
        private readonly List<KeyValuePair<int, NodeInfo>> _orderedNodes;
        private readonly Dictionary<PortKey, CircuitConnectionData> _dataInputs = new();
        private readonly Dictionary<PortKey, CircuitConnectionData> _execOutputs = new();
        private readonly Dictionary<PortKey, int> _registers = new();
        private readonly List<Instruction> _instructions = new();
        private readonly HashSet<int> _emittedExecNodes = new();
        private int _nextRegister;

        public CodeGenerator(
            Dictionary<int, NodeInfo> nodes,
            List<CircuitConnectionData> dataConnections,
            List<CircuitConnectionData> execConnections)
        {
            _nodes = nodes;
            _orderedNodes = new List<KeyValuePair<int, NodeInfo>>(nodes);
            _orderedNodes.Sort(static (left, right) => left.Key.CompareTo(right.Key));

            dataConnections.Sort(CompareConnections);
            foreach (var connection in dataConnections)
                _dataInputs.Add(new PortKey(connection.ToNode, connection.ToPort), connection);

            execConnections.Sort(CompareConnections);
            foreach (var connection in execConnections)
                _execOutputs.Add(new PortKey(connection.FromNode, connection.FromPort), connection);

            foreach (var pair in _orderedNodes)
            {
                foreach (var output in pair.Value.Definition.Outputs)
                {
                    if (output.Kind == PortKind.Data)
                    {
                        _registers.Add(new PortKey(pair.Key, output.Name), _nextRegister);
                        _nextRegister += output.Type == DataType.Vector ? 3 : 1;
                    }
                }
            }
        }

        public CompiledCircuit Generate()
        {
            var startEntry = _instructions.Count;
            foreach (var pair in _orderedNodes)
            {
                if (!pair.Value.Definition.IsEvent)
                    continue;

                EmitExec(GetExecDestination(pair.Key, "out"));
                break;
            }

            _instructions.Add(new Instruction(OpCode.Jump, startEntry, 0, 0, 0));
            return new CompiledCircuit
            {
                Instructions = _instructions.ToArray(),
                RegisterCount = _nextRegister,
                StartEntry = startEntry,
                Sensors = GetSensors(),
            };
        }

        private void EmitExec(int? nodeId)
        {
            if (!nodeId.HasValue || !_emittedExecNodes.Add(nodeId.Value))
                return;

            var node = _nodes[nodeId.Value];
            EmitDataDependencies(nodeId.Value);

            if (node.Data.TypeId == "flow_branch")
            {
                EmitBranch(nodeId.Value);
                return;
            }

            if (TryGetActionInstruction(node.Data.TypeId, nodeId.Value, out var instruction))
                _instructions.Add(instruction);

            EmitExec(GetExecDestination(nodeId.Value, "out"));
        }

        private void EmitBranch(int nodeId)
        {
            var conditionRegister = GetDataInputRegister(nodeId, "condition");
            var jumpIfFalseIndex = _instructions.Count;
            _instructions.Add(new Instruction(OpCode.JumpIfFalse, conditionRegister, 0, 0, 0));

            var trueStart = GetExecDestination(nodeId, "true");
            var falseStart = GetExecDestination(nodeId, "false");
            var convergence = FindConvergence(trueStart, falseStart);

            EmitExecUntil(trueStart, convergence);
            var skipFalseIndex = _instructions.Count;
            _instructions.Add(new Instruction(OpCode.Jump, 0, 0, 0, 0));

            var falseTarget = _instructions.Count;
            _instructions[jumpIfFalseIndex] = new Instruction(OpCode.JumpIfFalse, conditionRegister, falseTarget, 0, 0);
            EmitExecUntil(falseStart, convergence);

            var trueTarget = _instructions.Count;
            if (convergence.HasValue)
            {
                EmitExec(convergence);
            }

            _instructions[skipFalseIndex] = new Instruction(OpCode.Jump, trueTarget, 0, 0, 0);
        }

        private void EmitExecUntil(int? nodeId, int? stopNodeId)
        {
            if (!nodeId.HasValue || nodeId == stopNodeId || !_emittedExecNodes.Add(nodeId.Value))
                return;

            var node = _nodes[nodeId.Value];
            EmitDataDependencies(nodeId.Value);

            if (node.Data.TypeId == "flow_branch")
            {
                EmitBranch(nodeId.Value);
                return;
            }

            if (TryGetActionInstruction(node.Data.TypeId, nodeId.Value, out var instruction))
                _instructions.Add(instruction);

            EmitExecUntil(GetExecDestination(nodeId.Value, "out"), stopNodeId);
        }

        private void EmitDataDependencies(int nodeId)
        {
            var emittedDataNodes = new HashSet<int>();
            var node = _nodes[nodeId];
            foreach (var input in node.Definition.Inputs)
            {
                if (input.Kind != PortKind.Data || !_dataInputs.TryGetValue(new PortKey(nodeId, input.Name), out var connection))
                    continue;

                EmitDataNode(connection.FromNode, emittedDataNodes);
            }
        }

        private void EmitDataNode(int nodeId, HashSet<int> emittedDataNodes)
        {
            if (!emittedDataNodes.Add(nodeId))
                return;

            var node = _nodes[nodeId];
            foreach (var input in node.Definition.Inputs)
            {
                if (input.Kind == PortKind.Data && _dataInputs.TryGetValue(new PortKey(nodeId, input.Name), out var connection))
                    EmitDataNode(connection.FromNode, emittedDataNodes);
            }

            switch (node.Data.TypeId)
            {
                case "data_const_number":
                    _instructions.Add(new Instruction(
                        OpCode.LoadConst,
                        GetDataOutputRegister(nodeId, "value"),
                        0,
                        0,
                        GetInlineValue(node.Data, "value")));
                    break;
                case "sensor_cargo":
                    _instructions.Add(new Instruction(OpCode.ReadSensor, 0, 0, GetDataOutputRegister(nodeId, "count"), 0));
                    break;
                case "sensor_find_nearest_resource":
                    _instructions.Add(new Instruction(OpCode.FindNearestResource, 0, 0, GetDataOutputRegister(nodeId, "nearest"), 0));
                    break;
                case "sensor_nearest_storage":
                    _instructions.Add(new Instruction(OpCode.FindNearestStorage, 0, 0, GetDataOutputRegister(nodeId, "nearest"), 0));
                    break;
                case "data_compare":
                    _instructions.Add(new Instruction(
                        OpCode.Compare,
                        GetDataOutputRegister(nodeId, "result"),
                        GetDataInputRegister(nodeId, "a"),
                        GetDataInputRegister(nodeId, "b"),
                        GetInlineValue(node.Data, "mode")));
                    break;
            }
        }

        private bool TryGetActionInstruction(string typeId, int nodeId, out Instruction instruction)
        {
            switch (typeId)
            {
                case "action_move_to":
                    instruction = GetVectorActionInstruction(OpCode.MoveTo, nodeId);
                    return true;
                case "action_harvest":
                    instruction = GetVectorActionInstruction(OpCode.Harvest, nodeId);
                    return true;
                case "action_load":
                    instruction = new Instruction(OpCode.Load, 0, 0, 0, 0);
                    return true;
                case "action_unload":
                    instruction = new Instruction(OpCode.Unload, 0, 0, 0, 0);
                    return true;
                case "action_wait":
                    instruction = new Instruction(OpCode.Wait, GetDataInputRegister(nodeId, "ticks"), 0, 0, 0);
                    return true;
                default:
                    instruction = default;
                    return false;
            }
        }

        private int GetDataInputRegister(int nodeId, string portName)
        {
            if (_dataInputs.TryGetValue(new PortKey(nodeId, portName), out var connection))
                return GetDataOutputRegister(connection.FromNode, connection.FromPort);

            var register = _nextRegister++;
            _instructions.Add(new Instruction(OpCode.LoadConst, register, 0, 0, GetInlineValue(_nodes[nodeId].Data, portName)));
            return register;
        }

        private Instruction GetVectorActionInstruction(OpCode opCode, int nodeId)
        {
            if (_dataInputs.TryGetValue(new PortKey(nodeId, "target"), out var connection))
            {
                var baseRegister = GetDataOutputRegister(connection.FromNode, connection.FromPort);
                return new Instruction(opCode, baseRegister, baseRegister + 1, baseRegister + 2, 0);
            }

            return new Instruction(
                opCode,
                GetInlineComponentRegister(nodeId, "target_x"),
                GetInlineComponentRegister(nodeId, "target_y"),
                GetInlineComponentRegister(nodeId, "target_z"),
                0);
        }

        private int GetInlineComponentRegister(int nodeId, string componentName)
        {
            var register = _nextRegister++;
            _instructions.Add(new Instruction(
                OpCode.LoadConst,
                register,
                0,
                0,
                GetInlineValue(_nodes[nodeId].Data, componentName)));
            return register;
        }

        private int GetDataOutputRegister(int nodeId, string portName)
        {
            return _registers[new PortKey(nodeId, portName)];
        }

        private SensorMask GetSensors()
        {
            var sensors = SensorMask.None;
            foreach (var instruction in _instructions)
            {
                switch (instruction.Op)
                {
                    case OpCode.ReadSensor when instruction.A == 0:
                        sensors |= SensorMask.Cargo;
                        break;
                    case OpCode.FindNearestResource:
                        sensors |= SensorMask.NearestResource;
                        break;
                    case OpCode.FindNearestStorage:
                        sensors |= SensorMask.NearestStorage;
                        break;
                }
            }

            return sensors;
        }

        private int? GetExecDestination(int nodeId, string portName)
        {
            return _execOutputs.TryGetValue(new PortKey(nodeId, portName), out var connection)
                ? connection.ToNode
                : null;
        }

        private int? FindConvergence(int? firstStart, int? secondStart)
        {
            if (!firstStart.HasValue || !secondStart.HasValue)
                return null;

            var secondReachable = new HashSet<int>();
            AddReachable(secondStart.Value, secondReachable);

            return FindFirstCommonNode(firstStart.Value, secondReachable, new HashSet<int>());
        }

        private int? FindFirstCommonNode(int nodeId, HashSet<int> candidates, HashSet<int> visited)
        {
            if (!visited.Add(nodeId))
                return null;

            if (candidates.Contains(nodeId))
                return nodeId;

            var node = _nodes[nodeId];
            if (node.Data.TypeId == "flow_branch")
            {
                var trueDestination = GetExecDestination(nodeId, "true");
                if (trueDestination.HasValue)
                {
                    var trueConvergence = FindFirstCommonNode(trueDestination.Value, candidates, visited);
                    if (trueConvergence.HasValue)
                        return trueConvergence;
                }

                var falseDestination = GetExecDestination(nodeId, "false");
                if (falseDestination.HasValue)
                    return FindFirstCommonNode(falseDestination.Value, candidates, visited);

                return null;
            }

            var destination = GetExecDestination(nodeId, "out");
            return destination.HasValue
                ? FindFirstCommonNode(destination.Value, candidates, visited)
                : null;
        }

        private void AddReachable(int nodeId, HashSet<int> reachable)
        {
            if (!reachable.Add(nodeId))
                return;

            var node = _nodes[nodeId];
            if (node.Data.TypeId == "flow_branch")
            {
                var trueDestination = GetExecDestination(nodeId, "true");
                var falseDestination = GetExecDestination(nodeId, "false");
                if (trueDestination.HasValue)
                    AddReachable(trueDestination.Value, reachable);
                if (falseDestination.HasValue)
                    AddReachable(falseDestination.Value, reachable);
                return;
            }

            var destination = GetExecDestination(nodeId, "out");
            if (destination.HasValue)
                AddReachable(destination.Value, reachable);
        }

        private static int CompareConnections(CircuitConnectionData left, CircuitConnectionData right)
        {
            var comparison = left.FromNode.CompareTo(right.FromNode);
            if (comparison != 0)
                return comparison;

            comparison = StringComparer.Ordinal.Compare(left.FromPort, right.FromPort);
            if (comparison != 0)
                return comparison;

            comparison = left.ToNode.CompareTo(right.ToNode);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.ToPort, right.ToPort);
        }

        private static double GetInlineValue(CircuitNodeData node, string name)
        {
            return node.InlineParams.TryGetValue(name, out var value) ? value : 0;
        }
    }
}
