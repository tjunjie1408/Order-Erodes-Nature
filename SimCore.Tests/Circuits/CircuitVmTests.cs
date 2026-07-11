using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public sealed class CircuitVmTests
{
    [Fact]
    public void Wait_SuspendsForConfiguredTicksThenReachesHalt()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.LoadConst, 0, 0, 0, 2),
            new(OpCode.Wait, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 1);
        var io = new VmIo();
        var enteredPcs = EnteredPcs();

        vm.Tick(io, enteredPcs);
        Assert.Equal(VmStatus.Suspended, vm.Status);
        Assert.Equal(new[] { 0, 1 }, enteredPcs);

        enteredPcs.Clear();
        vm.Tick(io, enteredPcs);
        Assert.Empty(enteredPcs);
        Assert.Equal(VmStatus.Suspended, vm.Status);

        vm.Tick(io, enteredPcs);
        Assert.Equal(VmStatus.Idle, vm.Status);
        Assert.Equal(new[] { 2 }, enteredPcs);
    }

    [Fact]
    public void MoveTo_RequestsCoordinatesAndResumesOnlyAfterDone()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.MoveTo, 0, 1, 2, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 3);
        vm.Registers[0] = 1;
        vm.Registers[1] = 2;
        vm.Registers[2] = 3;
        var io = new VmIo();
        var enteredPcs = EnteredPcs();

        vm.Tick(io, enteredPcs);
        Assert.Equal(VmStatus.Suspended, vm.Status);
        Assert.Equal(ActionKind.MoveTo, io.RequestedAction);
        Assert.Equal(1, io.ActionX);
        Assert.Equal(2, io.ActionY);
        Assert.Equal(3, io.ActionZ);
        Assert.Equal(new[] { 0 }, enteredPcs);

        io.PendingActionResult = ActionResult.InProgress;
        enteredPcs.Clear();
        vm.Tick(io, enteredPcs);
        Assert.Equal(VmStatus.Suspended, vm.Status);
        Assert.Empty(enteredPcs);

        io.PendingActionResult = ActionResult.Done;
        vm.Tick(io, enteredPcs);
        Assert.Equal(VmStatus.Idle, vm.Status);
        Assert.Equal(ActionKind.None, io.RequestedAction);
        Assert.Equal(new[] { 1 }, enteredPcs);
    }

    [Fact]
    public void SelfJump_CrashesAfterInstructionBudgetAtCurrentProgramCounter()
    {
        var vm = Load(new[] { new Instruction(OpCode.Jump, 0, 0, 0, 0) });
        var enteredPcs = EnteredPcs();

        vm.Tick(new VmIo(), enteredPcs);

        Assert.Equal(VmStatus.Crashed, vm.Status);
        Assert.Equal(0, vm.CrashPc);
        Assert.Equal(CircuitVm.InstructionBudgetPerTick, enteredPcs.Count);
    }

    [Fact]
    public void Reset_RestartsAtEntryAndClearsCrashStateAndRegisters()
    {
        var vm = Load(new[] { new Instruction(OpCode.Jump, 0, 0, 0, 0) }, registerCount: 1);
        vm.Registers[0] = 42;

        vm.Tick(new VmIo(), EnteredPcs());
        vm.Reset();

        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.Equal(0, vm.ProgramCounter);
        Assert.Equal(-1, vm.CrashPc);
        Assert.Equal(0, vm.Registers[0]);
    }

    [Fact]
    public void JumpIfFalse_TakesFalsePath()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.LoadConst, 0, 0, 0, 0),
            new(OpCode.JumpIfFalse, 0, 3, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 1);
        var enteredPcs = EnteredPcs();

        vm.Tick(new VmIo(), enteredPcs);

        Assert.Equal(VmStatus.Idle, vm.Status);
        Assert.Equal(new[] { 0, 1, 3 }, enteredPcs);
    }

    [Theory]
    [InlineData(3, 2, 0, 1)]
    [InlineData(3, 3, 1, 1)]
    [InlineData(2, 3, 2, 1)]
    [InlineData(2, 3, 0, 0)]
    public void Compare_UsesLockedModes(double left, double right, double mode, double expected)
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.LoadConst, 0, 0, 0, left),
            new(OpCode.LoadConst, 1, 0, 0, right),
            new(OpCode.Compare, 2, 0, 1, mode),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 3);

        vm.Tick(new VmIo(), EnteredPcs());

        Assert.Equal(expected, vm.Registers[2]);
    }

    [Fact]
    public void ReadSensor_CargoUsesSensorIdInAAndDestinationInC()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.ReadSensor, 0, 0, 1, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 2);
        var io = new VmIo { SensorCargo = 17.5 };

        vm.Tick(io, EnteredPcs());

        Assert.Equal(17.5, vm.Registers[1]);
    }

    [Fact]
    public void LoadProgram_CopiesInstructionsBeforeExecuting()
    {
        var instructions = new Instruction[]
        {
            new(OpCode.LoadConst, 0, 0, 0, 1),
            new(OpCode.Halt, 0, 0, 0, 0),
        };
        var vm = Load(instructions, registerCount: 1);
        instructions[0] = new Instruction(OpCode.LoadConst, 0, 0, 0, 99);

        vm.Tick(new VmIo(), EnteredPcs());

        Assert.Equal(1, vm.Registers[0]);
    }

    [Fact]
    public void FindNearestResource_CopiesVectorAndFoundFlagToConsecutiveRegisters()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.FindNearestResource, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 4);
        var io = new VmIo
        {
            NearestResourceX = 1,
            NearestResourceY = 2,
            NearestResourceZ = 3,
            NearestResourceFound = true,
        };

        vm.Tick(io, EnteredPcs());

        Assert.Equal(new[] { 1d, 2d, 3d, 1d }, vm.Registers);
    }

    [Fact]
    public void FindNearestStorage_CopiesVectorAndFoundFlagToConsecutiveRegisters()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.FindNearestStorage, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        }, registerCount: 4);
        var io = new VmIo
        {
            NearestStorageX = 4,
            NearestStorageY = 5,
            NearestStorageZ = 6,
            NearestStorageFound = false,
        };

        vm.Tick(io, EnteredPcs());

        Assert.Equal(new[] { 4d, 5d, 6d, 0d }, vm.Registers);
    }

    [Fact]
    public void Tick_RequiresEntryListCapacityForInstructionBudget()
    {
        var vm = Load(new[] { new Instruction(OpCode.Jump, 0, 0, 0, 0) });
        var enteredPcs = new List<int>(CircuitVm.InstructionBudgetPerTick - 1);

        Assert.Throws<ArgumentException>(() => vm.Tick(new VmIo(), enteredPcs));

        Assert.Empty(enteredPcs);
        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.Equal(0, vm.ProgramCounter);
    }

    [Fact]
    public void Tick_WithPreallocatedEntryListDoesNotAllocate()
    {
        var warmupVm = Load(new[] { new Instruction(OpCode.Halt, 0, 0, 0, 0) });
        warmupVm.Tick(new VmIo(), EnteredPcs());

        var vm = Load(new[] { new Instruction(OpCode.Jump, 0, 0, 0, 0) });
        var io = new VmIo();
        var enteredPcs = EnteredPcs();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        vm.Tick(io, enteredPcs);

        Assert.Equal(allocatedBefore, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(CircuitVm.InstructionBudgetPerTick, enteredPcs.Count);
    }

    [Fact]
    public void ChainedHostActions_RequireAResultForEachRequestedAction()
    {
        var vm = Load(new Instruction[]
        {
            new(OpCode.Load, 0, 0, 0, 0),
            new(OpCode.Unload, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
        });
        var io = new VmIo();
        var enteredPcs = EnteredPcs();

        vm.Tick(io, enteredPcs);
        Assert.Equal(ActionKind.Load, io.RequestedAction);

        io.PendingActionResult = ActionResult.Done;
        enteredPcs.Clear();
        vm.Tick(io, enteredPcs);
        Assert.Equal(ActionKind.Unload, io.RequestedAction);
        Assert.Equal(ActionResult.InProgress, io.PendingActionResult);
        Assert.Equal(VmStatus.Suspended, vm.Status);

        enteredPcs.Clear();
        vm.Tick(io, enteredPcs);
        Assert.Equal(ActionKind.Unload, io.RequestedAction);
        Assert.Equal(VmStatus.Suspended, vm.Status);
        Assert.Empty(enteredPcs);
    }

    private static CircuitVm Load(Instruction[] instructions, int registerCount = 0)
    {
        var vm = new CircuitVm();
        vm.LoadProgram(new CompiledCircuit
        {
            Instructions = instructions,
            RegisterCount = registerCount,
            StartEntry = 0,
        });
        return vm;
    }

    private static List<int> EnteredPcs()
    {
        return new List<int>(CircuitVm.InstructionBudgetPerTick);
    }
}
