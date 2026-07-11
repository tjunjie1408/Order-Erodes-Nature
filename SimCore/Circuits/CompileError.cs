namespace SimCore.Circuits;

public sealed record CompileError(int NodeId, string Code, string Message);

public sealed class CompileResult
{
    public CompiledCircuit? Circuit { get; init; }
    public List<CompileError> Errors { get; init; } = new();
    public bool Success => Errors.Count == 0;
}
