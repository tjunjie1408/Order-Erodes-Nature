using SimCore;
using Xunit;

namespace SimCore.Tests;

public class SimulationTests
{
    [Fact]
    public void GridPos_ValueEquality()
    {
        Assert.Equal(new GridPos(1, 2, 3), new GridPos(1, 2, 3));
    }

    [Fact]
    public void Tick_IncrementsTickCount()
    {
        var sim = new Simulation();
        sim.Tick();
        sim.Tick();
        Assert.Equal(2, sim.TickCount);
    }

    [Fact]
    public void EnqueueCommand_IsNotAppliedUntilTick()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
        Assert.Empty(sim.Structures);
        sim.Tick();
        Assert.Single(sim.Structures);
    }

    [Fact]
    public void DrainEvents_ReturnsEventsOnceAndClears()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
        sim.Tick();
        var events = new List<SimEvent>();
        sim.DrainEvents(events);
        Assert.Single(events);
        Assert.IsType<StructurePlaced>(events[0]);
        events.Clear();
        sim.DrainEvents(events);
        Assert.Empty(events);
    }
}
