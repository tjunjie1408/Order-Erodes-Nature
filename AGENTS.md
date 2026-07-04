# Repository Guidelines

## Project Structure & Architecture

`SimCore/` is the pure .NET 8 simulation library; `SimCore.Tests/` contains its xUnit tests. Design specifications and staged implementation plans live under `docs/superpowers/`. `docs/backlog.md` is the parking lot for ideas outside the active milestone. The planned `game/` directory is the Godot presentation layer.

Keep these boundaries strict:

- `SimCore` must never import Godot. Godot code sends commands and reads simulation state/events; it never mutates simulation state directly.
- SimCore subsystems communicate only through events or pure data contracts, not direct subsystem references.
- Commands are consumed at fixed 20 Hz tick boundaries.
- `Simulation.Tick()` and its call chain must not allocate: avoid LINQ, temporary collections, interface-enumerator or struct boxing, and interpolated/log strings.
- Save DTOs contain only trivial values. Do not store delegates, lambdas, callbacks, or compiled circuit artifacts. Preserve floating-point values without decimal truncation.

## Build, Test, and Development Commands

- `dotnet restore` — restore solution dependencies.
- `dotnet build` — compile all projects in `dev_game.sln`.
- `dotnet test` — run the complete xUnit suite; required after every SimCore change.
- `godot --path game` — run the Godot project once `game/` exists; use Godot 4.4+ .NET edition.

## Coding Style & Naming Conventions

Use four-space indentation and standard C# naming: `PascalCase` for types and public members, `_camelCase` for private fields. Prefer file-scoped namespaces and one focused type per file. Keep scenes small, with one module per scene. Consult stable official Godot documentation before relying on uncertain APIs.

## Testing Guidelines

Develop SimCore with TDD: write a failing xUnit test, implement the minimum behavior, then rerun `dotnet test`. Name tests by behavior, such as `GridPos_ValueEquality` or `PlaceOnOccupiedCell_EmitsCommandRejected`. Add deterministic round-trip tests for persisted state.

## Commit & Pull Request Guidelines

Use focused conventional commits, matching history: `docs: ...`, `chore: ...`, `feat(sim): ...`, or `test(sim): ...`. Make one commit per plan task. Never add a `Co-Authored-By` trailer. Pull requests should identify the Linear issue, summarize architecture impact, report automated and manual verification, and include screenshots for visual Godot changes.

## Assets and Scope

Record every external asset in `CREDITS.md`. Accept only CC0 or explicitly commercially usable licenses; reject non-commercial restrictions. Put unplanned ideas in `docs/backlog.md` instead of implementing them.
