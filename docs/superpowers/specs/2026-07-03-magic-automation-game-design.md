# *Order Erodes Nature* (working title) Game Design Document

Date: 2026-07-03
Status: approved via brainstorming review, pending implementation plans

## 1. Project Background and Constraints

- Developer: a student with real product development experience, developing in spare time, with OpenAI Codex as the primary implementation assistant
- Nature: long-term hobby project (1-2 years of iteration), no hard deadline, ultimate goal is a complete playable game
- Asset budget: on the order of a hundred (US dollars), while also willing to self-learn Blender modeling and shader writing
- Platform: Windows desktop (Godot cross-platform export incidentally supports Mac/Linux)
- Multiplayer: strictly single-player, architecture makes no provisions for multiplayer
- Narrative: no narrative, pure gameplay-driven; atmosphere conveyed through art language

## 2. Game Concept

### One-Sentence Concept

The player is a builder who, on a warm low-poly natural continent, uses cold geometric megastructures to establish a fully automated production system; each machine's behavior is defined by the player through node-graph wiring (rune circuits).

### Genre Positioning

3D simulation building + automation factory + programming gameplay. Reference points: the Minecraft Create mod (multi-block assembly), Factorio (production chains and signals), The Farmer Was Replaced (programming units to do work), UE Blueprint (node-programming interaction paradigm).

### Art Theme: Order Erodes Nature

- World: an organic, warm, stylized low-poly natural continent (fixed terrain, not diggable)
- Player creations: cold geometric megastructures — obsidian monoliths, floating white polyhedra, sharp energy conduits; no mechanical meshing, everything operates via magnetic levitation
- The contrast is the theme: as the automation system expands, cold-logic geometry spreads across the natural continent — the picture itself carries the expression, no text needed
- Logic errors are presented as "rational collapse": data flows twist into chaotic fractals, garbled noise surfaces on geometric bodies — both an aesthetic spectacle and debug feedback
- Atmosphere mechanics (concepts adopted; landed in batches starting from M4):
  - During infinite loops / logic deadlocks, the space around the crashed structure undergoes gravitational-lensing-style distortion (shader, M4). **Implementation red line**: screen-space distortion must NOT attach a material per entity (30 machines crashing simultaneously = an Overdraw disaster of 30 layers of full-screen sampling) — a single global post-processing pass, with crash coordinates passed in via a `uniform vec3[]` array (capped at 16 entries, truncated after sorting by distance to camera), computing all lens effects in one full-screen pass
  - Natural vegetation around crashed structures withers and desertifies — rational collapse corrupts the very nature it depends on (post-MVP, goes to backlog)
  - The unlock interaction for truth shards is not a traditional tech-tree panel but a huge, cold core matrix: the player slots shards into it, with only a crisp mechanical click sound, after which a region is "formatted" into a sterile absolute grid (post-MVP, goes to backlog; MVP uses a simple placeholder panel)

### Core Loop

Explore terrain to discover resource nodes → write circuits for golems (harvesting/hauling) → assemble fixed structures to solidify production lines → hook into mana springs for power → produce truth shards to unlock stronger modules and nodes → use the signal system to coordinate multi-golem fleets → scale up and face more complex logic challenges.

### Camera and Character

Third-person real character (can walk, jump), with an interaction ray aimed at structures to open panels / the circuit editor. No first-person digging, no voxel terrain.

**Character-vs-simulation-entity collision convention**: the character runs at Godot's physics frame rate, golems run at the 20tps simulation — the two frequencies differ, so no real rigid-body interaction (otherwise jitter/clipping is unsolvable). Golems and machines carry a kinematic collision body in the presentation layer, whose position follows the interpolated visual position each frame; it is used only to block the player's walking and participates in no simulation computation whatsoever; all character interaction with machines goes through ray casting.

## 3. Technology Choices

| Item | Decision | Rationale |
|---|---|---|
| Engine | Godot 4.x (.NET edition) | GraphEdit is a runtime UI control — a player-usable node editor out of the box (Unity's GraphView lives in the UnityEditor namespace and cannot be packaged into the game); free and open source; scenes are text format, friendly to AI collaboration |
| Language | C# | Simulation performance is sufficient to support tens of thousands of entities; Codex is extremely familiar with the C# corpus; a pure C# class library can be tested independently of the engine |
| Rejected | Unity (a runtime node editor would have to be built from scratch), Unreal (slow iteration, high AI-assistance error rate, mismatched with a stylized single-player project) | |

## 4. Architecture

### Core Principle: Separation of Simulation and Presentation

The game's "truth" lives in an engine-independent pure C# simulation core (SimCore); Godot is responsible only for rendering and input. Benefits: logic can be unit-tested headless (Codex self-closed verification loop), performance is controllable, rendering can be refactored independently.

### Solution Structure

```
dev_game.sln
├── game/          Godot project (scenes, shaders, presentation-layer scripts, references SimCore)
├── SimCore/       Pure C# class library (net8.0, importing Godot is forbidden)
└── SimCore.Tests/ xUnit test project
```

### Six-Module Breakdown

1. **SimCore (simulation core)**: fixed-timestep tick (20tps) driving machine state machines, energy networks, item flow, recipe processing, and the circuit virtual machine. Takes commands as input, outputs state and events. The focus of unit test coverage.
2. **Rune circuit system**: the data model and virtual machine live in SimCore; the editing interface is a customized GraphEdit that only edits the graph and submits it. GraphEdit is a pure UI container and understands no logic — a **circuit compiler** must sit in between: the editor serializes the UI topology into a pure-data graph description (node list + connection list), and the compiler (living inside SimCore, unit-testable) validates it and produces a flat instruction array. Validation must be strict: cycles in execution wires, type mismatches on data wires, required pins left dangling, unreachable nodes — all must return an error list with node locations, which the editor uses to highlight nodes in red; inscription is not allowed unless validation passes.
3. **World and building**: fixed terrain scene, grid-based placement (ghost preview, snapping, legality validation), resource node distribution. Building operations generate commands sent to SimCore.
4. **Character**: CharacterBody3D third-person controller + interaction ray.
5. **Presentation layer**: listens to SimCore state and events; drives models, shader parameters, particles, sound effects; inter-tick interpolation keeps visuals silky; renders the glitch aesthetics.
6. **Save system**: serializes SimCore state (JSON to start, switch to binary once scale grows). Saves contain not just building positions but also all running VM state — see the serializability convention in "Circuit Execution Model" (everything is trivial value types; circuit compilation artifacts do not go into saves).

### Data Flow (One-Way)

Player input → command objects enqueued → SimCore consumes commands at tick boundaries and computes → state changes and events → presentation layer reads and renders. The presentation layer never writes simulation state directly.

**Exception clarification — high-frequency interactions front-loaded to the presentation layer**: interactions that need instant response, such as build preview (ghost buildings), grid snapping, and ray casting, are fully computed up front by the presentation layer at render frame rate, without waiting for SimCore. Only at the instant the player confirms the build is a `TryPlaceStructureCommand` sent; after SimCore validates it, it emits a `StructurePlacedEvent`, and the presentation layer then solidifies the ghost building into an official rendered mesh. The legality-check logic used by the preview shares the same set of pure functions with SimCore (placed inside SimCore, called directly by the presentation layer for read-only validation), avoiding drift between two rule sets.

**Client-side prediction cache (sealing the 60fps-interaction vs 20tps-confirmation race)**: between a command being issued and its tick confirmation there is a window of up to 50ms; rapid clicking / fast dragging would issue duplicate commands for the same cell and cause ghost flicker. The BuildController maintains a pending cell set: the instant a place/remove command is issued, the cell is marked pending (preview and clicks treat it as occupied/removed), and it is released upon receiving the corresponding `StructurePlaced`/`StructureRemoved`/`CommandRejected` event. Feedback stays "mechanical-keyboard-crisp" and no duplicate-command noise is generated.

### Grounding Mechanisms

- **Fixed tick**: a time accumulator in the main loop; once 50ms has accumulated, call `SimCore.Tick()` once, decoupling simulation from frame rate
- **Interpolation**: SimCore keeps two copies of positions — previous tick and current tick; the presentation layer linearly interpolates by frame-time ratio
- **Events**: SimCore pushes into an event list; the presentation layer drains it each frame and plays the presentation

### Performance Design

- Machine data stored in arrays/structs; the tick is a flat traversal; the hot path is zero-allocation (no LINQ, no new) to prevent GC stutter
- Circuits are compiled at edit-completion time into flat instruction arrays with jumps; the runtime does not interpret object graphs
- Connectivity topology for energy networks and multi-block structures is synchronously recomputed only once, in the tick where a build/removal happens, and never enters the per-tick hot path: removing a block uses local BFS/flood fill to determine whether the structure splits (one machine splitting into two), scanning only locally outward from the affected cells rather than the whole graph; structures have a maximum size cap (on the order of a few dozen cells), keeping the worst case controllable
- Rendering bottleneck contingency: machines are not attached to the scene tree; later use MultiMesh instancing for batched drawing
- Scale expectation: tens of thousands of machines at 20tps with no pressure; the long-term bottleneck is rendering, not simulation
- **C# hidden-allocation blacklist** (zero-allocation killers the AI is unaware of; a code review focus): foreach over interface types (`IEnumerable<T>`/`IReadOnlyCollection<T>`) boxes the enumerator — the hot path iterates only fields of concrete types; casting a struct to an interface is boxing — commands/events stay as class records or avoid interface casts; string interpolation and log concatenation are forbidden on the hot path. Each month-end acceptance includes one manual scan: sweep the `Simulation.Tick()` call tree with Rider Heap Allocation Viewer (or observe GC frequency with dotnet-counters)
- **Floating-point determinism stance**: System.Text.Json uses the shortest round-trippable format for float/double, so save/load round-trips are bit-exact — lock this behavior down with exact round-trip tests using adversarial values (0.1, 1.0/3.0, 1e-17); **decimal truncation of floats going into saves is forbidden** (truncation = every save mutates simulation state, and drift accumulates across save/load cycles). If cross-machine save sharing / replay-grade determinism is ever needed, the contingency is switching the simulation layer to fixed-point (int, thousandths); not done within the current single-player scope
- **Numeric type unification**: within SimCore, floating point is uniformly `double` (SimVec3, VM registers, VmIo, sensor values), eliminating internal float↔double conversion points (C# literals default to double; mixing them is a breeding ground for implicit conversions and precision confusion). Conversion to Godot's `Vector3` (float) happens only at the presentation-layer boundary, and presentation-layer data never flows back into the simulation, so it is harmless
- **Sensor cost model**: the VM's ReadSensor instruction merely copies a register value from VmIo; expensive spatial queries are executed once by the host when filling VmIo each tick. The compiler produces a **sensor usage mask** in the CompiledCircuit, so the host computes only the sensors the program actually uses; unused expensive queries are skipped outright

### Error-Handling Philosophy

Logic errors in circuits (infinite loops, type mismatches, insufficient energy) are game content: the machine enters a crashed state, visuals present the glitch aesthetics, and the circuit panel highlights the failing node and reason in red. Bugs in the simulation core itself are caught by unit tests.

## 5. Gameplay Systems

### Unified Programmability Rule

The `inscription slot` is a module; any structure equipped with one is programmable. A golem = a structure equipped with a hover core — in essence "a machine that can move". One rule set for the whole game: **module assembly determines capability, circuits determine behavior**.

### Multi-Block Module Assembly

Machines are assembled by the player in the world from functional blocks: a hovering base module + attached modules (harvest prism, capacity crystal, amplifier rune, inscription slot, hover core, etc.). A connected structure counts as one machine in the simulation, with capabilities and stats being the sum of its modules. No whole-structure motion physics (Besiege-style rejected).

### Energy System (Mana)

- Mana springs: sparsely distributed across the terrain, fixed output per tick — the motivation for territorial expansion
- Energy conduits: machines connect into the network via conduits; the same connected network aggregates supply and demand each tick; when demand exceeds supply, everything slows down proportionally by the supply/demand ratio (no blackout shutdowns; visual feedback is glow dimming and rotation slowing)
- Extension hooks (not in MVP): distance attenuation, mana frequencies, environmental effects

### Golems and the Opening

Within the first minute of the game the player already owns an initial golem (pre-fitted with a hover core + harvesting arm + inscription slot) and basic nodes. The tutorial is writing the first circuit. Programming is the game's verb, not a reward. Manual harvesting exists only in the opening tutorial.

### Circuit Execution Model: Hybrid Execution Flow + Data Flow (Blueprint-Style)

- Two kinds of wires: execution wires (define action order) and data wires (feed sensor readings and computation results into node parameters)
- Entry points are event nodes: `On Start` (the program body, naturally looping), `On Signal Received`, `Every N Ticks`
- **Suspendable interpreter**: time-consuming actions (moving, harvesting) suspend the program counter until completion — a coroutine-style micro virtual machine (compiled into an instruction array with jumps + one program counter per machine)
- **Runtime read-only iron rule**: a running circuit is a read-only flat instruction stream. When the player edits the circuit and re-inscribes it, or when the structure's modules change (removal/damage), the virtual machine is always force-Reset — program counter and local state cleared, execution restarting from `On Start`. Hot-patching an executing stack is forbidden
- **VM state is serializable**: the compilation artifact (instruction array) is statically bound to a blueprint ID and does not go into saves; each machine's runtime state contains only trivial values — `BlueprintID + ProgramCounter + WaitTicksRemaining + local variable table (value types) + current target data`. C# delegates, lambdas, reference callbacks, or any other non-serializable objects are forbidden in VM state
- The currently executing node glows highlighted in the editor — runtime visualization is a free debug tool
- MVP node set of about 15: events (On Start / On Signal Received / Every N Ticks), sensing (read inventory, detect, self state, **find nearest resource node / storage obelisk — outputs a Vector**), computation (compare, AND/OR/NOT, counter, constant), actions (move to, harvest, load, unload, start/stop, send signal, wait), flow control (conditional branch)
- **Relative addressing principle**: the primary addressing mode of circuits is "sensor outputs a coordinate → fed into an action node" (e.g. `Find Nearest Resource Node → Move To`); the player should not manually enter absolute coordinates in the 3D world; coordinate picking is a fallback only. This guarantees circuits automatically seek the next target when a resource is depleted, and circuits stay valid when a production line is translated
- Later extension: sub-circuits encapsulated as custom nodes (function-ization)

### Multi-Golem Cooperation

- **Rune blueprint library**: circuits are saved as blueprints and can be inscribed onto any number of golems (write once, deploy a fleet)
- **Blueprint updates are lazy and safe** (the fleet-level refinement of the reset iron rule): direct inscription onto a single machine = immediate Reset (the editor's explicit intent); but when **editing a blueprint**, its 50 referencing machines must not all instantly Reset at once (transport golems in mid-air would freeze; those carrying precious cargo would re-run the initial logic while still holding the goods) — referencing machines are only marked `PendingUpdate`, and silently switch to the new instruction stream when the PC returns to the program entry point (the natural loop boundary); crashed or manually Reset machines switch at the moment of reset
- **Signal system**: beacon crystals broadcast named signal channels; the circuits of machines and golems can read and write them; sensor runes / trigger crystals are the fixed structures that read/write signals
- **Signal semantics are latched**: once written, a channel's value persists until overwritten or explicitly zeroed by a `Clear Signal` node — it does not vanish because the writer stops emitting. Rationale: the VM is coroutine-suspendable — a golem executing a 20-tick harvest cannot keep emitting; if signals required per-tick maintenance they would cliff-drop into nothing. Latch + explicit clear preserves the "absolute sense of segmentation". Multiple writers in the same tick take the maximum value ("strongest signal wins"); writes at tick N become readable at N+1 (double-buffered for determinism); sensor-rune-type fixed structures rewrite their own channel every tick
- **Core limit**: the number of simultaneously active golems is constrained by a cap, raised with truth shards (a progression hook + protection for performance and cognitive load)

### Progression: Unlock Tree

Producing higher-tier items yields **truth shards**; spending shards unlocks. Each tier answers a pain point the player has just developed:

- Tier 0 (start): one golem, basic modules, basic nodes
- Tier 1: fixed structures (base module / harvest prism / capacity crystal), hover rails
- Tier 2: transmutation circle recipes, computation nodes, amplifier modules
- Tier 3: signal system (cooperation unlocked)
- Tier 4: blueprint library, core limit, advanced modules

### Items and Recipes (MVP Minimal Set)

2 raw materials (glimstone, aether dust) → 3 processed goods → 1 truth shard. Content-volume expansion is iteration work after M5.

## 6. Art and Audio Asset Strategy

### Source Division

- **Natural continent**: CC0 low-poly kits from Kenney, Quaternius, KayKit; textures/skyboxes from Poly Haven (CC0)
- **Geometric megastructures**: self-made (Blender learning project) — basic geometric primitives + bevels + emissive materials, each module within a few hundred faces; modeling discipline: cubes/cylinders/cones + bevels only, organic curved surfaces forbidden
- **Character and animation**: Synty POLYGON packs (the main budget destination) or KayKit free characters; Mixamo auto-rigging and animation. Golems use no skeletal animation — procedural animation (rotation/floating/scaling) conveys the sense of life
- **Shaders/VFX (self-developed; where the visual ceiling lies)**: emissive outlines, Fresnel rim light, flowing runes, energy beams, glitch noise; reference godotshaders.com; post-processing via WorldEnvironment (Glow / volumetric fog / tone mapping)
- **Sound effects**: Sonniss GDC free packs, Freesound (filtered for CC0), Kenney sound packs. Machine sound = low-frequency hum + crystal overtones; UI = mechanical-keyboard-crisp feedback
- **Music**: MVP uses ambient sound as a bed; music purchased later (itch.io ambient packs)

### Unified Discipline

1. Lock the palette: nature uses warm gray-green-brown, ≤8 colors; megastructures use black/white/gray + a single cold accent color (ice blue) + a single warning color (crimson); all external assets are recolored
2. Unified material pipeline: all models go through the same base material (flat shading / a single gradient texture); external assets get their textures scrubbed on entry
3. License discipline: accept only CC0 and explicitly commercially usable assets; maintain a `CREDITS.md` recording source and license entry by entry; avoid NC clauses

## 7. Codex Collaboration Workflow

### Repository Infrastructure (Day One)

- git repository + `AGENTS.md` (Codex's project handbook): architecture iron rules (SimCore must not import Godot, the presentation layer must not write simulation state, no allocations on the hot path, SimCore subsystems must not reference each other directly), project structure map, test commands, code style, "for uncertain Godot APIs, consult official docs first"

### Interface Isolation Inside SimCore (Designed for AI Context)

SimCore will keep growing (circuit VM, energy network, item flow, building/topology), while the AI's context window has a physical limit. Therefore subsystems are **forbidden from referencing each other directly** — the energy network does not know the circuit VM exists, and vice versa; interaction goes only through two channels: the simulation-internal event bus, or pure data structures (e.g. a "power satisfaction ratio per network this tick" table). Benefit: when assigning Codex a task, only a single subsystem's code + the interaction data contract needs to be fed in, preventing it from "conjuring up" another subsystem's API when it cannot read all the code; a byproduct is that each subsystem can be unit-tested independently. This iron rule goes into AGENTS.md; violations are rejected in review.
- Design documents and implementation plans live in `docs/`; tasks reference document sections

### Task Cadence

Small and verifiable, one thing in one module at a time. SimCore uses TDD: Codex writes tests → implements → `dotnet test` self-verification closed loop. The presentation layer (scenes/shaders/feel) is accepted by the developer personally playing the game.

**Interface-oriented prompting (guarding against context pollution and hallucinated refactors)**: as SimCore grows, tasks given to Codex are fed only "the current subsystem's code + the adjacent subsystems' interface contract files (VmIo, event definitions, data table structures)", **never the adjacent subsystems' implementation source**. Treat the AI as an outsourced programmer who can only see API docs — when the context is stuffed with the entire Simulation.cs, the LLM will invent nonexistent APIs, quietly alter established interfaces, and sneak LINQ in to break the zero-allocation discipline. During review, accept against the interface contracts; any diff to an interface file is a red light.

### Division of Labor

- Developer: architecture decisions, task breakdown, code review (focused on module boundaries), playtest acceptance, art direction, the hands-on Blender/shader parts. **Special note: Codex is good at writing shader algorithms but bad at tuning visual parameters — numeric tuning of Glow intensity, distortion amplitude, color curves, etc. must be done by the developer personally**
- Codex: SimCore logic and tests, UI code, presentation-layer bindings, shader first drafts, tooling scripts
- Review red lines: cross-module refactors, adding third-party dependencies, and changing architecture conventions all require the developer's approval

### AI-Friendly Engineering Habits

Scene files small and numerous (one scene per module); one commit per task; roll back immediately when something breaks.

## 8. Milestones and MVP Definition

| Milestone | Content | Estimate |
|---|---|---|
| M0 Skeleton | solution structure, SimCore tick/commands/events + first batch of tests, **save round-trip test (serialize → deserialize → identical state, locked down in the very first week)**, AGENTS.md, empty Godot scene wired up | 1-2 weeks |
| M1 Walking slice | hand-made terrain, third-person character (Mixamo), grid-snapped placement/removal | 2-3 weeks |
| M2 Golem and minimal circuit | initial golem, minimal GraphEdit editor, suspendable interpreter + 7 nodes, resource nodes, storage obelisk. **The core-experience validation point** | 4-6 weeks |
| M3 Production system | multi-block assembly, energy network, hover rails, transmutation circle, unlock tree prototype | 4-6 weeks |
| M4 Presentation upgrade | cold-glow shader suite, glitch aesthetics (including the spatial-distortion shader for infinite loops/deadlocks), sound effects, UI theme, post-processing | 3-4 weeks |
| M5 Full MVP | saves, signal system, blueprint library, content fill-out (about 10 modules / 15 nodes / 3 unlock tiers) | 3-4 weeks |

**MVP completion definition**: a new player, with no guidance, can complete — write a harvesting program for a golem → assemble the first automated production line → use signals to make two golems cooperate → unlock at least one tech tier → save and load to continue the game.

M2 is deliberately front-loaded: the programmable golem is the judgment point for whether the whole game holds up — the highest risk verified earliest.

## 9. Testing Strategy

- **SimCore**: xUnit unit tests are the main battlefield — energy solving, the circuit virtual machine (including suspend/resume), item flow, recipes, save round-trip (serialize → deserialize → identical state)
- **Determinism**: the same command sequence must produce the same state (backing regression tests and save consistency)
- **Presentation layer**: not automated; accepted via developer playtesting
- **Regression habit**: every bug fix first adds a test reproducing that bug
- **Distrust-the-graph-algorithm principle**: any graph algorithm the AI writes in one shot (connected components, BFS split detection, topological sort) counts as done only after its boundaries are locked down with vicious test cases — removing one edge of a ring structure does not split it (re-entry marking), removing a bridge splits one into three, self-loops / parallel edges. Tests first, implementation after
- **Floating-point round-trip exactness**: save tests must include bit-exact round-trip cases with adversarial float values (0.1, 1.0/3.0, 1e-17)

## 10. Risk List (Ordered by Lethality)

1. **Scope creep**: milestone discipline; ideas outside the current milestone go into `docs/backlog.md` and are not implemented; every milestone must be playable
2. **Circuit editor interaction as a bottomless pit**: M2 aims only for usable; polish belongs to M4; interaction copies Blueprint verbatim, no freestyle improvisation
3. **Godot C# corpus weaker than Unity's**: AGENTS.md requires Codex to consult official docs for uncertain APIs; domains with repeated errors get their tone set by the developer reading the docs
4. **Art cohesion spiraling out of control**: palette discipline + assets scrubbed on entry
5. **Enthusiasm decay**: every milestone produces a showable result; the most fun part, M2, is front-loaded
