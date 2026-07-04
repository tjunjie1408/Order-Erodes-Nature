namespace SimCore;

public abstract record SimEvent;

public sealed record StructurePlaced(int StructureId, string StructureType, GridPos Position) : SimEvent;

public sealed record StructureRemoved(int StructureId, GridPos Position) : SimEvent;

public sealed record CommandRejected(ICommand Command, string Reason) : SimEvent;
