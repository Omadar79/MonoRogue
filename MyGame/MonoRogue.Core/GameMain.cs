using SadRogue.Primitives;

namespace MonoRogue.Core;

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// Enum specifying the type of input command that the game can process.
/// </summary>
public enum InputType
{
    TogglePause,
    Move,
    Rest,
    UseItem,
    MenuUp,
    MenuDown,
    MenuSelect,
    MenuExit,
    Confirm
}

/// <summary>
/// Owns the active game session (the <see cref="MapBase"/>) and exposes the gameplay and
/// persistence commands that the UI forwards to it. The SadConsole layer only renders
/// <see cref="CurrentMap"/> and sends <see cref="InputCommand"/>s here, keeping game logic
/// independent of the presentation stack.
/// </summary>
public class GameMain
{
    private MapBase? _map;

    /// <summary>The current state of the game. Starts in the main menu.</summary>
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    /// <summary>The active map, or null until a game is started or loaded.</summary>
    public MapBase? CurrentMap => _map;

    /// <summary>True once a map has been created (a game is in progress).</summary>
    public bool HasActiveGame => _map != null;

    /// <summary>Starts a brand-new game with a map of the given dimensions.</summary>
    public void StartNewGame(int mapWidth, int mapHeight)
    {
        _map?.Dispose();
        _map = new MapBase(mapWidth, mapHeight);
        CurrentState = GameState.Playing;
    }

    public void GameOver()
    {
        CurrentState = GameState.GameOver;
    }

    /// <summary>Returns to the main menu, disposing any active game session.</summary>
    public void ReturnToMainMenu()
    {
        _map?.Dispose();
        _map = null;
        CurrentState = GameState.MainMenu;
    }

    public bool AllowsGameplayInput()
    {
        return CurrentState == GameState.Playing;
    }

    public void PauseGame()
    {
        if (CurrentState == GameState.Playing)
        {
            CurrentState = GameState.Paused;
        }
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
        }
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Playing)
        {
            CurrentState = GameState.Paused;
        }
        else if (CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
        }
    }

    // ---- Gameplay commands (delegate to the active map) ----

    public TurnResult? ProcessPlayerTurn(Point playerDelta) => _map?.ProcessPlayerTurn(playerDelta);

    public TurnResult? ProcessUsePotion() => _map?.ProcessUsePotion();

    // ---- Persistence ----

    /// <summary>Saves the active game. Returns false when there is no active game.</summary>
    public bool SaveMap(string path)
    {
        if (_map == null)
        {
            return false;
        }

        MapPersistenceHelpers.SaveToFile(_map, path);
        return true;
    }

    /// <summary>
    /// "Continue": loads a game from disk, creating a fresh map first if one does not
    /// exist yet, then transitions to <see cref="GameState.Playing"/>. Returns false when
    /// no save exists or loading fails.
    /// </summary>
    public bool LoadMap(string path, int mapWidth, int mapHeight)
    {
        if (_map == null)
        {
            _map = new MapBase(mapWidth, mapHeight);
        }

        if (!MapPersistenceHelpers.LoadIntoWorld(_map, path))
        {
            return false;
        }

        CurrentState = GameState.Playing;
        return true;
    }

    /// <summary>
    /// Process input provided by an IInputProvider (UI adapter) and return the commands
    /// that are valid for the current state.
    /// </summary>
    public IEnumerable<InputCommand> ProcessInput(IInputProvider inputProvider)
    {
        var incoming = inputProvider.ConsumeCommands();
        var results = new List<InputCommand>();

        foreach (var cmd in incoming)
        {
            switch (cmd.Type)
            {
                // Menu navigation/selection only valid in the main menu
                case InputType.MenuUp:

                case InputType.MenuDown:

                case InputType.MenuSelect:

                case InputType.MenuExit:
                    if (CurrentState == GameState.MainMenu)
                    {
                        results.Add(cmd);
                    }
                    break;

                // Toggle pause only valid when playing or paused
                case InputType.TogglePause:
                    if (CurrentState == GameState.Playing || CurrentState == GameState.Paused)
                    {
                        results.Add(cmd);
                    }
                    break;

                // Movement only when gameplay input is allowed
                case InputType.Move:
                case InputType.Rest:
                case InputType.UseItem:
                    if (AllowsGameplayInput())
                    {
                        results.Add(cmd);
                    }
                    break;

                // Confirm (return to menu) only valid after a game over
                case InputType.Confirm:
                    if (CurrentState == GameState.GameOver)
                    {
                        results.Add(cmd);
                    }
                    break;
            }
        }

        return results;
    }
}
