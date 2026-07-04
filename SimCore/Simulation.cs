namespace SimCore;

public sealed class Simulation
{
    public const int TicksPerSecond = 20;

    public long TickCount { get; private set; }

    private readonly Queue<ICommand> _commands = new();
    private readonly List<SimEvent> _events = new();
    private readonly Dictionary<GridPos, Structure> _byPos = new();
    private readonly Dictionary<int, Structure> _byId = new();
    private int _nextStructureId = 1;

    public IReadOnlyCollection<Structure> Structures => _byId.Values;

    public void EnqueueCommand(ICommand command) => _commands.Enqueue(command);

    public void Tick()
    {
        while (_commands.Count > 0)
            Apply(_commands.Dequeue());
        TickCount++;
    }

    /// <summary>Read-only validity check for the presentation layer's ghost preview (shares the same rule as Apply).</summary>
    public bool CanPlace(GridPos pos) => !_byPos.ContainsKey(pos);

    public void DrainEvents(List<SimEvent> into)
    {
        into.AddRange(_events);
        _events.Clear();
    }

    private void Apply(ICommand command)
    {
        switch (command)
        {
            case PlaceStructureCommand place:
                if (!CanPlace(place.Position))
                {
                    _events.Add(new CommandRejected(command, "cell_occupied"));
                    return;
                }
                var structure = new Structure
                {
                    Id = _nextStructureId++,
                    Type = place.StructureType,
                    Position = place.Position,
                };
                _byPos[structure.Position] = structure;
                _byId[structure.Id] = structure;
                _events.Add(new StructurePlaced(structure.Id, structure.Type, structure.Position));
                break;

            case RemoveStructureCommand remove:
                if (!_byPos.TryGetValue(remove.Position, out var existing))
                {
                    _events.Add(new CommandRejected(command, "cell_empty"));
                    return;
                }
                _byPos.Remove(existing.Position);
                _byId.Remove(existing.Id);
                _events.Add(new StructureRemoved(existing.Id, existing.Position));
                break;
        }
    }
}
