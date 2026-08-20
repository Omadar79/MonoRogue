using MonoRogue.Data;

namespace MonoRogue.Tests;

public static class MonsterDataLoaderTests
{
    public static void RunAll()
    {
        LoadDefinitions_FromJsonFile_ReturnsMonsterDefinitions();
        LoadItemDefinitions_FromJsonFile_ReturnsItemDefinitions();
    }

    public static void LoadDefinitions_FromJsonFile_ReturnsMonsterDefinitions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "monsters.json");
        File.WriteAllText(path, """
{
  "monsters": [
    {
      "name": "goblin",
      "glyph": "g",
      "foregroundArgb": -65536,
      "backgroundArgb": -16777216,
      "gainPerTurn": 100,
      "actionCost": 100
    }
  ]
}
""");

        var definitions = MonsterDataLoader.LoadDefinitions(path);

        if (definitions.Count != 1)
        {
            throw new InvalidOperationException($"Expected 1 monster definition but got {definitions.Count}.");
        }

        if (definitions[0].Name != "goblin") throw new InvalidOperationException("Expected goblin name.");
        if (definitions[0].Glyph != 'g') throw new InvalidOperationException("Expected goblin glyph.");
        if (definitions[0].GainPerTurn != 100) throw new InvalidOperationException("Expected gainPerTurn of 100.");
        if (definitions[0].ActionCost != 100) throw new InvalidOperationException("Expected actionCost of 100.");
    }

    public static void LoadItemDefinitions_FromJsonFile_ReturnsItemDefinitions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "items.json");
        File.WriteAllText(path, """
{
  "items": [
    {
      "name": "potion",
      "glyph": "!",
      "foregroundArgb": -16711936,
      "backgroundArgb": -16777216
    }
  ]
}
""");

        var definitions = ItemDataLoader.LoadDefinitions(path);

        if (definitions.Count != 1)
        {
            throw new InvalidOperationException($"Expected 1 item definition but got {definitions.Count}.");
        }

        if (definitions[0].Name != "potion") throw new InvalidOperationException("Expected potion name.");
        if (definitions[0].Glyph != '!') throw new InvalidOperationException("Expected potion glyph.");
        if (definitions[0].ForegroundArgb != -16711936) throw new InvalidOperationException("Expected green foreground.");
        if (definitions[0].BackgroundArgb != -16777216) throw new InvalidOperationException("Expected black background.");
    }
}
