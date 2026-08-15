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


        // setup the SadConsole game engine and create the main window.
        Settings.WindowTitle = "My MonoRogue Game";

        Builder.GetBuilder()
                .ConfigureFonts(true)
                .SetWindowSizeInCells(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT)
                .SetStartingScreen(_ => new RootScreen(gameMain))
                .IsStartingScreenFocused(true)
                .Run();

    }
}
