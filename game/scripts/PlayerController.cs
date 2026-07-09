using Godot;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float Speed = 6.0f;
    [Export] public float JumpVelocity = 4.8f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float TurnSpeed = 12.0f;

    public bool IsMoving { get; private set; }
    public bool IsAirborne => !IsOnFloor();

    private Node3D _yaw = null!;
    private SpringArm3D _arm = null!;
    private Node3D _modelRoot = null!;
    private float _pitch;

    public override void _Ready()
    {
        _yaw = GetNode<Node3D>("Yaw");
        _arm = GetNode<SpringArm3D>("Yaw/SpringArm3D");
        _modelRoot = GetNode<Node3D>("ModelRoot");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw.RotateY(-motion.Relative.X * MouseSensitivity);
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity, -1.2f, 0.5f);
            _arm.Rotation = new Vector3(_pitch, 0, 0);
        }
        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= 9.8f * (float)delta;
        else if (Input.IsPhysicalKeyPressed(Key.Space))
            velocity.Y = JumpVelocity;

        var input = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1;

        var direction = _yaw.GlobalBasis * new Vector3(input.X, 0, input.Y);
        direction.Y = 0;
        direction = direction.Normalized();
        IsMoving = !direction.IsZeroApprox();

        if (IsMoving)
        {
            var targetYaw = Mathf.Atan2(direction.X, direction.Z);
            var turnWeight = 1.0f - Mathf.Exp(-TurnSpeed * (float)delta);
            var rotation = _modelRoot.Rotation;
            rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, turnWeight);
            _modelRoot.Rotation = rotation;
        }

        velocity.X = direction.X * Speed;
        velocity.Z = direction.Z * Speed;
        Velocity = velocity;
        MoveAndSlide();
    }
}
