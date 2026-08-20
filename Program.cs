using SadConsole;
using SadConsole.Configuration;
using MonoRogue.Core;

public static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // Run lightweight in-project verification tests when requested.
        if (args.Contains("--test-content"))
        {
            MonoRogue.Tests.MonsterDataLoaderTests.RunAll();
            System.Console.WriteLine("Content loader tests passed.");
            return;
        }

        // Create the game main logic object
        var gameMain = new GameMain();


        // setup the SadConsole game engine and create the main window.
        Settings.WindowTitle = "MonoRogue Game";

        Builder.GetBuilder()
                .ConfigureFonts(true)
                .SetWindowSizeInCells(GameSettings.GAME_WIDTH, GameSettings.GAME_HEIGHT)
                .SetStartingScreen(_ =>
                {
                    // create UI mapper from the UI assembly and pass it into the RootScreen
                    var mapper = new SadConsoleGlyphMapper();
                    return new RootScreen(gameMain, mapper);
                })
                .IsStartingScreenFocused(true)
                .Run();

    }
}
