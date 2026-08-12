

namespace MonoRogue.Core;

/// <summary>
/// Game Main controls the state and logic of the game.  I wanted to seperate the game logic from the SadConsole logic, 
/// so that the game could be ported to other platforms in the future.
/// </summary>
public class GameMain
{
    public GameState CurrentState { get; private set; }


    public GameMain()
    {
        CurrentState = GameState.MainMenu;
    }

    public void StartGame()
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

    // Input command model for routing input centrally
    public enum InputType
    {
        TogglePause,
        Move
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

    // Process keyboard centrally and return actions for the front-end to execute.
    // This keeps state-based gating inside GameMain instead of scattered ifs.
    public IEnumerable<InputCommand> ProcessKeyboard(SadConsole.Input.Keyboard keyboard)
    {
        var results = new List<InputCommand>();

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
