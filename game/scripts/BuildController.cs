using Godot;
using SimCore;
using System.Collections.Generic;

public partial class BuildController : Node3D
{
    private SimDriver _driver = null!;
    private CharacterBody3D _player = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _ghost = null!;
    private StandardMaterial3D _ghostOk = null!;
    private StandardMaterial3D _ghostBad = null!;
    private readonly Dictionary<int, MeshInstance3D> _views = new();
    private readonly HashSet<GridPos> _pendingCells = new();
    private GridPos? _aimedCell;
    private GridPos? _aimedExistingCell;

    private const float RayLength = 8.0f;

    public override void _Ready()
    {
        _driver = GetNode<SimDriver>("../SimDriver");
        _player = GetNode<CharacterBody3D>("../Player");
        _camera = GetNode<Camera3D>("../Player/Yaw/SpringArm3D/Camera3D");
        _driver.SimEventEmitted += OnSimEvent;

        _ghostOk = MakeGhostMaterial(new Color(0.3f, 0.7f, 1.0f, 0.4f));
        _ghostBad = MakeGhostMaterial(new Color(1.0f, 0.25f, 0.2f, 0.4f));
        _ghost = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One },
            MaterialOverride = _ghostOk,
            Visible = false,
        };
        AddChild(_ghost);
    }

    public override void _ExitTree() => _driver.SimEventEmitted -= OnSimEvent;

    private static StandardMaterial3D MakeGhostMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };

    public override void _PhysicsProcess(double delta)
    {
        UpdateAim();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } click)
            return;

        if (click.ButtonIndex == MouseButton.Left
            && _aimedCell is { } cell
            && CanPlaceLocally(cell))
        {
            _pendingCells.Add(cell);
            _driver.Sim.EnqueueCommand(new PlaceStructureCommand("base_block", cell));
        }

        if (click.ButtonIndex == MouseButton.Right
            && _aimedExistingCell is { } target
            && CanRemoveLocally(target))
        {
            _pendingCells.Add(target);
            _driver.Sim.EnqueueCommand(new RemoveStructureCommand(target));
        }
    }

    private bool CanPlaceLocally(GridPos cell) =>
        _driver.Sim.CanPlace(cell) && !_pendingCells.Contains(cell);

    private bool CanRemoveLocally(GridPos cell) =>
        !_driver.Sim.CanPlace(cell) && !_pendingCells.Contains(cell);

    private void UpdateAim()
    {
        var from = _camera.GlobalPosition;
        var to = from + -_camera.GlobalBasis.Z * RayLength;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (hit.Count == 0)
        {
            _ghost.Visible = false;
            _aimedCell = null;
            _aimedExistingCell = null;
            return;
        }

        var hitPosition = (Vector3)hit["position"];
        var normal = (Vector3)hit["normal"];
        _aimedCell = ToCell(hitPosition + normal * 0.5f);
        var inner = ToCell(hitPosition - normal * 0.5f);
        _aimedExistingCell = _driver.Sim.CanPlace(inner) ? null : inner;

        var cell = _aimedCell.Value;
        _ghost.Visible = true;
        _ghost.GlobalPosition = CellCenter(cell);
        _ghost.MaterialOverride = CanPlaceLocally(cell) ? _ghostOk : _ghostBad;
    }

    private static GridPos ToCell(Vector3 world) => new(
        Mathf.FloorToInt(world.X),
        Mathf.FloorToInt(world.Y),
        Mathf.FloorToInt(world.Z));

    private static Vector3 CellCenter(GridPos cell) =>
        new(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);

    private void OnSimEvent(SimEvent simEvent)
    {
        switch (simEvent)
        {
            case StructurePlaced placed:
                _pendingCells.Remove(placed.Position);
                AddStructureView(placed);
                break;

            case StructureRemoved removed:
                _pendingCells.Remove(removed.Position);
                if (_views.Remove(removed.StructureId, out var removedView))
                    removedView.QueueFree();
                break;

            case CommandRejected rejected:
                if (CommandPosition(rejected.Command) is { } rejectedPosition)
                    _pendingCells.Remove(rejectedPosition);
                break;
        }
    }

    private void AddStructureView(StructurePlaced placed)
    {
        var view = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.15f, 0.15f, 0.18f),
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.6f, 1.0f),
                EmissionEnergyMultiplier = 0.4f,
            },
        };
        AddChild(view);
        view.GlobalPosition = CellCenter(placed.Position);

        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = Vector3.One },
        });
        view.AddChild(body);
        _views[placed.StructureId] = view;
    }

    private static GridPos? CommandPosition(ICommand command) => command switch
    {
        PlaceStructureCommand place => place.Position,
        RemoveStructureCommand remove => remove.Position,
        _ => null,
    };
}
