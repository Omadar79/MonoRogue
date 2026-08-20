using System.Text.Json.Serialization;

namespace MonoRogue.Core;

public sealed record ItemDefinition(
    string Name,
    char Glyph,
    int ForegroundArgb,
    int BackgroundArgb)
{
    public static ItemDefinition Default(string name, char glyph) =>
        new(name, glyph, unchecked((int)0xFFFFFF00), unchecked((int)0xFF000000));
}

public sealed class ItemDefinitionsFile
{
    [JsonPropertyName("items")]
    public List<ItemDefinition> Items { get; set; } = new();
}

public static class ItemDataLoader
{
    public static List<ItemDefinition> LoadDefinitions(string path)
    {
        return ContentLoader.LoadDefinitions<ItemDefinition>(path, "items")
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new ItemDefinition(
                item.Name.Trim(),
                item.Glyph,
                item.ForegroundArgb,
                item.BackgroundArgb))
            .ToList();
    }

    public static List<ItemDefinition> LoadDefinitionsFromDefaultSearchPaths(string? baseDirectory = null)
    {
        return ContentLoader.LoadDefinitionsFromDefaultSearchPaths<ItemDefinition>("items.json", "items", baseDirectory)
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new ItemDefinition(
                item.Name.Trim(),
                item.Glyph,
                item.ForegroundArgb,
                item.BackgroundArgb))
            .ToList();
    }
}
