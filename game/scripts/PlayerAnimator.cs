using System;
using Godot;

public partial class PlayerAnimator : Node
{
    private const float BlendTime = 0.15f;

    private PlayerController _player = null!;
    private AnimationPlayer _animationPlayer = null!;
    private string _currentAnimation = string.Empty;

    public override void _Ready()
    {
        _player = GetParent<PlayerController>();
        _animationPlayer = _player.GetNode<Node3D>("ModelRoot")
            .FindChild("AnimationPlayer", true, false) as AnimationPlayer
            ?? throw new InvalidOperationException(
                "The imported player model must contain an AnimationPlayer.");

        ValidateAnimation(PlayerAnimationSelector.Idle);
        ValidateAnimation(PlayerAnimationSelector.Jog);
        ValidateAnimation(PlayerAnimationSelector.Jump);
    }

    public override void _Process(double delta)
    {
        var nextAnimation = PlayerAnimationSelector.Select(
            _player.IsMoving, _player.IsAirborne);
        if (nextAnimation == _currentAnimation)
            return;

        _animationPlayer.Play(nextAnimation, BlendTime);
        _currentAnimation = nextAnimation;
    }

    private void ValidateAnimation(string animationName)
    {
        if (!_animationPlayer.HasAnimation(animationName))
            throw new InvalidOperationException(
                $"The imported player model is missing animation '{animationName}'.");
    }
}
