# MonoRogue

A turn-based roguelike built on .NET 10, MonoGame, SadConsole, RogueSharp, and Arch ECS.

I wanted to create a roguelike that was easy to read and understand, with a focus on clean architecture and testability. 
The game is designed to be simple, but extensible, so that new features can be added without breaking existing code.

NOTE: I used AI to help me create tests and separate the SadConsole UI from the game logic, so that the game can be tested without a UI.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/) (developed against `10.0.302`)
- A terminal/window host capable of running a MonoGame DesktopGL app (Windows, Linux, macOS)

## Build & run

```powershell
# Build the whole solution
dotnet build MonoRogue.slnx

# Run the game
dotnet run --project MyGame\MyGame.csproj

# Run the test suite
dotnet test MonoRogue.slnx
```

Runtime content (`monsters.json`, `items.json`) lives in `Data\` and is copied next to
the built executable automatically.

## Project layout

| Path | Purpose |
| --- | --- |
| `MonoRogue.slnx` | Solution file (two projects) |
| `MyGame\MyGame.csproj` | Executable (WinExe, net10.0) |
| `MyGame\MonoRogue.Core\` | Gameplay, map generation, ECS components & systems |
| `MyGame\MonoRogue.Data\` | JSON-backed content definitions and loaders |
| `MyGame\MonoRogue.UI\` | SadConsole terminal/UI layer |
| `Data\` | Runtime content: `monsters.json`, `items.json` |
| `Tests\` | xUnit test project referencing `MyGame` |

## Architecture

`MonoRogue.Core` is organized around the Arch ECS:

- **`GameSession`** is a thin orchestrator that composes focused systems, owns persistence,
  inventory, and turn ordering.
- **Systems** (under `MonoRogue.Core.Systems`) each own one responsibility:
  energy, combat, effects, player actions, and monster AI.
- **`EntityFactory`** is the single owner of all world structural mutations
  (create/destroy/clear); systems observe state but mutate only through the factory.
- **`MapSerializer`** / **`WorldSnapshotReader`** handle save/load mapping; file I/O is
  isolated in `MapPersistenceHelpers`.
- **`MonoRogue.Data`** keeps JSON-driven content definitions separate from gameplay logic.
- Monster AI is data-driven via `MonsterBehavior` (melee/breath, range, special cost).

## Save format

Saves are JSON via `MapData` with a versioned `Version` field (currently `4`). Newer DTO
fields are optional so legacy saves remain loadable.

## CI

`.github/workflows/ci.yml` performs a fresh restore, build, and test on every push to
`main` and every pull request.
