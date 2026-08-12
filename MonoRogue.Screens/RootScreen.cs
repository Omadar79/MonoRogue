using Microsoft.Xna.Framework;
using SadConsole;
using Game = SadConsole.Game;
using SadConsole.Input;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;
using Point = SadRogue.Primitives.Point;

namespace MonoRogue.Core;

public class RootScreen : ScreenObject
{
    private  GameMain _game;
    private Map _map;
    private SadConsole.ScreenSurface _pauseOverlay;

    public RootScreen(GameMain game)
    {
        _game = game;
        _map = new Map(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY - 5);
        Children.Add(_map.SurfaceObject);
        // Create a pause overlay that will be shown when the game is paused
        _pauseOverlay = new SadConsole.ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);
        _pauseOverlay.UseMouse = false;
        _pauseOverlay.UseKeyboard = false;
        _pauseOverlay.IsVisible = false;
        DrawPauseMessage();
        Children.Add(_pauseOverlay);
    }

    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        bool handled = false;

        var commands = _game.ProcessKeyboard(keyboard);
        foreach (var cmd in commands)
        {
            switch (cmd.Type)
            {
                case MonoRogue.Core.GameMain.InputType.TogglePause:
                    _game.TogglePause();
                    // Show or hide the pause overlay based on game state
                    _pauseOverlay.IsVisible = (_game.CurrentState == GameState.Paused);
                    if (_pauseOverlay.IsVisible) DrawPauseMessage();
                    handled = true;
                    break;
                case MonoRogue.Core.GameMain.InputType.Move:
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
}
