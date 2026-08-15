using SadConsole;
using Game = SadConsole.Game;
using SadConsole.Input;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.Core;
public class RootScreen : ScreenObject
{
    private  GameMain _game;
    private MapBase _map;
    private ScreenSurface _pauseOverlay;
    private ScreenSurface _menuOverlay;
    private int _menuSelectedIndex = 0;
    private readonly string[] _menuOptions = new[] { "New Game", "Exit Game" };

    public RootScreen(GameMain game)
    {
        _game = game;

        UseKeyboard = true;

        // Create the main map 
        _map = new MapBase(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY - 5);
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
                    return true;
            }
        }

        bool handled = false;

        var commands = _game.ProcessKeyboard(keyboard);
        foreach (var cmd in commands)
        {
            switch (cmd.Type)
            {
                case GameMain.InputType.TogglePause:
                    _game.TogglePause();

                    // Show or hide the pause overlay based on game state
                    _pauseOverlay.IsVisible = (_game.CurrentState == GameState.Paused);

                    if (_pauseOverlay.IsVisible)
                    {
                        DrawPauseMessage();
                    }
                    handled = true;
                    break;

                case GameMain.InputType.Move:
                    _map.TryMovePlayer(cmd.Delta);
                    handled = true;
                    break;
            }
        }

        // If we're at the main menu, handle menu navigation here
        if (_game.CurrentState == GameState.MainMenu)
        {
            bool handledMenu = false;

            if (keyboard.IsKeyPressed(Keys.Up))
            {
                _menuSelectedIndex = (_menuSelectedIndex - 1 + _menuOptions.Length) % _menuOptions.Length;
                DrawMainMenu();
                handledMenu = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Down))
            {
                _menuSelectedIndex = (_menuSelectedIndex + 1) % _menuOptions.Length;
                DrawMainMenu();
                handledMenu = true;
            }
            else if (keyboard.IsKeyPressed(Keys.Enter))
            {
                var choice = _menuOptions[_menuSelectedIndex];
                if (choice == "New Game")
                {
                    _game.StartGame();
                    _menuOverlay.IsVisible = false;
                    handledMenu = true;
                }
                else if (choice == "Exit Game")
                {
                    Environment.Exit(0);
                }
            }

            if (handledMenu) return true;
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

        var msg = "PAUSED - Press Escape to resume";
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

        var title = "MonoRogue";
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
}
