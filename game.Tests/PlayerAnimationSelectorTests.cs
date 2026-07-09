public sealed class PlayerAnimationSelectorTests
{
    [Fact]
    public void Select_WhenGroundedAndStill_ReturnsIdle()
    {
        Assert.Equal("Idle_Loop", PlayerAnimationSelector.Select(false, false));
    }

    [Fact]
    public void Select_WhenGroundedAndMoving_ReturnsJog()
    {
        Assert.Equal("Jog_Fwd_Loop", PlayerAnimationSelector.Select(true, false));
    }

    [Fact]
    public void Select_WhenAirborne_ReturnsJump()
    {
        Assert.Equal("Jump_Loop", PlayerAnimationSelector.Select(false, true));
    }

    [Fact]
    public void Select_WhenMovingAndAirborne_PrioritizesJump()
    {
        Assert.Equal("Jump_Loop", PlayerAnimationSelector.Select(true, true));
    }
}
