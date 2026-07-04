namespace SimCore;

public interface ICommand { }

public sealed record PlaceStructureCommand(string StructureType, GridPos Position) : ICommand;

public sealed record RemoveStructureCommand(GridPos Position) : ICommand;
