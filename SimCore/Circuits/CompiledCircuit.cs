namespace SimCore.Circuits;

public sealed class CompiledCircuit
{
    public Instruction[] Instructions { get; init; } = Array.Empty<Instruction>();
    public int RegisterCount { get; init; }
    public int StartEntry { get; init; } = -1;
}
