using MonoRogue.Core;
using SadConsole;
using SadConsole.Input;
using SadConsole.Transitions;
using SadConsole.UI;
using Game = SadConsole.Game;
using Color = SadRogue.Primitives.Color;
using Console = System.Console;

namespace MonoRogue.UI;

/// <summary>
/// Root screen that handles rendering and input for the entire game. This is the root of the SadConsole UI hierarchy
/// and owns the various subsurface for the map, right panel, message console, and overlays. 
/// </summary>
public class RootScreen : ScreenObject
{
    private GameMain _game;  // Since we're in the UI, this is our reference to the game logic layer to forward input and render state.
    private int _mapWidth;
    private int _mapHeight;
    private ScreenSurface _mapSurface;
    private ScreenSurface _pauseOverlay;
    private ScreenSurface _menuOverlay;
    private ScreenSurface _rightPanel;
    private ScreenSurface _messageConsole;
    private ScreenSurface _gameOverOverlay;
    private ScreenSurface _inventoryOverlay;
    private int _inventorySelectedIndex = 0;
    private int _menuSelectedIndex = 0;
    private string[] _menuOptions = [];
    private readonly List<string> _messages = [];
    private int _lastDepth = 1;


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
        RefreshMenuOptions();
        DrawMainMenu();
        _menuOverlay.IsVisible = (_game.GetCurrentState() == GameState.MainMenu);
        Children.Add(_menuOverlay);

        // Create an inventory selection overlay (visible when GameState == Inventory).
        _inventoryOverlay = new ScreenSurface(totalWidth, totalHeight);
        _inventoryOverlay.UseMouse = false;
        _inventoryOverlay.UseKeyboard = false;
        _inventoryOverlay.IsVisible = false;
        DrawInventoryOverlay();
        Children.Add(_inventoryOverlay);
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
                {
                    var choice = _menuOptions[_menuSelectedIndex];
                    if (choice == "New Game")
                    {
                        _game.StartNewGame(_mapWidth, _mapHeight);
                        FadeOut(_menuOverlay);
                        DrawMap();
                        DrawRightPanel();
                    }
                    else if (choice == "Continue")
                    {
                        if (_game.ContinueGame(_mapWidth, _mapHeight))
                        {
                            FadeOut(_menuOverlay);
                            DrawMap();
                            DrawRightPanel();
                            AppendMessage($"Continued. Active effects: {_game.GetCurrentSession()!.GetActiveEffectCount()}");
                        }
                        else
                        {
                            AppendMessage("No valid save found.");
                        }
                    }
                    else if (choice == "Exit Game")
                    {
                        Game.Instance.MonoGameInstance.Exit();
                    }
                    handled = true;
                    break;
                }

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
                    if (_game.GetCurrentState() == GameState.Paused)
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

                case InputType.Quit:
                    Game.Instance.MonoGameInstance.Exit();
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
                    if (tr.LevelChanged)
                    {
                        AppendMessage(tr.Depth > _lastDepth
                            ? $"You descend to depth {tr.Depth}"
                            : $"You ascend to depth {tr.Depth}");
                        _lastDepth = tr.Depth;
                    }
                    else if (tr.PlayerAttacked)
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

                    if (tr.ExperienceGained > 0)
                    {
                        AppendMessage($"You gain {tr.ExperienceGained} experience.");
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

                    if (tr.ExperienceGained > 0)
                    {
                        AppendMessage($"You gain {tr.ExperienceGained} experience.");
                    }

                    if (tr.PlayerDied)
                    {
                        HandlePlayerDeath();
                    }

                    handled = true;
                    break;
                }

                case InputType.OpenInventory:
                    _game.OpenInventory();
                    _inventorySelectedIndex = 0;
                    DrawInventoryOverlay();
                    _inventoryOverlay.IsVisible = true;
                    FadeIn(_inventoryOverlay);
                    handled = true;
                    break;

                case InputType.InventoryUp:
                    MoveInventorySelection(-1);
                    handled = true;
                    break;

                case InputType.InventoryDown:
                    MoveInventorySelection(1);
                    handled = true;
                    break;

                case InputType.InventoryCancel:
                    _game.CloseInventory();
                    FadeOut(_inventoryOverlay);
                    handled = true;
                    break;

                case InputType.InventorySelect:
                    UseSelectedInventoryItem();
                    handled = true;
                    break;
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

    // Draws a frame border inside the surface's own outer cells (rather than SadConsole's
    // Border.CreateForSurface, which draws 1 cell outside and overlaps adjacent surfaces).
    // This keeps the map viewport fully visible with no clipping.
    private static void DrawBoxBorder(ScreenSurface screen, string title, Color? titleColor = null)
    {
        var surface = screen.Surface;
        int width = surface.Width;
        int height = surface.Height;
        if (width < 2 || height < 2)
        {
            return;
        }

        var borderColor = Color.Gray;
        var headingColor = titleColor ?? borderColor;

        // Corners.
        surface[0, 0].Glyph = '+';
        surface[width - 1, 0].Glyph = '+';
        surface[0, height - 1].Glyph = '+';
        surface[width - 1, height - 1].Glyph = '+';

        // Horizontal edges.
        for (int x = 1; x < width - 1; x++)
        {
            surface[x, 0].Glyph = '-';
            surface[x, height - 1].Glyph = '-';
        }

        // Vertical edges.
        for (int y = 1; y < height - 1; y++)
        {
            surface[0, y].Glyph = '|';
            surface[width - 1, y].Glyph = '|';
        }

        // Tint the frame.
        for (int x = 0; x < width; x++)
        {
            surface[x, 0].Foreground = borderColor;
            surface[x, height - 1].Foreground = borderColor;
        }
        for (int y = 0; y < height; y++)
        {
            surface[0, y].Foreground = borderColor;
            surface[width - 1, y].Foreground = borderColor;
        }

        // Title rendered into the top edge.
        if (!string.IsNullOrEmpty(title) && width > 3)
        {
            screen.Print(2, 0, title, headingColor, Color.Black);
        }
    }
    
    private static void FadeIn(ScreenSurface surface, long duration = 200)
    {
        var fadeDuration = TimeSpan.FromMilliseconds(duration);
        surface.SadComponents.Add(new FadeIn(surface, fadeDuration, null));
    }

    private static void FadeOut(ScreenSurface surface, long duration = 200)
    {
        var fadeDuration = TimeSpan.FromMilliseconds(duration);
        surface.SadComponents.Add(new FadeOut(surface, fadeDuration, null) { HideObject = true });
    }
    
    private void DrawPauseMessage()
    {
        ClearSurface(_pauseOverlay);

        const string msg = "PAUSED - Esc: resume   Q: quit";
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
        _inventoryOverlay.IsVisible = false;
        _messages.Clear();
        DrawMessageConsole();
        RefreshMenuOptions();
        DrawMainMenu();
        _menuOverlay.IsVisible = true;
        FadeIn(_menuOverlay,0);
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

        Color[] colors = [Color.LightGreen, Color.Coral, Color.CornflowerBlue, Color.DarkGreen];
        float[] colorStops = [0f, 0.35f, 0.75f, 1f];
        Algorithms.GradientFill(_mapSurface.FontSize,
                                surface.Area.Center,
                                Math.Max(1, surface.Width / 3),
                                45,
                                surface.Area,
                                new SadRogue.Primitives.Gradient(colors, colorStops),
                                (x, y, color) => surface[x, y].Background = color);

        var map = _game.GetCurrentSession();
        if (map != null)
        {
            var viewWidth = surface.Width;
            var viewHeight = surface.Height;

            // Center the camera on the player, clamped so the view stays within the world.
            var player = map.ExtractPlayerState();
            var focus = player != null ? new SadRogue.Primitives.Point(player.X, player.Y) : new SadRogue.Primitives.Point(0, 0);
            var topLeft = Camera.ComputeTopLeft(viewWidth, viewHeight, map.GetWidth(), map.GetHeight(), focus);

            var cells = map.GetRenderSnapshot();
            foreach (var cell in cells)
            {
                var screenX = cell.X - topLeft.X;
                var screenY = cell.Y - topLeft.Y;

                if (screenX < 0 || screenY < 0 || screenX >= viewWidth || screenY >= viewHeight)
                {
                    continue;
                }

                if (cell.Visibility == CellVisibility.Hidden)
                {
                    surface[screenX, screenY].Glyph = ' ';
                    surface[screenX, screenY].Foreground = Color.Black;
                    surface[screenX, screenY].Background = Color.Black;
                    continue;
                }

                var foreground = ColorConverter.FromArgb(cell.Glyph.ForegroundArgb);
                var background = ColorConverter.FromArgb(cell.Glyph.BackgroundArgb);

                // Dim remembered (explored but not currently visible) cells to half brightness.
                if (cell.Visibility == CellVisibility.Explored)
                {
                    foreground = new Color(
                        (byte)(foreground.R / 2),
                        (byte)(foreground.G / 2),
                        (byte)(foreground.B / 2),
                        foreground.A);
                    background = Color.Black;
                }

                surface[screenX, screenY].Glyph = cell.Glyph.Glyph;
                surface[screenX, screenY].Foreground = foreground;
                surface[screenX, screenY].Background = background;
            }
        }

        _mapSurface.IsDirty = true;
    }

    private void DrawRightPanel()
    {
        var surface = _rightPanel.Surface;
        ClearSurface(_rightPanel);
        DrawBoxBorder(_rightPanel, "Status", Color.Yellow);

        // Content is inset by one cell on every side, so it stays inside the border.
        var contentX = 1;
        var contentWidth = Math.Max(0, surface.Width - contentX - 1);
        var line = 1;

        // Show a simple player state if present
        var map = _game.GetCurrentSession();
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

            _rightPanel.Print(contentX, line, $"Level: {map!.GetPlayerLevel()}");
            line += 2;

            _rightPanel.Print(contentX, line, $"Depth: {map!.GetDepth()}/{map!.GetMaxDepth()}");
            line += 2;

            var xp = map.GetExperience();
            var nextXp = map.GetXpForNextLevel();
            _rightPanel.Print(contentX, line, nextXp > 0 ? $"XP: {xp}/{nextXp}" : $"XP: {xp}");
            line += 2;
        }

        const string inv = "Inventory";
        _rightPanel.Print(contentX + Math.Max(0, (contentWidth - inv.Length) / 2), line,
            inv, Color.Yellow, Color.DarkBlue);

        // Render inventory stacks below the "Inventory" heading.
        var inventory = map?.GetInventory();
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
        DrawBoxBorder(_messageConsole, "Log", Color.Cyan);

        // Render messages inside the border.
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

    private int GetInventoryCount()
    {
        return _game.GetCurrentSession()?.GetInventory()?.Count ?? 0;
    }

    private void MoveInventorySelection(int delta)
    {
        var count = GetInventoryCount();
        if (count == 0)
        {
            return;
        }

        _inventorySelectedIndex = ((_inventorySelectedIndex + delta) % count + count) % count;
        DrawInventoryOverlay();
    }

    private void UseSelectedInventoryItem()
    {
        var map = _game.GetCurrentSession();
        var inventory = map?.GetInventory();

        _game.CloseInventory();
        FadeOut(_inventoryOverlay);

        if (map == null || inventory == null || inventory.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(_inventorySelectedIndex, 0, inventory.Count - 1);
        var turnResult = _game.UseItemAt(index);

        if (!turnResult.HasValue)
        {
            return;
        }

        var tr = turnResult.Value;
        DrawMap();
        DrawRightPanel();

        if (tr.PotionUsed)
        {
            AppendMessage($"You use {tr.UsedItemName} and heal {tr.HealAmount} HP");
            Console.WriteLine($"You use {tr.UsedItemName} and heal {tr.HealAmount} HP");
        }
        else
        {
            AppendMessage("You can't use that item.");
            Console.WriteLine("You can't use that item.");
        }

        if (tr.MonsterActionsExecuted > 0)
        {
            AppendMessage($"Monsters act: {tr.MonsterActionsExecuted}");
        }

        if (tr.ExperienceGained > 0)
        {
            AppendMessage($"You gain {tr.ExperienceGained} experience.");
        }

        if (tr.PlayerDied)
        {
            HandlePlayerDeath();
        }
    }

    private void DrawInventoryOverlay()
    {
        var surface = _inventoryOverlay.Surface;
        ClearSurface(_inventoryOverlay);

        const string title = "Inventory";
        int titleX = Math.Max(0, (surface.Width - title.Length) / 2);
        int titleY = Math.Max(0, surface.Height / 4);
        _inventoryOverlay.Print(titleX, titleY, title, Color.Yellow, Color.DarkBlue);

        var inventory = _game.GetCurrentSession()?.GetInventory();
        int listStartY = titleY + 2;

        if (inventory == null || inventory.Count == 0)
        {
            const string empty = "(empty)";
            _inventoryOverlay.Print(Math.Max(0, (surface.Width - empty.Length) / 2), listStartY, empty);
        }
        else
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                var text = $"{inventory[i].Name} x{inventory[i].Count}";
                int x = Math.Max(0, (surface.Width - text.Length) / 2);
                int y = listStartY + i;

                var fg = i == _inventorySelectedIndex ? Color.Black : Color.White;
                var bg = i == _inventorySelectedIndex ? Color.White : Color.Black;
                _inventoryOverlay.Print(x, y, text, fg, bg);
            }
        }

        const string hint = "Up/Down: choose   Enter: use   Esc: close";
        int hintY = Math.Max(0, surface.Height - 3);
        _inventoryOverlay.Print(Math.Max(0, (surface.Width - hint.Length) / 2), hintY, hint, Color.Gray, Color.Black);

        _inventoryOverlay.IsDirty = true;
    }

    // Rebuilds the main-menu options based on whether a continue-able auto-save exists.
    private void RefreshMenuOptions()
    {
        _menuOptions = _game.HasSaveFile()
            ? ["Continue", "New Game", "Exit Game"]
            : ["New Game", "Exit Game"];

        if (_menuSelectedIndex >= _menuOptions.Length)
        {
            _menuSelectedIndex = 0;
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
