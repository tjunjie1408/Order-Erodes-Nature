using Godot;

public partial class Main : Node3D
{
    private SimDriver _driver = null!;
    private Label _tickLabel = null!;

    public override void _Ready()
    {
        _driver = GetNode<SimDriver>("SimDriver");
        _tickLabel = GetNode<Label>("UI/TickLabel");
        BuildGroundSlab();
        AddSun();
        AddChild(new Camera3D
        {
            Position = new Vector3(0, 6, 12),
            RotationDegrees = new Vector3(-20, 0, 0),
            Current = true,
        });
        AddChild(BuildPlayer());
        AddChild(new BuildController { Name = "BuildController" });
        var crosshair = new Label { Text = "+" };
        crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        GetNode<CanvasLayer>("UI").AddChild(crosshair);
    }

    public override void _Process(double delta)
    {
        _tickLabel.Text = $"tick: {_driver.Sim.TickCount}";
    }

    private void BuildGroundSlab()
    {
        var body = new StaticBody3D { Name = "Ground" };
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) },
        };
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(100, 1, 100) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.5f, 0.42f),
            },
        };
        body.AddChild(shape);
        body.AddChild(mesh);
        body.Position = new Vector3(0, -0.5f, 0);
        AddChild(body);
    }

    private void AddSun()
    {
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -30, 0),
            ShadowEnabled = true,
        };
        AddChild(sun);
    }

    private static PlayerController BuildPlayer()
    {
        var player = new PlayerController { Name = "Player" };
        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
        });
        player.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.55f, 0.2f),
            },
        });
        var yaw = new Node3D { Name = "Yaw", Position = new Vector3(0, 1.5f, 0) };
        var arm = new SpringArm3D { Name = "SpringArm3D", SpringLength = 5.0f };
        arm.AddChild(new Camera3D { Name = "Camera3D", Current = true });
        yaw.AddChild(arm);
        player.AddChild(yaw);
        player.Position = new Vector3(0, 1.0f, 0);
        return player;
    }
}
