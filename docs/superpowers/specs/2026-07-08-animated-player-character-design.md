# Animated Player Character Design

**Linear issue:** TEO-14

## Goal

Replace the gray-box capsule with a mature stylized ranger that visibly idles, runs, jumps, and turns toward movement without changing the existing camera or build interaction paths.

## Asset choice

Use the free Standard editions of these Quaternius CC0 packs:

- Universal Base Characters
- Modular Character Outfits - Fantasy
- Universal Animation Library

The visible model is `Male_Ranger.gltf`. Its rig and the animation library use identical names for all 65 animated bones. The non-root-motion `UAL1_Standard.glb` supplies 43 animations. The runtime names are:

- Idle: `Idle_Loop`
- Run: `Jog_Fwd_Loop`
- Jump: `Jump_Loop`

A repository tool merges the donor animation accessors, buffer views, and channels into the ranger glTF by bone name. It fails if any animation target is missing. The resulting asset is deterministic and does not require Blender or runtime retargeting.

## Scene architecture

`game/scenes/Player.tscn` owns the player hierarchy:

```text
Player (CharacterBody3D, PlayerController)
|- CollisionShape3D
|- ModelRoot (Node3D)
|  `- Male_Ranger_Animated (imported glTF instance)
|     `- AnimationPlayer (imported with the glTF animations)
|- PlayerAnimator
`- Yaw
   `- SpringArm3D
      `- Camera3D
```

The `Player/Yaw/SpringArm3D/Camera3D` path and `Player` node name remain unchanged because `BuildController` depends on them.

`Main` loads and instantiates `Player.tscn`; it no longer builds the player in code.

## Runtime behavior

`PlayerController` exposes read-only `IsMoving` and `IsAirborne` state. Movement state updates after input direction is normalized. When moving, it smoothly rotates only `ModelRoot` toward the world-space movement direction; the body and camera yaw remain untouched.

`PlayerAnimator` converts the two booleans into one animation state. Airborne has priority over moving. It changes animation only when the selected state changes and blends over 0.15 seconds.

Missing nodes or animation names fail loudly during `_Ready()` with a clear Godot error and disable animation processing instead of producing repeated errors every frame.

## Testing and verification

- Unit-test animation selection: airborne -> jump, moving -> run, otherwise -> idle.
- Unit-test the glTF merge: all donor tracks map, expected animations exist, and rerunning produces identical bytes.
- Verify the scene contract contains the required player, camera, model, and animator nodes.
- Run `dotnet test` and `dotnet build`.
- Run Godot headless project import/startup when a Godot executable is available.
- Manually verify idle/run/jump blending, model-facing direction, unchanged camera behavior, and build interactions.

## Asset ledger

Copy the original CC0 license texts beside the shipped asset and add source, license, and usage rows to `CREDITS.md`. Only the ranger model, its six referenced textures, the merged animation data, and license texts ship; unused pack content remains outside the repository.

## Out of scope

- Combat or tool animations
- AnimationTree state machines
- Root motion
- Character customization UI
- Camera or build-system redesign
