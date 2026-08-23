# MonoRogue Agents Guide

This document describes how automated agents (AI or CI) should interact with the MonoRogue repository. It supersedes any older `agent.md` guidance.

## What this document covers
- Purpose and scope for agents interacting with this repository
- Solution structure and project layout
- Architecture and separation of concerns
- Recommended workflow for making changes (development loop)
- Coding style and testing requirements
- Build troubleshooting (duplicate assembly attribute errors and other common issues)

## Purpose and scope
- Agents should assist developers with small, well-scoped changes, creating or updating tests, fixing build errors, and suggesting architectural improvements.
- Avoid large, sweeping refactors unless explicitly requested by a human reviewer.
- Keep content data and runtime logic separated: JSON-driven content definitions live in the `MonoRogue.Data` namespace/folder, gameplay and ECS logic in `MonoRogue.Core`, and terminal/UI-facing code in `MonoRogue.UI`.

## Solution structure
The solution is `MonoRogue.slnx` (XML solution format). It contains exactly two projects:

- `MyGame\MyGame.csproj` — the single executable project (`OutputType=WinExe`, `TargetFramework=net10.0`). It compiles three source folders (not separate assemblies): `MonoRogue.Core`, `MonoRogue.Data`, and `MonoRogue.UI`. `RootNamespace=MonoRogue`.
- `Tests\MonoRogue.Tests.csproj` — an xUnit test project referencing `MyGame.csproj`. Run with `dotnet test`.

Packages referenced by `MyGame.csproj`: `Arch` (ECS), `MonoGame.Framework.DesktopGL`, `RogueSharp`, `SadConsole.Host.MonoGame`, and `SadConsole.Extended`. (Note: `RogueSharp` is referenced but not currently used in source.)

### Directory layout
- `MyGame\Program.cs` — app entry point and SadConsole host setup (no `--test-content` flag anymore).
- `MyGame\MonoRogue.Core\` — core gameplay, map generation, ECS components and systems.
  - `GameMain.cs` — top-level game state machine (`GameState`, `InputType`); intentionally decoupled from SadConsole.
  - `GameConstants.cs` — shared tuning constants.
  - `Components.cs` — ECS component definitions and small records (e.g. `RenderCell`, `MonsterBehavior`).
  - `GameSession.cs` — the thin orchestrator that owns the `World`, composes all systems, and handles map generation, turn ordering, inventory, and persistence.
  - `SpatialMap.cs` — spatial index/lookup helper.
  - `SerializationDTOs.cs` — save/load DTOs (`EntityDTO`, `EffectDTO`, `MapData` with a versioned format).
  - `MapPersistenceHelpers.cs` — helpers shared by save/load logic.
  - `InputCommand.cs` / `IInputProvider.cs` — input abstraction.
  - `Systems\` — ECS systems (one responsibility each):
    - `EnergySystem.cs`
    - `CombatSystem.cs`
    - `EffectSystem.cs`
    - `PlayerActionSystem.cs`
    - `MonsterAISystem.cs`
- `MyGame\MonoRogue.Data\` — JSON-backed content definitions and loader utilities. Responsible for monster/item content:
  - `ContentLoader.cs`, `MonsterDataLoader.cs`, `ItemDataLoader.cs`
- `MyGame\MonoRogue.UI\` — terminal/UI-facing code:
  - `RootScreen.cs`, `SadConsoleInputProvider.cs`, `GameSettings.cs`, `ColorConverter.cs`
- `Data\` (repo root) — runtime content folder: `monsters.json`, `items.json`.
- `Tests\` — `MonoRogue.Tests.csproj` and `MonsterDataLoaderTests.cs`.

## Architecture and separation of concerns
- `GameSession` is an orchestrator, not a god class. Gameplay behavior is owned by focused systems under `MonoRogue.Core.Systems`, composed inside `GameSession`.
- ECS is Arch (`World`, `Entity`, components, `QueryDescription`). `GameSession` uses a stable `World` reference created in its constructor and disposed in `Dispose()`.
- System dependencies are intentionally acyclic:
  - `CombatSystem` depends on `EffectSystem`.
  - `MonsterAISystem` depends on `CombatSystem` and `SpatialMap`.
  - `PlayerActionSystem` depends on `SpatialMap`.
  - `EffectSystem` accepts a `Func<Entity,int,int>` (`applyDamage`) callback for poison ticks so it does not form a cycle with `CombatSystem`.
- Monster AI is data-driven: `MonsterBehavior` component (`Type`, `Range`, `SpecialEnergyCost`) drives `MonsterAISystem`. `InferBehavior(char)` remains only as a fallback for legacy saves / no-content scenarios.
- Rendering is decoupled from persistence: `RootScreen.DrawMap()` uses `GameSession.GetRenderSnapshot()` (returns `RenderCell` records), not `SaveMap()`.
- Save format is versioned (`MapData.Version`). The current version is 4. Newer DTO fields are optional/nullable so legacy saves remain loadable. Persisted entity data includes position, glyph, inventory, behavior, health/max health, and attack.
- Keep content-loading code in `MonoRogue.Data`; do not move JSON-driven content definitions into the gameplay layer. Keep UI ownership in `MonoRogue.UI`.

## Development workflow (agent-friendly)
1. Run a clean build for a reproducible baseline:
   - Remove all `bin`/`obj` folders (see troubleshooting below), then `dotnet build MonoRogue.slnx`.
2. Use `git checkout -b <short-topic>` for any set of changes.
3. Make minimal, focused edits. Prefer separate commits per concern.
4. Run `dotnet test MonoRogue.slnx` (or `dotnet test Tests\MonoRogue.Tests.csproj`) after logic changes.
5. Open a PR with a short description, CI results, and any required migration steps.

## Coding rules and style
- Follow existing C# style: nullable annotations enabled, implicit usings, concise comments (only for non-obvious logic).
- Keep public APIs stable; prefer additive changes.
- Add small, focused unit tests for logic changes. If a change is non-deterministic (e.g. procedural generation using `Random.Shared`), clear or seed the relevant entities so tests are deterministic.
- Tests are xUnit `[Fact]` methods, not a custom runner. Do not reintroduce a `--test-content` program flag.
- Document high-level design decisions in a short comment block or the PR description.

## Build troubleshooting — duplicate assembly attribute errors (CS0579)
- Symptom: `dotnet build` fails with CS0579 duplicate attribute errors pointing to generated files under `obj/` (e.g. `MyGame.AssemblyInfo.cs` and `.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`).
- Root cause: stale or duplicated intermediate files under `obj/` or `bin/` can cause the SDK to generate assembly attributes twice for the same assembly. This often happens after moving the repository between drives/machines or when intermediate files were committed/leftover from an earlier build on a different path.

### Immediate fix (Windows PowerShell, from repo root)
```powershell
Set-Location -Path 'C:\Users\Dustin\MonogameProjects\MonoRogue'
Get-ChildItem -Directory -Recurse | Where-Object { $_.Name -in @('bin','obj') } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
dotnet build 'C:\Users\Dustin\MonogameProjects\MonoRogue\MonoRogue.slnx' --no-incremental
```

- Alternative: `dotnet clean` then `dotnet build` for the solution or per project.

### Prevention
- Ensure `bin/` and `obj/` are in `.gitignore` so intermediate files are never checked in.
- `MyGame.csproj` already sets `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` and `<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>` with a blank `Properties\AssemblyInfo.cs` to avoid CS0579. Do not remove these settings unless you provide a complete assembly attribute set yourself.

## CI guidance
- CI runs should always do a fresh `dotnet restore` and `dotnet build` (no incremental `obj/` caching unless intentionally configured).
- Add a `dotnet test` step to run the xUnit suite.

## When to ask for human review
- Any change touching public APIs, major architectural changes, or anything that may affect save formats or serialized data should be presented to a human reviewer.
- If a build failure persists after cleanup and you cannot identify why, produce a short diagnostic summary (relevant `dotnet build` errors, `obj/` listing) and request human input.

## Notes
- This document is agent-facing; keep it updated when the repository structure or build system changes.
- This repository may contain committed build outputs under `bin/` and populated `obj/` folders. Treat these as stale artifacts, run a clean build, and do not rely on checked-in binaries.
