public static class PlayerAnimationSelector
{
    public const string Idle = "Idle_Loop";
    public const string Jog = "Jog_Fwd_Loop";
    public const string Jump = "Jump_Loop";

    public static string Select(bool isMoving, bool isAirborne) =>
        isAirborne ? Jump : isMoving ? Jog : Idle;
}
