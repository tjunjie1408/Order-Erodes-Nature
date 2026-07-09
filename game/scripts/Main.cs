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
        AddChild(GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate());
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

}
