using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class SaveSystemTests
{
    [Fact]
    public void AutoSave_CreatesSaveAfterEachTurn()
    {
        var savePath = NewTempSavePath();
        try
        {
            var game = new GameMain(savePath);
            game.StartNewGame(20, 15, seed: 42);

            if (game.HasSaveFile())
            {
                throw new InvalidOperationException("Expected no save file immediately after starting a new game.");
            }

            game.ProcessPlayerTurn(Point.None); // rest = one completed turn

            if (!game.HasSaveFile())
            {
                throw new InvalidOperationException("Expected an auto-save after the first turn.");
            }

            // The save must be loadable, proving it is a valid snapshot of the run.
            var continued = game.ContinueGame(20, 15);
            if (!continued)
            {
                throw new InvalidOperationException("Expected the auto-save to be loadable.");
            }
        }
        finally
        {
            CleanupSave(savePath);
        }
    }

    [Fact]
    public void GameOver_DeletesTheAutoSave()
    {
        var savePath = NewTempSavePath();
        try
        {
            var game = new GameMain(savePath);
            game.StartNewGame(20, 15, seed: 42);
            game.ProcessPlayerTurn(Point.None);

            if (!game.HasSaveFile())
            {
                throw new InvalidOperationException("Expected an auto-save before game over.");
            }

            game.GameOver();

            if (game.HasSaveFile())
            {
                throw new InvalidOperationException("Expected the auto-save to be deleted on game over.");
            }
        }
        finally
        {
            CleanupSave(savePath);
        }
    }

    private static string NewTempSavePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
    }

    private static void CleanupSave(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
