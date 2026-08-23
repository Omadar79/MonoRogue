using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoRogue.Data;

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
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() }
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
        // Ordered from most-correct to least-correct so a deployed app reads its own
        // content directory (AppContext.BaseDirectory) before falling back to
        // developer-friendly locations (current working directory and repo root).
        var searchPaths = new List<string>();

        void AddRoot(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            searchPaths.Add(Path.Combine(root, fileName));
            searchPaths.Add(Path.Combine(root, "Data", fileName));
        }

        AddRoot(baseDirectory);
        AddRoot(AppContext.BaseDirectory);
        AddRoot(Directory.GetCurrentDirectory());
        AddRoot(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..")));

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
