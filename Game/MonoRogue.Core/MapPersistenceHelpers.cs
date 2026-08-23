using System.Text.Json;

namespace MonoRogue.Core;

/// <summary>
/// Helper class to centralize session persistence (file I/O) logic. Keeps JSON/file I/O out of GameSession so core-logic
/// remains UI-agnostic and testable.
/// </summary>
public static class MapPersistenceHelpers
{

    // Serialize a session's DTO to JSON and write it to disk. Throws exceptions on failure.
    public static void SaveToFile(GameSession session, string path)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        var mapData = session.SaveMap();
        var json = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }


    // Read JSON from disk and deserialize a MapData. Returns null if the file is missing or deserialization fails.
    public static MapData? LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var json = File.ReadAllText(path);
            var mapData = JsonSerializer.Deserialize<MapData>(json);
            return mapData;
        }
        catch
        {
            return null;
        }
    }


    // Load a session JSON file and populate the supplied GameSession world. Returns true on success.
    public static bool LoadIntoWorld(GameSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        
        var mapData = LoadFromFile(path);
        if (mapData == null)
        {
            return false;
        }
        
        session.LoadMap(mapData);
        return true;
    }

    //Returns true when a save file exists at the given path.
    public static bool SaveFileExists(string path) => File.Exists(path);

    //Deletes the save file at the given path, if present.
    public static void DeleteSave(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }


    // Directory where MonoRogue stores its auto-save. Uses the OS-specific application-data
    // location (Windows: %APPDATA%, Linux/macOS: ~/.config) so saves work across platforms.
    private static string GetSaveDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "MonoRogue");
    }

    // Full path to the auto-save file, creating the save directory on first use.
    public static string GetDefaultSavePath()
    {
        var directory = GetSaveDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "saved_map.json");
    }
}

