public sealed class PlayerSceneContractTests
{
    [Fact]
    public void PlayerScene_PreservesRequiredRuntimePaths()
    {
        var scene = File.ReadAllText(RepoFile("game", "scenes", "Player.tscn"));

        Assert.Contains("[node name=\"Player\" type=\"CharacterBody3D\"]", scene);
        Assert.Contains("[node name=\"ModelRoot\" type=\"Node3D\" parent=\".\"]", scene);
        Assert.Contains("[node name=\"Male_Ranger_Animated\" parent=\"ModelRoot\" instance=ExtResource(", scene);
        Assert.Contains("[node name=\"PlayerAnimator\" type=\"Node\" parent=\".\"]", scene);
        Assert.Contains("[node name=\"Yaw\" type=\"Node3D\" parent=\".\"]", scene);
        Assert.Contains("[node name=\"SpringArm3D\" type=\"SpringArm3D\" parent=\"Yaw\"]", scene);
        Assert.Contains("[node name=\"Camera3D\" type=\"Camera3D\" parent=\"Yaw/SpringArm3D\"]", scene);
    }

    [Fact]
    public void Main_LoadsPlayerScene_InsteadOfBuildingPlayerInCode()
    {
        var main = File.ReadAllText(RepoFile("game", "scripts", "Main.cs"));

        Assert.Contains("GD.Load<PackedScene>(\"res://scenes/Player.tscn\").Instantiate()", main);
        Assert.DoesNotContain("BuildPlayer(", main);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dev_game.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
