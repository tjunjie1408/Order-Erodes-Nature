using System.Collections.ObjectModel;

namespace SimCore.Circuits;

public static class NodeCatalog
{
    public static IReadOnlyDictionary<string, NodeDef> All { get; } = BuildCatalog();

    private static IReadOnlyDictionary<string, NodeDef> BuildCatalog()
    {
        var nodes = new Dictionary<string, NodeDef>(StringComparer.Ordinal)
        {
            ["event_start"] = Node(
                "event_start",
                "On Start",
                isEvent: true,
                inputs: Ports(),
                outputs: Ports(ExecOut("out"))),

            ["action_move_to"] = Action(
                "action_move_to",
                "Move To",
                DataIn("target", DataType.Vector, required: true)),

            ["action_harvest"] = Action(
                "action_harvest",
                "Harvest",
                DataIn("target", DataType.Vector, required: true)),

            ["action_load"] = Action(
                "action_load",
                "Load"),

            ["action_unload"] = Action(
                "action_unload",
                "Unload"),

            ["flow_branch"] = Node(
                "flow_branch",
                "Branch",
                isEvent: false,
                inputs: Ports(ExecIn("in"), DataIn("condition", DataType.Bool, required: true)),
                outputs: Ports(ExecOut("true"), ExecOut("false"))),

            ["action_wait"] = Action(
                "action_wait",
                "Wait",
                DataIn("ticks", DataType.Number, required: true)),

            ["data_const_number"] = Node(
                "data_const_number",
                "Number",
                isEvent: false,
                inputs: Ports(),
                outputs: Ports(DataOut("value", DataType.Number))),

            ["data_compare"] = Node(
                "data_compare",
                "Compare",
                isEvent: false,
                inputs: Ports(
                    DataIn("a", DataType.Number, required: true),
                    DataIn("b", DataType.Number, required: true)),
                outputs: Ports(DataOut("result", DataType.Bool))),

            ["sensor_cargo"] = Node(
                "sensor_cargo",
                "Cargo Count",
                isEvent: false,
                inputs: Ports(),
                outputs: Ports(DataOut("count", DataType.Number))),
        };

        return new ReadOnlyDictionary<string, NodeDef>(nodes);
    }

    private static NodeDef Action(string typeId, string displayName, params PortDef[] dataInputs)
    {
        var inputs = new PortDef[dataInputs.Length + 1];
        inputs[0] = ExecIn("in");
        dataInputs.CopyTo(inputs, 1);

        return Node(
            typeId,
            displayName,
            isEvent: false,
            inputs: Ports(inputs),
            outputs: Ports(ExecOut("out")));
    }

    private static NodeDef Node(
        string typeId,
        string displayName,
        bool isEvent,
        IReadOnlyList<PortDef> inputs,
        IReadOnlyList<PortDef> outputs)
    {
        return new NodeDef(typeId, displayName, inputs, outputs, isEvent);
    }

    private static IReadOnlyList<PortDef> Ports(params PortDef[] ports)
    {
        return Array.AsReadOnly(ports);
    }

    private static PortDef ExecIn(string name)
    {
        return new PortDef(name, PortKind.Exec, DataType.None, Required: true);
    }

    private static PortDef ExecOut(string name)
    {
        return new PortDef(name, PortKind.Exec, DataType.None, Required: false);
    }

    private static PortDef DataIn(string name, DataType type, bool required)
    {
        return new PortDef(name, PortKind.Data, type, required);
    }

    private static PortDef DataOut(string name, DataType type)
    {
        return new PortDef(name, PortKind.Data, type, Required: false);
    }
}
