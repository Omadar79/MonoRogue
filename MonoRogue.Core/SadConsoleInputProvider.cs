using SadConsole.Input;


namespace MonoRogue.Core;

// Adapter that bridges SadConsole keyboard snapshots to the core IInputProvider interface.
// This keeps the core free of SadConsole types while allowing the UI to decide how keys
// map to domain-level commands (it may consult the game's current state when mapping).
public class SadConsoleInputProvider : IInputProvider
{
    private readonly GameMain _game;
    private readonly Keyboard _keyboard;

    public SadConsoleInputProvider(GameMain game, Keyboard keyboard)
    {
        _game = game;
        _keyboard = keyboard;
    }

    public IEnumerable<InputCommand> ConsumeCommands()
    { 
        var results = new List<InputCommand>();

        // If we're at the main menu, map keys to menu commands and return them
        if (_game.CurrentState == GameState.MainMenu)
        {
            if (_keyboard.IsKeyPressed(Keys.Up))
            {
                results.Add(new InputCommand(InputType.MenuUp, new SadRogue.Primitives.Point(0, 0)));
            }
            else if (_keyboard.IsKeyPressed(Keys.Down))
            { 
                results.Add(new InputCommand(InputType.MenuDown, new SadRogue.Primitives.Point(0, 0)));
            }

            if (_keyboard.IsKeyPressed(Keys.Enter))
            { 
                results.Add(new InputCommand(InputType.MenuSelect, new SadRogue.Primitives.Point(0, 0)));
            }

            if (_keyboard.IsKeyPressed(Keys.Escape))
            {
                results.Add(new InputCommand(InputType.MenuExit, new SadRogue.Primitives.Point(0, 0)));
            }

            return results;
        }

        // Pause/unpause is allowed when playing or paused.
        if (_keyboard.IsKeyPressed(Keys.Escape) && (_game.CurrentState == GameState.Playing || _game.CurrentState == GameState.Paused))
        {
            results.Add(new InputCommand(InputType.TogglePause, new SadRogue.Primitives.Point(0, 0)));
        }

        // Movement only when gameplay input is allowed
        if (_game.AllowsGameplayInput())
        {
            if (_keyboard.IsKeyPressed(Keys.Up))
            { 
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(0, -1)));
            }
            else if (_keyboard.IsKeyPressed(Keys.Down))
            { 
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(0, 1)));
            }

            if (_keyboard.IsKeyPressed(Keys.Left))
            { 
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(-1, 0)));
            }
            else if (_keyboard.IsKeyPressed(Keys.Right))
            { 
                results.Add(new InputCommand(InputType.Move, new SadRogue.Primitives.Point(1, 0)));
            }
        }

        return results;
    }
}