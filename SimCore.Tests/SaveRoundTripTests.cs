using SimCore;
using SimCore.Persistence;
using Xunit;

namespace SimCore.Tests;

public class SaveRoundTripTests
{
    private static Simulation BuildSampleSim()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 0)));
        sim.Tick();
        sim.EnqueueCommand(new RemoveStructureCommand(new GridPos(0, 0, 0)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(2, 0, 5)));
        sim.Tick();
        sim.Tick();
        return sim;
    }

    [Fact]
    public void SaveRoundTrip_RestoresIdenticalState()
    {
        var original = BuildSampleSim();
        var json = SaveSerializer.ToJson(original.CreateSnapshot());
        var restored = Simulation.FromSnapshot(SaveSerializer.FromJson(json));

        Assert.Equal(json, SaveSerializer.ToJson(restored.CreateSnapshot()));
        Assert.Equal(original.TickCount, restored.TickCount);
        Assert.Equal(original.Structures.Count, restored.Structures.Count);
    }

    [Fact]
    public void RestoredSim_ContinuesWithSameNextId()
    {
        var original = BuildSampleSim();
        original.DrainEvents(new List<SimEvent>());
        var restored = Simulation.FromSnapshot(
            SaveSerializer.FromJson(SaveSerializer.ToJson(original.CreateSnapshot())));
        var position = new GridPos(9, 0, 9);

        original.EnqueueCommand(new PlaceStructureCommand("base_block", position));
        restored.EnqueueCommand(new PlaceStructureCommand("base_block", position));
        original.Tick();
        restored.Tick();

        var originalEvents = new List<SimEvent>();
        var restoredEvents = new List<SimEvent>();
        original.DrainEvents(originalEvents);
        restored.DrainEvents(restoredEvents);
        var originalPlaced = Assert.Single(originalEvents.OfType<StructurePlaced>());
        var restoredPlaced = Assert.Single(restoredEvents.OfType<StructurePlaced>());
        Assert.Equal(originalPlaced.StructureId, restoredPlaced.StructureId);
    }

    [Fact]
    public void SameCommandSequence_ProducesIdenticalSnapshots()
    {
        var a = BuildSampleSim();
        var b = BuildSampleSim();
        Assert.Equal(SaveSerializer.ToJson(a.CreateSnapshot()),
                     SaveSerializer.ToJson(b.CreateSnapshot()));
    }
}
