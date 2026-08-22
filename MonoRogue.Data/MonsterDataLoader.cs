using System.Text.Json.Serialization;

namespace MonoRogue.Data;

public sealed record MonsterDefinition(
    string Name,
    char Glyph,
    int ForegroundArgb,
    int BackgroundArgb,
    int GainPerTurn,
    int ActionCost,
    int Damage)
{
    public static MonsterDefinition Default(string name, char glyph) =>
        new(name, glyph, unchecked((int)0xFFFF0000), unchecked((int)0xFF000000), 100, 100, 3);
}

public sealed class MonsterDefinitionsFile
{
    [JsonPropertyName("monsters")]
    public List<MonsterDefinition> Monsters { get; set; } = new();
}

public static class MonsterDataLoader
{
    public static List<MonsterDefinition> LoadDefinitions(string path)
    {
        var definitions = ContentLoader.LoadDefinitions<MonsterDefinition>(path, "monsters");
        return definitions
            .Where(m => !string.IsNullOrWhiteSpace(m.Name))
            .Select(m => new MonsterDefinition(
                m.Name.Trim(),
                m.Glyph,
                m.ForegroundArgb,
                m.BackgroundArgb,
                Math.Max(1, m.GainPerTurn),
                Math.Max(1, m.ActionCost),
                Math.Max(1, m.Damage)))
            .ToList();
    }

    public static List<MonsterDefinition> LoadDefinitionsFromDefaultSearchPaths(string? baseDirectory = null)
    {
        return ContentLoader.LoadDefinitionsFromDefaultSearchPaths<MonsterDefinition>("monsters.json", "monsters", baseDirectory)
            .Where(m => !string.IsNullOrWhiteSpace(m.Name))
            .Select(m => new MonsterDefinition(
                m.Name.Trim(),
                m.Glyph,
                m.ForegroundArgb,
                m.BackgroundArgb,
                Math.Max(1, m.GainPerTurn),
                Math.Max(1, m.ActionCost),
                Math.Max(1, m.Damage)))
            .ToList();
    }
}
