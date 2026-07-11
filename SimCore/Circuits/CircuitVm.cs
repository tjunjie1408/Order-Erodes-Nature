namespace SimCore.Circuits;

public sealed class CircuitVm
{
    private Instruction[] _instructions = Array.Empty<Instruction>();
    private int _startEntry = -1;
    private ActionKind _activeAction;

    public VmStatus Status { get; private set; } = VmStatus.Idle;
    public int ProgramCounter { get; private set; }
    public int CrashPc { get; private set; } = -1;
    public double[] Registers { get; private set; } = Array.Empty<double>();
    public const int InstructionBudgetPerTick = 256;

    private int WaitTicksRemaining { get; set; }

    public void LoadProgram(CompiledCircuit circuit)
    {
        _instructions = (Instruction[])circuit.Instructions.Clone();
        _startEntry = circuit.StartEntry;
        Registers = new double[circuit.RegisterCount];
        Reset();
    }

    public void Reset()
    {
        ProgramCounter = _startEntry;
        Array.Clear(Registers);
        CrashPc = -1;
        WaitTicksRemaining = 0;
        _activeAction = ActionKind.None;
        Status = VmStatus.Running;
    }

    public void Tick(VmIo io, List<int> enteredPcs)
    {
        if (Status is VmStatus.Idle or VmStatus.Crashed)
            return;

        if (Status == VmStatus.Suspended)
        {
            if (_activeAction == ActionKind.Wait)
            {
                if (WaitTicksRemaining > 0)
                    WaitTicksRemaining--;

                if (WaitTicksRemaining > 0)
                    return;

                ProgramCounter++;
                _activeAction = ActionKind.None;
                Status = VmStatus.Running;
            }
            else if (_activeAction != ActionKind.None)
            {
                if (io.PendingActionResult == ActionResult.InProgress)
                    return;

                ClearActionRequest(io);
                ProgramCounter++;
                _activeAction = ActionKind.None;
                Status = VmStatus.Running;
            }
        }

        var instructionsExecuted = 0;
        while (Status == VmStatus.Running)
        {
            if (instructionsExecuted == InstructionBudgetPerTick ||
                ProgramCounter < 0 || ProgramCounter >= _instructions.Length)
            {
                CrashPc = ProgramCounter;
                Status = VmStatus.Crashed;
                return;
            }

            var instruction = _instructions[ProgramCounter];
            enteredPcs.Add(ProgramCounter);
            instructionsExecuted++;

            switch (instruction.Op)
            {
                case OpCode.Halt:
                    Status = VmStatus.Idle;
                    return;
                case OpCode.Jump:
                    ProgramCounter = instruction.A;
                    break;
                case OpCode.JumpIfFalse:
                    ProgramCounter = Registers[instruction.A] == 0 ? instruction.B : ProgramCounter + 1;
                    break;
                case OpCode.LoadConst:
                    Registers[instruction.A] = instruction.Imm;
                    ProgramCounter++;
                    break;
                case OpCode.Compare:
                    Registers[instruction.A] = Compare(Registers[instruction.B], Registers[instruction.C], instruction.Imm)
                        ? 1
                        : 0;
                    ProgramCounter++;
                    break;
                case OpCode.ReadSensor:
                    if (instruction.A == 0)
                        Registers[instruction.C] = io.SensorCargo;
                    ProgramCounter++;
                    break;
                case OpCode.FindNearestResource:
                    Registers[instruction.C] = io.NearestResourceX;
                    Registers[instruction.C + 1] = io.NearestResourceY;
                    Registers[instruction.C + 2] = io.NearestResourceZ;
                    Registers[instruction.C + 3] = io.NearestResourceFound ? 1 : 0;
                    ProgramCounter++;
                    break;
                case OpCode.FindNearestStorage:
                    Registers[instruction.C] = io.NearestStorageX;
                    Registers[instruction.C + 1] = io.NearestStorageY;
                    Registers[instruction.C + 2] = io.NearestStorageZ;
                    Registers[instruction.C + 3] = io.NearestStorageFound ? 1 : 0;
                    ProgramCounter++;
                    break;
                case OpCode.MoveTo:
                    RequestAction(io, ActionKind.MoveTo, Registers[instruction.A], Registers[instruction.B], Registers[instruction.C]);
                    return;
                case OpCode.Harvest:
                    RequestAction(io, ActionKind.Harvest, Registers[instruction.A], Registers[instruction.B], Registers[instruction.C]);
                    return;
                case OpCode.Load:
                    RequestAction(io, ActionKind.Load, 0, 0, 0);
                    return;
                case OpCode.Unload:
                    RequestAction(io, ActionKind.Unload, 0, 0, 0);
                    return;
                case OpCode.Wait:
                    WaitTicksRemaining = (int)Registers[instruction.A];
                    _activeAction = ActionKind.Wait;
                    Status = VmStatus.Suspended;
                    return;
                default:
                    CrashPc = ProgramCounter;
                    Status = VmStatus.Crashed;
                    return;
            }
        }
    }

    private static bool Compare(double left, double right, double mode)
    {
        return mode switch
        {
            0 => left > right,
            1 => left == right,
            2 => left < right,
            _ => false,
        };
    }

    private void RequestAction(VmIo io, ActionKind action, double x, double y, double z)
    {
        ClearActionRequest(io);
        io.RequestedAction = action;
        io.ActionX = x;
        io.ActionY = y;
        io.ActionZ = z;
        _activeAction = action;
        Status = VmStatus.Suspended;
    }

    private static void ClearActionRequest(VmIo io)
    {
        io.RequestedAction = ActionKind.None;
        io.ActionX = 0;
        io.ActionY = 0;
        io.ActionZ = 0;
    }
}
