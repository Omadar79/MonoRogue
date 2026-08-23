using MonoRogue.Core;
using SadConsole.Input;

namespace MonoRogue.UI;

/// <summary>
/// Adapter that bridges SadConsole keyboard snapshots to the core IInputProvider interface. This keeps the core free of 
/// SadConsole types while allowing the UI to decide how keys map to domain-level commands
/// </summary>
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

        // If we're at the main menu, map keys to menu commands and return them.
        if (_game.GetCurrentState() == GameState.MainMenu)
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

        // Game over: allow the player to return to the main menu.
        if (_game.GetCurrentState() == GameState.GameOver)
        {
            if (_keyboard.IsKeyPressed(Keys.Enter) || _keyboard.IsKeyPressed(Keys.Escape))
            {
                results.Add(new InputCommand(InputType.Confirm, new SadRogue.Primitives.Point(0, 0)));
            }

            return results;
        }

        // Inventory modal: arrows navigate, Enter uses, Escape closes.
        if (_game.GetCurrentState() == GameState.Inventory)
        {
            if (_keyboard.IsKeyPressed(Keys.Up))
            {
                results.Add(new InputCommand(InputType.InventoryUp, new SadRogue.Primitives.Point(0, 0)));
            }
            else if (_keyboard.IsKeyPressed(Keys.Down))
            {
                results.Add(new InputCommand(InputType.InventoryDown, new SadRogue.Primitives.Point(0, 0)));
            }

            if (_keyboard.IsKeyPressed(Keys.Enter))
            {
                results.Add(new InputCommand(InputType.InventorySelect, new SadRogue.Primitives.Point(0, 0)));
            }

            if (_keyboard.IsKeyPressed(Keys.Escape))
            {
                results.Add(new InputCommand(InputType.InventoryCancel, new SadRogue.Primitives.Point(0, 0)));
            }

            return results;
        }

        // Pause/unpause is allowed when playing or paused.
        if (_keyboard.IsKeyPressed(Keys.Escape) && (_game.GetCurrentState() == GameState.Playing || _game.GetCurrentState() == GameState.Paused))
        {
            results.Add(new InputCommand(InputType.TogglePause, new SadRogue.Primitives.Point(0, 0)));
        }

        // Movement only when gameplay input is allowed.
        if (_game.AllowsGameplayInput())
        {
            if (_keyboard.IsKeyPressed(Keys.R))
            {
                results.Add(new InputCommand(InputType.Rest, new SadRogue.Primitives.Point(0, 0)));
            }

            if (_keyboard.IsKeyPressed(Keys.U))
            {
                results.Add(new InputCommand(InputType.OpenInventory, new SadRogue.Primitives.Point(0, 0)));
            }

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
