using System.Text.Json;

namespace MonoRogue.Core;

public static class ContentLoader
{
    public static List<T> LoadDefinitions<T>(string path, string collectionPropertyName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new List<T>();
        }

        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(collectionPropertyName, out var collectionElement) ||
                collectionElement.ValueKind != JsonValueKind.Array)
            {
                return new List<T>();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var results = new List<T>();
            foreach (var element in collectionElement.EnumerateArray())
            {
                var item = JsonSerializer.Deserialize<T>(element.GetRawText(), options);
                if (item is not null)
                {
                    results.Add(item);
                }
            }

            return results;
        }
        catch
        {
            return new List<T>();
        }
    }

    public static List<T> LoadDefinitionsFromDefaultSearchPaths<T>(string fileName, string collectionPropertyName, string? baseDirectory = null)
    {
        var searchPaths = new List<string>();

        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            searchPaths.Add(Path.Combine(baseDirectory, fileName));
        }

        searchPaths.Add(Path.Combine(AppContext.BaseDirectory, fileName));
        searchPaths.Add(Path.Combine(Directory.GetCurrentDirectory(), fileName));

        foreach (var searchPath in searchPaths.Distinct())
        {
            if (File.Exists(searchPath))
            {
                var definitions = LoadDefinitions<T>(searchPath, collectionPropertyName);
                if (definitions.Count > 0)
                {
                    return definitions;
                }
            }
        }

        return new List<T>();
    }
}
