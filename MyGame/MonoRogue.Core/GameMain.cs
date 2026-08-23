using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Enum specifying the current state of the game, which determines what input commands are valid and what UI is displayed.
/// </summary>
public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    Inventory,
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
    OpenInventory,
    InventoryUp,
    InventoryDown,
    InventorySelect,
    InventoryCancel,
    MenuUp,
    MenuDown,
    MenuSelect,
    MenuExit,
    Confirm
}

/// <summary>
/// Owns the active game session (the <see cref="GameSession"/>) and exposes the gameplay and
/// persistence commands that the UI forwards to it. The SadConsole layer only renders
/// <see cref="GetCurrentSession"/> and sends <see cref="InputCommand"/>s here, keeping game logic
/// independent of the presentation stack.
/// </summary>
public class GameMain
{
    private GameSession? _session;
    private GameState _currentState = GameState.MainMenu;

    // The current state of the game. Starts in the main menu.
    public GameState GetCurrentState() => _currentState;

    // The active map, or null until a game is started or loaded.
    public GameSession? GetCurrentSession() => _session;

    // True once a map has been created (a game is in progress).
    public bool HasActiveGame()
    {
        return _session != null;
    }

    // Starts a brand-new game with a map of the given dimensions.
    public void StartNewGame(int mapWidth, int mapHeight)
    {
        _session?.Dispose();
        _session = new GameSession(mapWidth, mapHeight);
        _currentState = GameState.Playing;
    }

    public void GameOver()
    {
        _currentState = GameState.GameOver;
    }

    // Returns to the main menu, disposing any active game session.
    public void ReturnToMainMenu()
    {
        _session?.Dispose();
        _session = null;
        _currentState = GameState.MainMenu;
    }

    // Returns true if the current state allows gameplay input.
    public bool AllowsGameplayInput()
    {
        return _currentState == GameState.Playing;
    }

    // Pauses the game if it is currently playing.
    public void PauseGame()
    {
        if (_currentState == GameState.Playing)
        {
            _currentState = GameState.Paused;
        }
    }

    // Resumes the game if it is currently paused.
    public void ResumeGame()
    {
        if (_currentState == GameState.Paused)
        {
            _currentState = GameState.Playing;
        }
    }

    // Toggles the pause state of the game.
    public void TogglePause()
    {
        if (_currentState == GameState.Playing)
        {
            _currentState = GameState.Paused;
        }
        else if (_currentState == GameState.Paused)
        {
            _currentState = GameState.Playing;
        }
    }

    // ---- Gameplay commands (delegate to the active map) ----

    // Processes a player turn with the given movement delta. Returns the result of the turn or null if no game is active.
    public TurnResult? ProcessPlayerTurn(Point playerDelta)
    {
        return _session?.ProcessPlayerTurn(playerDelta);
    }

    // Processes the use of a potion. Returns the result of the turn or null if no game is active.
    public TurnResult? ProcessUsePotion()
    {
        return _session?.ProcessUsePotion();
    }

    // ---- Inventory modal ----

    // Opens the inventory selection modal (only from active gameplay). 
    public void OpenInventory()
    {
        if (_currentState == GameState.Playing)
        {
            _currentState = GameState.Inventory;
        }
    }

    // Closes the inventory selection modal and resumes gameplay.
    public void CloseInventory()
    {
        if (_currentState == GameState.Inventory)
        {
            _currentState = GameState.Playing;
        }
    }

    // Uses the item at the given inventory index as a full turn. Returns null when no game is active.
    public TurnResult? UseItemAt(int index)
    {
        return _session?.ProcessUseItemAt(index);
    }

    // ---- Persistence ----

    // Saves the active game. Returns false when there is no active game.
    public bool SaveMap(string path)
    {
        if (_session == null)
        {
            return false;
        }

        MapPersistenceHelpers.SaveToFile(_session, path);
        return true;
    }

    // "Continue": loads a game from disk, creating a fresh map first if one does not exist yet, then transitions to
    // GameState.Playing. Returns false when no save exists or loading fails.
    public bool LoadMap(string path, int mapWidth, int mapHeight)
    {
        _session ??= new GameSession(mapWidth, mapHeight);

        if (!MapPersistenceHelpers.LoadIntoWorld(_session, path))
        {
            return false;
        }

        _currentState = GameState.Playing;
        return true;
    }


    // Process input provided by an IInputProvider (UI adapter) and return the commands that are valid for the current state.
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
                    if (_currentState == GameState.MainMenu)
                    {
                        results.Add(cmd);
                    }
                    break;

                // Toggle pause only valid when playing or paused
                case InputType.TogglePause:
                    if (_currentState == GameState.Playing || _currentState == GameState.Paused)
                    {
                        results.Add(cmd);
                    }
                    break;

                // Movement / rest / open-inventory only when gameplay input is allowed
                case InputType.Move:
                case InputType.Rest:
                case InputType.OpenInventory:
                    if (AllowsGameplayInput())
                    {
                        results.Add(cmd);
                    }
                    break;

                // Inventory navigation/selection only while the inventory modal is open
                case InputType.InventoryUp:
                case InputType.InventoryDown:
                case InputType.InventorySelect:
                case InputType.InventoryCancel:
                    if (_currentState == GameState.Inventory)
                    {
                        results.Add(cmd);
                    }
                    break;

                // Confirm (return to menu) only valid after a game over
                case InputType.Confirm:
                    if (_currentState == GameState.GameOver)
                    {
                        results.Add(cmd);
                    }
                    break;
            }
        }

        return results;
    }
}
