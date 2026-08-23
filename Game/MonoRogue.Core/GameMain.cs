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
    Confirm,
    Quit
}

/// <summary>
/// Owns the active game session (the <see cref="GameSession"/>) and exposes the gameplay and persistence commands
///  that the UI forwards to it. The SadConsole layer only renders <see cref="GetCurrentSession"/> and sends
///  <see cref="InputCommand"/>s here, keeping game logic independent of the presentation stack.
/// </summary>
public class GameMain
{
    private GameSession? _session;
    private GameState _currentState = GameState.MainMenu;
    private readonly string _saveFilePath;

    public GameMain(string? saveFilePath = null)
    {
        _saveFilePath = saveFilePath ?? MapPersistenceHelpers.GetDefaultSavePath();
    }

    // The current state of the game. Starts in the main menu.
    public GameState GetCurrentState() => _currentState;

    // The active map, or null until a game is started or loaded.
    public GameSession? GetCurrentSession() => _session;

    // Returns true once a map has been created (a game is in progress).
    public bool HasActiveGame()
    {
        return _session != null;
    }

    // Starts a brand-new game with a procedurally generated rooms-and-corridors map. An
    // optional seed makes generation reproducible; when omitted, a non-deterministic seed
    // is used. Any existing auto-save is cleared so a new run never resumes stale progress.
    public void StartNewGame(int mapWidth, int mapHeight, int? seed = null)
    {
        _session?.Dispose();
        _session = new GameSession(mapWidth, mapHeight, seed, new RoomsAndCorridorsLayoutGenerator());
        DeleteSave();
        _currentState = GameState.Playing;
    }

    // The player died: discard the auto-save and enter the game-over state.
    public void GameOver()
    {
        DeleteSave();
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
        var result = _session?.ProcessPlayerTurn(playerDelta);
        if (result is TurnResult turn && !turn.PlayerDied)
        {
            AutoSave();
        }
        return result;
    }

    // Processes the use of a potion. Returns the result of the turn or null if no game is active.
    public TurnResult? ProcessUsePotion()
    {
        var result = _session?.ProcessUsePotion();
        if (result is TurnResult turn && !turn.PlayerDied)
        {
            AutoSave();
        }
        return result;
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
        var result = _session?.ProcessUseItemAt(index);
        if (result is TurnResult turn && !turn.PlayerDied)
        {
            AutoSave();
        }
        return result;
    }

    // ---- Persistence ----

    // Single auto-save slot. Every completed player turn overwrites this file; it is
    // deleted when the player dies (or starts a new game), and recreated on the next turn.
    // The path lives in the OS application-data directory so it works on Windows, Linux,
    // and macOS alike.
    public static string GetDefaultSaveFilePath() => MapPersistenceHelpers.GetDefaultSavePath();

    // Returns true when a continue-able auto-save exists on disk.
    public bool HasSaveFile() => MapPersistenceHelpers.SaveFileExists(_saveFilePath);

    // "Continue": loads the auto-save into a fresh session and transitions to playing.
    // Returns false when no valid save exists.
    public bool ContinueGame(int mapWidth, int mapHeight)
    {
        _session?.Dispose();
        _session = new GameSession(mapWidth, mapHeight);

        if (!MapPersistenceHelpers.LoadIntoWorld(_session, _saveFilePath))
        {
            return false;
        }

        _currentState = GameState.Playing;
        return true;
    }

    // Writes the current session to the auto-save slot. No-op without an active game.
    private void AutoSave()
    {
        if (_session == null)
        {
            return;
        }

        MapPersistenceHelpers.SaveToFile(_session, _saveFilePath);
    }

    // Removes the auto-save file (used on death and when starting a new game).
    private void DeleteSave() => MapPersistenceHelpers.DeleteSave(_saveFilePath);


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

                // Quit (exit the game) only valid from the pause menu
                case InputType.Quit:
                    if (_currentState == GameState.Paused)
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
