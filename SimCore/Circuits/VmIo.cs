namespace SimCore.Circuits;

public enum VmStatus : byte
{
    Idle,
    Running,
    Suspended,
    Crashed,
}

public enum ActionKind : byte
{
    None,
    MoveTo,
    Harvest,
    Load,
    Unload,
    Wait,
}

public enum ActionResult : byte
{
    InProgress,
    Done,
    Failed,
}

public sealed class VmIo
{
    public double SensorCargo;
    public ActionResult PendingActionResult = ActionResult.Done;
    public ActionKind RequestedAction = ActionKind.None;
    public double ActionX;
    public double ActionY;
    public double ActionZ;
    public double NearestResourceX;
    public double NearestResourceY;
    public double NearestResourceZ;
    public bool NearestResourceFound;
    public double NearestStorageX;
    public double NearestStorageY;
    public double NearestStorageZ;
    public bool NearestStorageFound;
}
