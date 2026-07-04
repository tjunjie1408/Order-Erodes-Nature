# Implementation Plan Index

Spec source: [../specs/2026-07-03-magic-automation-game-design.md](../specs/2026-07-03-magic-automation-game-design.md)

| Month | Milestone | Plan | Core Deliverables |
|---|---|---|---|
| 1 | M0 + first half of M1 | [month1-skeleton-and-walking-slice](2026-07-03-month1-skeleton-and-walking-slice.md) | SimCore skeleton, save round-trip, gray-box character and grid building |
| 2 | M1 wrap-up + first half of M2 | [month2-character-and-circuit-compiler](2026-07-03-month2-character-and-circuit-compiler.md) | Animated character, circuit graph model and compiler |
| 3 | M2 wrap-up | [month3-vm-golem-and-editor](2026-07-03-month3-vm-golem-and-editor.md) | Suspendable VM, golem, GraphEdit editor, end-to-end harvesting |
| 4 | M3 | [month4-assembly-energy-production](2026-07-03-month4-assembly-energy-production.md) | Multi-block assembly, energy network, rails, transmutation circles, unlock tree |
| 5 | M4 | [month5-presentation-upgrade](2026-07-03-month5-presentation-upgrade.md) | Shader suite, glitch aesthetics, sound effects, UI theme |
| 6 | First half of M5 | [month6-saves-and-signals](2026-07-03-month6-saves-and-signals.md) | Save persistence to disk, signal system, multi-golem cooperation |
| 7 | M5 wrap-up | [month7-blueprints-content-mvp](2026-07-03-month7-blueprints-content-mvp.md) | Blueprint library, content fill-out, tutorial, MVP acceptance |

## Usage Rules

1. **Execute month by month**: Only execute the current month's plan. See each plan's header for the execution method (Codex / subagents both work).
2. **Pre-start calibration**: From month 2 onward, the first step at the start of each month is to verify that plan's Interfaces and file paths against the actual code in the repository. Interface drift → revise the plan first, then start work; **monthly goals and acceptance criteria must not be downgraded**. Step granularity gets coarser toward the later months; during calibration, fill in step-level code according to the interface contracts and test checklists within the plan.
3. **Scope discipline**: Ideas outside the plan go into `docs/backlog.md`; do not implement them.
4. **Month end**: Run that plan's "End-of-month definition of done" checklist, plus two fixed rituals:
   - **Allocation scan**: Use Rider Heap Allocation Viewer (or dotnet-counters to watch GC frequency) to manually walk the `Simulation.Tick()` call tree, catching hidden allocations left by AI (interface enumerator boxing, struct-to-interface boxing, string interpolation)
   - **Boundary scan**: `grep -r "using Godot" SimCore/` returns empty; interface contract files have no unapproved diff compared with the previous month

   Only proceed to the next month once everything passes.
5. **Feeding discipline (mandatory from month 4 onward)**: Tasks given to Codex contain only the current subsystem's code + adjacent subsystems' interface contracts, and never adjacent subsystems' implementation source code — to prevent context pollution that leads to hallucinated APIs and unauthorized interface changes.
