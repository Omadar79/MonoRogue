# MonoRogue Agents Guide

This document replaces older `agent.md` guidance and describes how automated agents (AI or CI) should interact with the MonoRogue repository.

Checklist (what this document provides)
- Purpose and scope for agents interacting with this repository
- Environment and runtime expectations
- Recommended workflow for making changes (development loop)
- Style and testing requirements
- Build troubleshooting (duplicate assembly attribute errors and other common issues)
- Useful scripts and where to find them (including a cleanup script)

Purpose and scope
- Agents should assist developers by making small, well-scoped changes, creating or updating tests, fixing build errors, and suggesting architectural improvements.
- Avoid large, sweeping refactors unless explicitly requested by the human reviewer.
- When in doubt, create a clear PR with an explanation and request human review.

Environment and expectations
- Target framework: .NET (projects use `TargetFramework` set to `net10.0` in this repository). Projects include `MyGame`, `MonoRogue.Core`, and `MonoRogue.UI`.
- Tools available on the developer machine: `dotnet` CLI, PowerShell (Windows), and standard Git.
- Do not assume any files under `bin/` or `obj/` are present — agents should not rely on committed build artifacts.

Repository layout (high-level)
- `MyGame.csproj` — top-level application project (hosts references to Core and UI)
- `MonoRogue.Core/` — core gameplay logic, map generation, components
- `MonoRogue.UI/` — UI layer, SadConsole glyph mapping, rendering glue
- `Program.cs` — app entry and SadConsole host setup

Development workflow (agent-friendly)
1. Run a full clean build to get a reproducible baseline:
   - `dotnet clean` (or the provided script at `scripts/clean-build.ps1`)
   - `dotnet build MonoRogue.slnx`
2. Use `git checkout -b <short-topic>` for any set of changes.
3. Make minimal, focused edits. Prefer separate commits per concern.
4. Run unit tests (if added) and a debug run locally when applicable.
5. Open a PR that includes a short description, CI results, and any required migration steps.

Coding rules and style
- Follow existing C# style in the repository (nullable annotations enabled, concise comments).
- Keep public APIs stable; prefer additive changes when possible.
- Add small, focused unit tests for logic changes. If the change is non-deterministic (e.g., procedural generation with randomness), seed the RNG in tests.
- Document high-level design decisions in a short comment block or in the PR description.

Build troubleshooting — duplicate assembly attribute errors (CS0579)
- Symptom: `dotnet build` fails with CS0579 duplicate attribute errors pointing to generated files under `obj/` (for example: `MyGame.AssemblyInfo.cs` and `.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`).
- Root cause: stale or duplicated intermediate files under `obj/` or `bin/` can cause the SDK to generate assembly attributes twice for the same assembly. This often happens after moving the repository between drives or machines, or when intermediate files were committed/leftover from an earlier build on a different path.

Immediate fix
- Best (fast) approach on Windows PowerShell (from repo root):

```powershell
# remove all bin and obj folders then rebuild
Set-Location -Path 'C:\Users\Dustin\MonogameProjects\MonoRogue'
Get-ChildItem -Directory -Recurse | Where-Object { $_.Name -in @('bin','obj') } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
dotnet build 'C:\Users\Dustin\MonogameProjects\MonoRogue\MonoRogue.slnx' --no-incremental
```

- Alternative: `dotnet clean` then `dotnet build` for solution or per-project.

Prevention
- Ensure `bin/` and `obj/` are in `.gitignore` so intermediate files are never checked into source control.
- If you intentionally maintain your own `AssemblyInfo.cs` with attributes, disable SDK auto-generation by adding the following to the relevant project file(s):

```xml
<PropertyGroup>
  <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
</PropertyGroup>
```

Only disable SDK generation when you provide a complete assembly attribute set yourself.

Useful scripts
- `scripts/clean-build.ps1` — convenience script that performs the `bin/` and `obj/` cleanup and rebuild. Use it when you see build issues or after moving the repository.

CI guidance
- CI runs should always do a fresh `dotnet restore` and `dotnet build` (no incremental caching of `obj/` unless intentionally configured).
- Add a `dotnet test` step once the project has unit tests.

When to ask for human review
- Any change that touches public APIs, major architectural changes, or anything that may affect save formats or serialized data should be presented to a human reviewer.
- If a build failure persists after cleanup and you can't identify why, produce a short diagnostic summary (relevant `dotnet build` errors, `obj/` listing) and request human input.

Contact/notes
- This document is agent-facing; keep it updated when the repository structure or build system changes.
- For more developer-facing tips, see `agent.md` (kept for historical notes). The `agent.md` file includes a short troubleshooting section as well.

