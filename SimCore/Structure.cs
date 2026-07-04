namespace SimCore;

public sealed class Structure
{
    public required int Id { get; init; }
    public required string Type { get; init; }
    public required GridPos Position { get; init; }
}
