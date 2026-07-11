namespace SimCore.Circuits;

[Flags]
public enum SensorMask : uint
{
    None = 0,
    Cargo = 1,
    NearestResource = 2,
    NearestStorage = 4,
}

public sealed class CompiledCircuit
{
    public Instruction[] Instructions { get; init; } = Array.Empty<Instruction>();
    public int RegisterCount { get; init; }
    public int StartEntry { get; init; } = -1;
    public SensorMask Sensors { get; init; }
}
