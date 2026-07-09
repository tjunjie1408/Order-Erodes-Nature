namespace SimCore.Circuits;

public sealed class CircuitGraphData
{
    public List<CircuitNodeData> Nodes { get; set; } = new();
    public List<CircuitConnectionData> Connections { get; set; } = new();
}

public sealed class CircuitNodeData
{
    public int NodeId { get; set; }
    public string TypeId { get; set; } = "";
    public Dictionary<string, double> InlineParams { get; set; } = new();
    public float EditorX { get; set; }
    public float EditorY { get; set; }
}

public sealed class CircuitConnectionData
{
    public int FromNode { get; set; }
    public string FromPort { get; set; } = "";
    public int ToNode { get; set; }
    public string ToPort { get; set; } = "";
}
