namespace SimCore.Circuits;

public enum PortKind { Exec, Data }

public enum DataType { None, Number, Bool, Vector }

public sealed record PortDef(string Name, PortKind Kind, DataType Type, bool Required);
