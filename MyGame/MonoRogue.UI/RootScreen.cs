using MonoRogue.Core;
using SadConsole;
using SadConsole.Input;
using SadConsole.Transitions;
using SadConsole.UI;
using Game = SadConsole.Game;
using Color = SadRogue.Primitives.Color;
using Console = System.Console;

namespace MonoRogue.UI;

public class RootScreen : ScreenObject
{
    private GameMain _game;
    private int _mapWidth;
    private int _mapHeight;
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
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        // The map is owned and created by GameMain (lazily on "New Game"/"Load Map");
        // this screen only renders it and forwards commands.
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
        Border.CreateForSurface(_rightPanel, "Status");
        Children.Add(_rightPanel);

        // Create a bottom message console that spans map width and sits under the map
        _messageConsole = new ScreenSurface(mapWidth, GameSettings.BOTTOM_CONSOLE_HEIGHT);
        _messageConsole.UseMouse = false;
        _messageConsole.UseKeyboard = false;
        _messageConsole.Position = new SadRogue.Primitives.Point(0, mapHeight);
        DrawMessageConsole();
        Border.CreateForSurface(_messageConsole, "Log");
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
                        _game.StartNewGame(_mapWidth, _mapHeight);
                        FadeOut(_menuOverlay);
                        DrawMap();
                        DrawRightPanel();
                    }
                    else if (choice == "Save Map")
                    {
                        try
                        {
                            if (_game.SaveMap("saved_map.json"))
                            {
                                AppendMessage($"Saved map. Active effects: {_game.CurrentMap!.GetActiveEffectCount()}");
                            }
                            else
                            {
                                AppendMessage("No active game to save.");
                            }
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
                            if (_game.LoadMap("saved_map.json", _mapWidth, _mapHeight))
                            {
                                FadeOut(_menuOverlay);
                                DrawMap();
                                DrawRightPanel();
                                AppendMessage($"Loaded map. Restored active effects: {_game.CurrentMap!.GetActiveEffectCount()}");
                            }
                            else
                            {
                                AppendMessage("No saved map found.");
                            }
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
                        Game.Instance.MonoGameInstance.Exit();
                    }
                    handled = true;
                    break;

                case InputType.MenuExit:
                    Game.Instance.MonoGameInstance.Exit();
                    handled = true;
                    break;

                case InputType.Confirm:
                    // Only reachable after a game over: return to the main menu.
                    _game.ReturnToMainMenu();
                    ShowMainMenu();
                    handled = true;
                    break;

                case InputType.TogglePause:
                    _game.TogglePause();

                    // Fade the pause overlay in or out based on the game state.
                    if (_game.CurrentState == GameState.Paused)
                    {
                        DrawPauseMessage();
                        _pauseOverlay.IsVisible = true;
                        FadeIn(_pauseOverlay);
                    }
                    else
                    {
                        FadeOut(_pauseOverlay);
                    }
                    handled = true;
                    break;

                case InputType.Move:
                {
                    var turnResult = _game.ProcessPlayerTurn(cmd.Delta);
                    if (!turnResult.HasValue)
                    {
                        handled = true;
                        break;
                    }

                    var tr = turnResult.Value;
                    DrawMap();
                    DrawRightPanel();
                    var direction = ToDirectionText(cmd.Delta);
                    if (tr.PlayerAttacked)
                    {
                        AppendMessage($"You strike for {tr.DamageDealt} damage");
                        if (tr.MonsterKilled)
                        {
                            AppendMessage("The monster is slain!");
                        }
                    }
                    else if (tr.PlayerMoved)
                    {
                        AppendMessage($"You move {direction}");
                        if (tr.ItemPickedUp)
                        {
                            AppendMessage($"You pick up {tr.ItemPickedUpName}");
                        }
                    }
                    else
                    {
                        AppendMessage($"You are obstructed from moving {direction}");
                    }

                    if (tr.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {tr.MonsterActionsExecuted}");
                    }

                    if (tr.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }
                    handled = true;
                    break;
                }

                case InputType.Rest:
                {
                    var turnResult = _game.ProcessPlayerTurn(SadRogue.Primitives.Point.None);
                    if (!turnResult.HasValue)
                    {
                        handled = true;
                        break;
                    }

                    var tr = turnResult.Value;
                    DrawMap();
                    DrawRightPanel();
                    AppendMessage("You rest.");

                    if (tr.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {tr.MonsterActionsExecuted}");
                    }

                    if (tr.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }

                    handled = true;
                    break;
                }

                case InputType.UseItem:
                {
                    var turnResult = _game.ProcessUsePotion();
                    if (!turnResult.HasValue)
                    {
                        handled = true;
                        break;
                    }

                    var tr = turnResult.Value;
                    DrawMap();
                    DrawRightPanel();

                    if (tr.PotionUsed)
                    {
                        AppendMessage($"You drink a potion and heal {tr.HealAmount} HP");
                    }
                    else
                    {
                        AppendMessage("You have no potion to use.");
                    }

                    if (tr.MonsterActionsExecuted > 0)
                    {
                        AppendMessage($"Monsters act: {tr.MonsterActionsExecuted}");
                    }

                    if (tr.PlayerDied)
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

    private static void ClearSurface(ScreenSurface screen)
    {
        var surface = screen.Surface;
        for (int y = 0; y < surface.Height; y++)
        {
            for (int x = 0; x < surface.Width; x++)
            {
                surface[x, y].Glyph = ' ';
                surface[x, y].Foreground = Color.White;
                surface[x, y].Background = Color.Black;
            }
        }
    }

    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(200);

    private static void FadeIn(ScreenSurface surface) =>
        surface.SadComponents.Add(new FadeIn(surface, FadeDuration, null));

    private static void FadeOut(ScreenSurface surface) =>
        surface.SadComponents.Add(new FadeOut(surface, FadeDuration, null) { HideObject = true });

    private void DrawPauseMessage()
    {
        ClearSurface(_pauseOverlay);

        const string msg = "PAUSED - Press Escape to resume";
        var surface = _pauseOverlay.Surface;
        _pauseOverlay.Print(Math.Max(0, (surface.Width - msg.Length) / 2), Math.Max(0, surface.Height / 2),
            msg, Color.Yellow, Color.DarkBlue);

        _pauseOverlay.IsDirty = true;
    }

    private void HandlePlayerDeath()
    {
        _game.GameOver();
        DrawGameOverMessage();
        _gameOverOverlay.IsVisible = true;
        FadeIn(_gameOverOverlay);
        AppendMessage("You die...");
    }

    private void ShowMainMenu()
    {
        _gameOverOverlay.IsVisible = false;
        _pauseOverlay.IsVisible = false;
        _messages.Clear();
        DrawMessageConsole();
        DrawMainMenu();
        _menuOverlay.IsVisible = true;
        FadeIn(_menuOverlay);
    }

    private void DrawGameOverMessage()
    {
        ClearSurface(_gameOverOverlay);

        const string msg = "GAME OVER - You died (press Enter to return to menu)";
        var surface = _gameOverOverlay.Surface;
        _gameOverOverlay.Print(Math.Max(0, (surface.Width - msg.Length) / 2), Math.Max(0, surface.Height / 2),
            msg, Color.Red, Color.DarkRed);

        _gameOverOverlay.IsDirty = true;
    }

    private void DrawMap()
    {
        var surface = _mapSurface.Surface;

        ClearSurface(_mapSurface);

        Color[] colors = new[] { Color.LightGreen, Color.Coral, Color.CornflowerBlue, Color.DarkGreen };
        float[] colorStops = new[] { 0f, 0.35f, 0.75f, 1f };
        Algorithms.GradientFill(_mapSurface.FontSize,
                                surface.Area.Center,
                                Math.Max(1, surface.Width / 3),
                                45,
                                surface.Area,
                                new SadRogue.Primitives.Gradient(colors, colorStops),
                                (x, y, color) => surface[x, y].Background = color);

        var map = _game.CurrentMap;
        if (map != null)
        {
            var cells = map.GetRenderSnapshot();
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
        }

        _mapSurface.IsDirty = true;
    }

    private void DrawRightPanel()
    {
        var surface = _rightPanel.Surface;
        ClearSurface(_rightPanel);

        // Content is inset by one cell on every side so it stays inside the border
        // that was added around this panel in the constructor.
        int contentX = 1;
        int contentWidth = Math.Max(0, surface.Width - contentX - 1);
        var line = 1;

        // Show a simple player state if present
        var map = _game.CurrentMap;
        var st = map?.ExtractPlayerState();
        if (st != null)
        {
            _rightPanel.Print(contentX, line, $"Pos: {st.X},{st.Y}");
            line += 2;

            var (hp, maxHp) = map!.GetPlayerHealth();
            _rightPanel.Print(contentX, line, $"HP: {hp}/{maxHp}");
            line += 2;

            _rightPanel.Print(contentX, line, $"Gold: {map!.GetGold()}");
            line += 2;
        }

        const string inv = "Inventory";
        _rightPanel.Print(contentX + Math.Max(0, (contentWidth - inv.Length) / 2), line,
            inv, Color.Yellow, Color.DarkBlue);

        // Render inventory stacks below the "Inventory" heading.
        var inventory = map?.Inventory;
        if (inventory == null || inventory.Count == 0)
        {
            _rightPanel.Print(contentX, line + 1, "(empty)");
        }
        else
        {
            for (int r = 0; r < inventory.Count && line + 1 + r < surface.Height - 1; r++)
            {
                _rightPanel.Print(contentX, line + 1 + r, $"{inventory[r].Name} x{inventory[r].Count}");
            }
        }

        _rightPanel.IsDirty = true;
    }

    private void DrawMessageConsole()
    {
        var surface = _messageConsole.Surface;
        ClearSurface(_messageConsole);

        // Render messages inside the border that was added around this console in the constructor.
        int maxLines = Math.Max(0, surface.Height - 2);
        int start = Math.Max(0, _messages.Count - maxLines);
        int row = 1;

        for (int i = start; i < _messages.Count; i++)
        {
            _messageConsole.Print(1, row, _messages[i]);
            row++;

            if (row > surface.Height - 2) break;
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
        ClearSurface(_menuOverlay);

        const string title = "MonoRogue";
        int titleX = Math.Max(0, (surface.Width - title.Length) / 2);
        int titleY = Math.Max(0, surface.Height / 3);
        _menuOverlay.Print(titleX, titleY, title, Color.Yellow, Color.DarkBlue);

        int menuStartY = titleY + 3;
        for (int idx = 0; idx < _menuOptions.Length; idx++)
        {
            var opt = _menuOptions[idx];
            int optX = Math.Max(0, (surface.Width - opt.Length) / 2);
            int y = menuStartY + idx * 2;

            var fg = idx == _menuSelectedIndex ? Color.Black : Color.White;
            var bg = idx == _menuSelectedIndex ? Color.White : Color.Black;
            _menuOverlay.Print(optX, y, opt, fg, bg);
        }

        _menuOverlay.IsDirty = true;
    }
}
