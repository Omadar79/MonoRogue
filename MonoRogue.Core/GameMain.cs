

namespace MonoRogue.Core;

/// <summary>
/// Game Main controls the state and logic of the game.  I wanted to seperate the game logic from the SadConsole logic, 
/// so that the game could be ported to other platforms in the future.
/// </summary>
public class GameMain
{

    // Input command model for routing input centrally
    public enum InputType
    {
        TogglePause,
        Move,
        MenuUp,
        MenuDown,
        MenuSelect,
        MenuExit
    }

    public readonly struct InputCommand
    {
        public InputType Type { get; }
        public SadRogue.Primitives.Point Delta { get; }

        public InputCommand(InputType type, SadRogue.Primitives.Point delta)
        {
            Type = type;
            Delta = delta;
        }
    }


    public GameState CurrentState { get; private set; }


    public GameMain()
    {
        //start the game in the main menu state
        CurrentState = GameState.MainMenu;
    }

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


    // Process keyboard centrally and return actions for the front-end to execute.
    public IEnumerable<InputCommand> ProcessKeyboard(SadConsole.Input.Keyboard keyboard)
    {
        var results = new List<InputCommand>();

        // If we're at the main menu, map keys to menu commands and return them
        if (CurrentState == GameState.MainMenu)
        {
            if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Up))
            {
                results.Add(new InputCommand(InputType.MenuUp, new SadRogue.Primitives.Point(0, 0)));
            }
            else if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Down))
            {
                results.Add(new InputCommand(InputType.MenuDown, new SadRogue.Primitives.Point(0, 0)));
            }

            if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Enter))
            {
                results.Add(new InputCommand(InputType.MenuSelect, new SadRogue.Primitives.Point(0, 0)));
            }

            if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Escape))
            {
                results.Add(new InputCommand(InputType.MenuExit, new SadRogue.Primitives.Point(0, 0)));
            }

            return results;
        }

        // Pause/unpause is allowed when playing or paused.
        if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Escape) && (CurrentState == GameState.Playing || CurrentState == GameState.Paused))
        {
            results.Add(new InputCommand(InputType.TogglePause, new SadRogue.Primitives.Point(0, 0)));
        }

        // Movement only when gameplay input is allowed
        if (AllowsGameplayInput())
        {
            if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Up))
            {
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(0, -1)));
            }
            else if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Down))
            {
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(0, 1)));
            }

            if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Left))
            {
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(-1, 0)));
            }
            else if (keyboard.IsKeyPressed(SadConsole.Input.Keys.Right))
            {
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(1, 0)));
            }
        }

        return results;
    }

}

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}
