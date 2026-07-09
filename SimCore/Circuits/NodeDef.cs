namespace SimCore.Circuits;

public sealed record NodeDef(
    string TypeId,
    string DisplayName,
    IReadOnlyList<PortDef> Inputs,
    IReadOnlyList<PortDef> Outputs,
    bool IsEvent);
