namespace SimCore.Circuits;

public enum OpCode : byte
{
    Halt = 0,
    Jump,
    JumpIfFalse,
    LoadConst,
    Compare,
    ReadSensor,
    MoveTo,
    Harvest,
    Load,
    Unload,
    Wait,
    FindNearestResource,
    FindNearestStorage,
}

public readonly record struct Instruction(OpCode Op, int A, int B, int C, double Imm);
