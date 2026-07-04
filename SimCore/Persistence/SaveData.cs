namespace SimCore.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 1;
    public long TickCount { get; set; }
    public int NextStructureId { get; set; }
    public List<StructureData> Structures { get; set; } = new();
}

public sealed class StructureData
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
