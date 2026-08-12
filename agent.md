# MonoRogue Agent Guide

This file describes how an AI coding agent should work in the MonoRogue project.

Primary stack:
- MonoGame host framework
- RogueSharp for roguelike map/gameplay algorithms
- Arch ECS for entity-component-system gameplay structure
- SadConsole for terminal-style rendering and input

Project layout:
- Program.cs: application startup and SadConsole host configuration.
- MonoRogue.Core/GameMain.cs: high-level game state and flow.
- MonoRogue.Core/Map.cs: map surface, entities, and spatial interactions.
- MonoRogue.Core/Components.cs: ECS component definitions for position, rendering, and tags.
- MonoRogue.Screens/RootScreen.cs: screen composition and keyboard input routing.

Rules for code changes:
- Keep gameplay rules deterministic and turn-based.
- Keep state transitions explicit (menu, playing, paused, game over).
- Prefer Arch ECS when organizing entities, components, and systems.
- Prefer RogueSharp primitives/algorithms for dungeon generation and FOV/pathfinding additions.
- Keep SadConsole rendering logic near screen/surface code.
- Use bounds checks and occupancy checks before movement.
- Avoid broad refactors unless specifically requested.

Rendering and input guidance:
- Continue using ColoredGlyph for map and entities.
- Mark surfaces dirty only when a visible change occurs.
- Keep keyboard command mapping straightforward and easy to audit.

Code quality guidance:
- Match existing C# style and nullable usage in the repository.
- Add brief comments only when logic is non-obvious.
- Preserve public APIs unless a change is required for the feature.

Typical implementation workflow:
1. Define gameplay behavior and state transitions.
2. Implement core logic in focused classes.
3. Wire input actions to that logic.
4. Update SadConsole rendering.
5. Run `dotnet run` and fix errors.

For VS Code custom-agent support, see:
- .github/agents/mono-rogue.agent.md
