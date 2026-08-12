using SadConsole;
using SadConsole.Configuration;
using MonoRogue.Core;

public static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {

        // Create the game main logic object
        var gameMain = new GameMain();
        // Start the game so gameplay input is accepted by default
        gameMain.StartGame();


        // setup the SadConsole game engine and create the main window.
        Settings.WindowTitle = "My MonoRogue Game";

        Builder.GetBuilder()
                .ConfigureFonts(true)
                .SetWindowSizeInCells(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT)
                .SetStartingScreen(rootScreen => new RootScreen(gameMain))
                .IsStartingScreenFocused(true)
                .Run();

    }
}
