using MonoRogue.Core;
using SadConsole;
using SadConsole.Input;
using Game = SadConsole.Game;
using Color = SadRogue.Primitives.Color;
using Console = System.Console;

namespace MonoRogue.UI;

public class RootScreen : ScreenObject
{
    private GameMain _game;
    private MapBase _map;
    private ScreenSurface _mapSurface;
    private ScreenSurface _pauseOverlay;
    private ScreenSurface _menuOverlay;
    private ScreenSurface _rightPanel;
    private ScreenSurface _messageConsole;
    private ScreenSurface _gameOverOverlay;
    private int _menuSelectedIndex = 0;
    private readonly string[] _menuOptions = ["New Game", "Save Map", "Load Map", "Exit Game"];
    private readonly List<string> _messages = new List<string>();


    public RootScreen(GameMain game)
    {
        _game = game;

        UseKeyboard = true;

        // Layout sizes
        int totalWidth = Game.Instance.ScreenCellsX;
        int totalHeight = Game.Instance.ScreenCellsY;
        int mapWidth = Math.Max(10, totalWidth - GameSettings.RIGHT_PANEL_WIDTH);
        int mapHeight = Math.Max(8, totalHeight - GameSettings.BOTTOM_CONSOLE_HEIGHT);

        // Create the main map (left area)
        _map = new MapBase(mapWidth, mapHeight);
        _mapSurface = new ScreenSurface(mapWidth, mapHeight);
        _mapSurface.UseMouse = false;
        _mapSurface.UseKeyboard = false;
        DrawMap();

        // Ensure the map surface does not steal keyboard focus from this screen
        Children.Add(_mapSurface);

        // Create right-hand panel for player stats/inventory and position it to the right of the map
        _rightPanel = new ScreenSurface(GameSettings.RIGHT_PANEL_WIDTH, totalHeight);
        _rightPanel.UseMouse = false;
        _rightPanel.UseKeyboard = false;
        _rightPanel.Position = new SadRogue.Primitives.Point(mapWidth, 0);
        DrawRightPanel();
        Children.Add(_rightPanel);

        // Create a bottom message console that spans map width and sits under the map
        _messageConsole = new ScreenSurface(mapWidth, GameSettings.BOTTOM_CONSOLE_HEIGHT);
        _messageConsole.UseMouse = false;
        _messageConsole.UseKeyboard = false;
        _messageConsole.Position = new SadRogue.Primitives.Point(0, mapHeight);
        DrawMessageConsole();
        Children.Add(_messageConsole);

        // Create a pause overlay that will be shown when the game is paused
        _pauseOverlay = new ScreenSurface(totalWidth, totalHeight);
        _pauseOverlay.UseMouse = false;
        _pauseOverlay.UseKeyboard = false;
        _pauseOverlay.IsVisible = false;
        DrawPauseMessage();
        Children.Add(_pauseOverlay);

        // Create a game-over overlay that will be shown when the player dies
        _gameOverOverlay = new ScreenSurface(totalWidth, totalHeight);
        _gameOverOverlay.UseMouse = false;
        _gameOverOverlay.UseKeyboard = false;
        _gameOverOverlay.IsVisible = false;
        DrawGameOverMessage();
        Children.Add(_gameOverOverlay);

        // Create a menu overlay and draw the main menu; it will be visible when GameState == MainMenu
        _menuOverlay = new ScreenSurface(totalWidth, totalHeight);
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
                        DrawMap();
                        DrawRightPanel();
                    }
                    else if (choice == "Save Map")
                    {
                        try
                        {
                            MapPersistenceHelpers.SaveToFile(_map, "saved_map.json");
                            AppendMessage($"Saved map. Active effects: {_map.GetActiveEffectCount()}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to save map: {ex.Message}");
                            AppendMessage("Failed to save map.");
                        }
                        handled = true;
                    }
                    else if (choice == "Load Map")
                    {
                        try
                        {
                            var loaded = MapPersistenceHelpers.LoadIntoWorld(_map, "saved_map.json");
                            if (!loaded)
                            {
                                AppendMessage("No saved map found.");
                                handled = true;
                                break;
                            }

                            DrawMap();
                            DrawRightPanel();
                            AppendMessage($"Loaded map. Restored active effects: {_map.GetActiveEffectCount()}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to load map: {ex.Message}");
                            AppendMessage("Failed to load map.");
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
                {
                    var turnResult = _map.ProcessPlayerTurn(cmd.Delta);
                    DrawMap();
                    DrawRightPanel();
                    var direction = ToDirectionText(cmd.Delta);
                    if (turnResult.PlayerAttacked)
                    {
                        AppendMessage($"You strike for {turnResult.DamageDealt} damage");
                        if (turnResult.MonsterKilled)
                        {
                            AppendMessage("The monster is slain!");
                        }
                    }
                    else if (turnResult.PlayerMoved)
                    {
                        AppendMessage($"You move {direction}");
                        if (turnResult.ItemPickedUp)
                        {
                            AppendMessage($"You pick up {turnResult.ItemPickedUpName}");
                        }
                    }
                    else
                    {
                        AppendMessage($"You are obstructed from moving {direction}");
                    }

                    if (turnResult.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {turnResult.MonsterActionsExecuted}");
                    }

                    if (turnResult.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }
                    handled = true;
                    break;
                }

                case InputType.Rest:
                {
                    var turnResult = _map.ProcessPlayerTurn(SadRogue.Primitives.Point.None);
                    DrawMap();
                    DrawRightPanel();
                    AppendMessage("You rest.");

                    if (turnResult.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {turnResult.MonsterActionsExecuted}");
                    }

                    if (turnResult.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }

                    handled = true;
                    break;
                }

                case InputType.UseItem:
                {
                    var turnResult = _map.ProcessUsePotion();
                    DrawMap();
                    DrawRightPanel();

                    if (turnResult.PotionUsed)
                    {
                        AppendMessage($"You drink a potion and heal {turnResult.HealAmount} HP");
                    }
                    else
                    {
                        AppendMessage("You have no potion to use.");
                    }

                    if (turnResult.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {turnResult.MonsterActionsExecuted}");
                    }

                    if (turnResult.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }

                    handled = true;
                    break;
                }
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


        // Pause overlay: do not draw panel separators here (they are rendered by the right panel and message console)

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

    private void HandlePlayerDeath()
    {
        _game.GameOver();
        DrawGameOverMessage();
        _gameOverOverlay.IsVisible = true;
        AppendMessage("You die...");
    }

    private void DrawGameOverMessage()
    {
        var surface = _gameOverOverlay.Surface;

        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        const string msg = "GAME OVER - You died";
        int msgX = Math.Max(0, (surface.Width - msg.Length) / 2);
        int msgY = Math.Max(0, surface.Height / 2);

        for (int i = 0; i < msg.Length && msgX + i < surface.Width; i++)
        {
            surface[msgX + i, msgY].Glyph = msg[i];
            surface[msgX + i, msgY].Foreground = Color.Red;
            surface[msgX + i, msgY].Background = Color.DarkRed;
        }

        _gameOverOverlay.IsDirty = true;
    }

    private void DrawMap()
    {
        var surface = _mapSurface.Surface;

        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        Color[] colors = new[] { Color.LightGreen, Color.Coral, Color.CornflowerBlue, Color.DarkGreen };
        float[] colorStops = new[] { 0f, 0.35f, 0.75f, 1f };
        Algorithms.GradientFill(_mapSurface.FontSize,
                                surface.Area.Center,
                                Math.Max(1, surface.Width / 3),
                                45,
                                surface.Area,
                                new SadRogue.Primitives.Gradient(colors, colorStops),
                                (x, y, color) => surface[x, y].Background = color);

        var cells = _map.GetRenderSnapshot();
        foreach (var cell in cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= surface.Width || cell.Y >= surface.Height)
            {
                continue;
            }

            surface[cell.X, cell.Y].Glyph = cell.Glyph.Glyph;
            surface[cell.X, cell.Y].Foreground = ColorConverter.FromArgb(cell.Glyph.ForegroundArgb);
            surface[cell.X, cell.Y].Background = ColorConverter.FromArgb(cell.Glyph.BackgroundArgb);
        }

        _mapSurface.IsDirty = true;
    }

    private void DrawRightPanel()
    {
        var surface = _rightPanel.Surface;
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        // Draw vertical separator at the left edge of the right panel to separate map and panel
        for (int y = 0; y < surface.Height; y++)
        {
            surface[0, y].Glyph = '|';
            surface[0, y].Foreground = Color.Gray;
            surface[0, y].Background = Color.Black;
        }

        // Keep column 0 reserved for the separator; render panel content starting at column 1.
        int contentX = 1;
        int contentWidth = Math.Max(0, surface.Width - contentX);

        const string stats = "Stats";
        int sx = contentX + Math.Max(0, (contentWidth - stats.Length) / 2);
        for (int i = 0; i < stats.Length && sx + i < surface.Width; i++)
        {
            surface[sx + i, 0].Glyph = stats[i];
            surface[sx + i, 0].Foreground = Color.Yellow;
            surface[sx + i, 0].Background = Color.DarkBlue;
        }

        // Show a simple player state if present
        var st = _map.ExtractPlayerState();
        var line = 2;
        if (st != null)
        {
            var pos = $"Pos: {st.X},{st.Y}";
            for (int i = 0; i < pos.Length && i < contentWidth; i++) surface[contentX + i, line].Glyph = pos[i];
            line += 2;

            var (hp, maxHp) = _map.GetPlayerHealth();
            var hpText = $"HP: {hp}/{maxHp}";
            for (int i = 0; i < hpText.Length && i < contentWidth; i++) surface[contentX + i, line].Glyph = hpText[i];
            line += 2;

            var goldText = $"Gold: {_map.GetGold()}";
            for (int i = 0; i < goldText.Length && i < contentWidth; i++) surface[contentX + i, line].Glyph = goldText[i];
            line += 2;
        }

        const string inv = "Inventory";
        int ix = contentX + Math.Max(0, (contentWidth - inv.Length) / 2);
        for (int i = 0; i < inv.Length && ix + i < surface.Width; i++)
        {
            surface[ix + i, line].Glyph = inv[i];
            surface[ix + i, line].Foreground = Color.Yellow;
            surface[ix + i, line].Background = Color.DarkBlue;
        }

        // Render inventory stacks below the "Inventory" heading.
        var inventory = _map.Inventory;
        if (inventory.Count == 0)
        {
            const string empty = "(empty)";
            for (int c = 0; c < empty.Length && c < contentWidth; c++) surface[contentX + c, line + 1].Glyph = empty[c];
        }
        else
        {
            for (int r = 0; r < inventory.Count && line + 1 + r < surface.Height; r++)
            {
                var s = $"{inventory[r].Name} x{inventory[r].Count}";
                for (int c = 0; c < s.Length && c < contentWidth; c++) surface[contentX + c, line + 1 + r].Glyph = s[c];
            }
        }

        _rightPanel.IsDirty = true;
    }

    private void DrawMessageConsole()
    {
        var surface = _messageConsole.Surface;
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }

        // Draw a horizontal separator at the top of the message console
        for (int x = 0; x < surface.Width; x++)
        {
            surface[x, 0].Glyph = '_';
            surface[x, 0].Foreground = Color.Gray;
            surface[x, 0].Background = Color.Black;
        }

        // Render messages starting at row 1 (below the separator)
        int maxLines = Math.Max(0, surface.Height - 1);
        int start = Math.Max(0, _messages.Count - maxLines);
        int row = 1;

        for (int i = start; i < _messages.Count; i++)
        {
            var msg = _messages[i];
            for (int c = 0; c < msg.Length && c < surface.Width; c++)
            {
                surface[c, row].Glyph = msg[c];
            }
            row++;

            if (row >= surface.Height) break;
        }

        _messageConsole.IsDirty = true;
    }

    private void AppendMessage(string message)
    {
        _messages.Add(message);

        // keep some reasonable cap
        if (_messages.Count > 100)
        {
            _messages.RemoveAt(0);
        }
        DrawMessageConsole();
    }

    private static string ToDirectionText(SadRogue.Primitives.Point delta)
    {
        switch (delta)
        {
            case { X: 0, Y: < 0 }:
                return "north";

            case { X: 0, Y: > 0 }:
                return "south";

            case { X: > 0, Y: 0 }:
                return "east";

            case { X: < 0, Y: 0 }:
                return "west";

            default:
                return "that way";
        }
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
}
