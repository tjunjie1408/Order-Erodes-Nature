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
}
