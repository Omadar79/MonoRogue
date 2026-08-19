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
    MenuUp,
    MenuDown,
    MenuSelect,
    MenuExit
}

/// <summary>
/// Controls the state and logic of the game.  I wanted to seperate the game logic from the SadConsole logic, 
/// so that the game could be ported to other platforms in the future.
/// </summary>
public class GameMain
{
    /// <summary>
    /// The current state of the game.  Start in the Main Menu
    /// </summary>
    public GameState CurrentState { get; private set; } = GameState.MainMenu;



    public void StartNewGame()
    {
        CurrentState = GameState.Playing;
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

    
    /// <summary>
    /// Process input provided by an IInputProvider (UI adapter) and return actions for the front-end to execute.
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
                    if (AllowsGameplayInput())
                    {
                        results.Add(cmd);
                    }
                    break;
            }
        }

        return results;
    }

}
