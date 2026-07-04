using Godot;
using SimCore;
using System;
using System.Collections.Generic;

public partial class SimDriver : Node
{
    public Simulation Sim { get; private set; } = new();

    public event Action<SimEvent>? SimEventEmitted;

    private const double TickInterval = 1.0 / Simulation.TicksPerSecond;
    private double _accumulator;
    private readonly List<SimEvent> _frameEvents = new();

    public override void _Process(double delta)
    {
        _accumulator += delta;
        while (_accumulator >= TickInterval)
        {
            _accumulator -= TickInterval;
            Sim.Tick();
            Sim.DrainEvents(_frameEvents);
            foreach (var simEvent in _frameEvents)
                SimEventEmitted?.Invoke(simEvent);
            _frameEvents.Clear();
        }
    }
}
