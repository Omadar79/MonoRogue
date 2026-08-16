using SadConsole;
using Game = SadConsole.Game;
using SadConsole.Input;
using Color = SadRogue.Primitives.Color;
using System.Text.Json;
using Console = System.Console;

namespace MonoRogue.Core;
public class RootScreen : ScreenObject
{
    private GameMain _game;
    private MapBase _map;
    private ScreenSurface _pauseOverlay;
    private ScreenSurface _menuOverlay;
    private int _menuSelectedIndex = 0;
    private readonly string[] _menuOptions = new[] { "New Game", "Save Map", "Load Map", "Exit Game" };
    private readonly IGlyphMapper _glyphMapper;

    public RootScreen(GameMain game, IGlyphMapper glyphMapper)
    {
        _game = game;
        _glyphMapper = glyphMapper;

        UseKeyboard = true;

        // Create the main map 
        _map = new MapBase(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY - 5);

        // Probe initial player state using the mapper (no-op if none); keeps mapping wired-up for later operations
        var initialPlayer = _map.ExtractPlayerState(_glyphMapper);

        // Ensure the map surface does not steal keyboard focus from this screen
        _map.SurfaceObject.UseKeyboard = false;
        Children.Add(_map.SurfaceObject);

        // Create a pause overlay that will be shown when the game is paused
        _pauseOverlay = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);
        _pauseOverlay.UseMouse = false;
        _pauseOverlay.UseKeyboard = false;
        _pauseOverlay.IsVisible = false;
        DrawPauseMessage();
        Children.Add(_pauseOverlay);

        // Create a menu overlay and draw the main menu; it will be visible when GameState == MainMenu
        _menuOverlay = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);
        _menuOverlay.UseMouse = false;
        _menuOverlay.UseKeyboard = false; // RootScreen handles keyboard centrally
        DrawMainMenu();
        _menuOverlay.IsVisible = (_game.CurrentState == GameState.MainMenu);
        Children.Add(_menuOverlay);
    }

    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        // Let visible children that accept keyboard input handle it first (topmost first)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is ScreenObject child && child.IsVisible && child.UseKeyboard)
            {
                if (child.ProcessKeyboard(keyboard))
                {
                    return true;
                }
            }
        }

        bool handled = false;

        // Create an input-provider adapter that translates the SadConsole Keyboard snapshot
        // into domain-level InputCommand instances, then pass that to the game core.
        var provider = new SadConsoleInputProvider(_game, keyboard);
        var commands = _game.ProcessInput(provider);
        foreach (var cmd in commands)
        {
            switch (cmd.Type)
            {
                case InputType.MenuUp:
                    _menuSelectedIndex = (_menuSelectedIndex - 1 + _menuOptions.Length) % _menuOptions.Length;
                    DrawMainMenu();
                    handled = true;
                    break;

                case InputType.MenuDown:
                    _menuSelectedIndex = (_menuSelectedIndex + 1) % _menuOptions.Length;
                    DrawMainMenu();
                    handled = true;
                    break;

                case InputType.MenuSelect:
                    var choice = _menuOptions[_menuSelectedIndex];
                    if (choice == "New Game")
                    {
                        _game.StartNewGame();
                        _menuOverlay.IsVisible = false;
                    }
                    else if (choice == "Save Map")
                    {
                        try
                        {
                            var mapData = _map.SaveMap(_glyphMapper);
                            var json = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText("saved_map.json", json);
                        }
                        catch (Exception ex)
                        {
                            // For now just ignore errors; in future show a message to the user.
                            Console.WriteLine($"Failed to save map: {ex.Message}");
                        }
                        handled = true;
                    }
                    else if (choice == "Load Map")
                    {
                        try
                        {
                            if (File.Exists("saved_map.json"))
                            {
                                var json = File.ReadAllText("saved_map.json");
                                var mapData = JsonSerializer.Deserialize<MapData>(json);
                                if (mapData != null)
                                {
                                    _map.LoadMap(mapData, _glyphMapper);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load map: {ex.Message}");
                        }
                        handled = true;
                    }
                    else if (choice == "Exit Game")
                    {
                        Environment.Exit(0);
                    }
                    handled = true;
                    break;

                case InputType.MenuExit:
                    Environment.Exit(0);
                    handled = true;
                    break;

                case InputType.TogglePause:
                    _game.TogglePause();

                    // Show or hide the pause overlay based on game state
                    _pauseOverlay.IsVisible = (_game.CurrentState == GameState.Paused);

                    if (_pauseOverlay.IsVisible)
                    {
                        DrawPauseMessage();
                    }
                    handled = true;
                    break;

                case InputType.Move:
                    _map.TryMovePlayer(cmd.Delta);
                    handled = true;
                    break;
            }
        }

        return handled;
    }

    private void DrawPauseMessage()
    {
        var surface = _pauseOverlay.Surface;

        // Fill overlay with a solid background
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        const string msg = "PAUSED - Press Escape to resume";
        int msgX = Math.Max(0, (surface.Width - msg.Length) / 2);
        int msgY = Math.Max(0, surface.Height / 2);

        for (int i = 0; i < msg.Length && msgX + i < surface.Width; i++)
        {
            surface[msgX + i, msgY].Glyph = msg[i];
            surface[msgX + i, msgY].Foreground = Color.Yellow;
            surface[msgX + i, msgY].Background = Color.DarkBlue;
        }

        _pauseOverlay.IsDirty = true;
    }

    private void DrawMainMenu()
    {
        var surface = _menuOverlay.Surface;
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        const string title = "MonoRogue";
        int titleX = Math.Max(0, (surface.Width - title.Length) / 2);
        int titleY = Math.Max(0, surface.Height / 3);

        for (int i = 0; i < title.Length && titleX + i < surface.Width; i++)
        {
            surface[titleX + i, titleY].Glyph = title[i];
            surface[titleX + i, titleY].Foreground = Color.Yellow;
            surface[titleX + i, titleY].Background = Color.DarkBlue;
        }

        int menuStartY = titleY + 3;
        for (int idx = 0; idx < _menuOptions.Length; idx++)
        {
            var opt = _menuOptions[idx];
            int optX = Math.Max(0, (surface.Width - opt.Length) / 2);
            int y = menuStartY + idx * 2;

            var fg = idx == _menuSelectedIndex ? Color.Black : Color.White;
            var bg = idx == _menuSelectedIndex ? Color.White : Color.Black;

            for (int i = 0; i < opt.Length && optX + i < surface.Width; i++)
            {
                surface[optX + i, y].Glyph = opt[i];
                surface[optX + i, y].Foreground = fg;
                surface[optX + i, y].Background = bg;
            }
        }

        _menuOverlay.IsDirty = true;
    }

    // Adapter that bridges SadConsole keyboard snapshots to the core IInputProvider interface.
    // This keeps the core free of SadConsole types while allowing the UI to decide how keys
    // map to domain-level commands (it may consult the game's current state when mapping).
    private class SadConsoleInputProvider : IInputProvider
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
}
