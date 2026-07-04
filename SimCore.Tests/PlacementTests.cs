using SimCore;
using Xunit;

namespace SimCore.Tests;

public class PlacementTests
{
    private static List<SimEvent> TickAndDrain(Simulation sim)
    {
        sim.Tick();
        var events = new List<SimEvent>();
        sim.DrainEvents(events);
        return events;
    }

    [Fact]
    public void PlaceOnOccupiedCell_EmitsCommandRejected_AndKeepsOriginal()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 1)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 1)));
        var events = TickAndDrain(sim);

        Assert.Single(sim.Structures);
        var rejected = Assert.Single(events.OfType<CommandRejected>());
        Assert.Equal("cell_occupied", rejected.Reason);
    }

    [Fact]
    public void RemoveEmptyCell_EmitsCommandRejected()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new RemoveStructureCommand(new GridPos(5, 0, 5)));
        var events = TickAndDrain(sim);
        var rejected = Assert.Single(events.OfType<CommandRejected>());
        Assert.Equal("cell_empty", rejected.Reason);
    }

    [Fact]
    public void PlaceThenRemove_RoundTrip_CellIsPlaceableAgain()
    {
        var sim = new Simulation();
        var pos = new GridPos(2, 0, 3);
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", pos));
        var placed = Assert.Single(TickAndDrain(sim).OfType<StructurePlaced>());

        sim.EnqueueCommand(new RemoveStructureCommand(pos));
        var removed = Assert.Single(TickAndDrain(sim).OfType<StructureRemoved>());

        Assert.Equal(placed.StructureId, removed.StructureId);
        Assert.True(sim.CanPlace(pos));
        Assert.Empty(sim.Structures);
    }

    [Fact]
    public void CanPlace_MatchesApplyRule()
    {
        var sim = new Simulation();
        var pos = new GridPos(0, 0, 0);
        Assert.True(sim.CanPlace(pos));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", pos));
        sim.Tick();
        Assert.False(sim.CanPlace(pos));
    }
}
