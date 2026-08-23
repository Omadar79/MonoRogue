using System;
using System.IO;
using System.Text.Json;

namespace MonoRogue.Core;

/// <summary>
/// Helper class to centralize session persistence (file I/O) logic.
/// Keeps JSON/file I/O out of GameSession so core-logic remains UI-agnostic and testable.
/// </summary>
public static class MapPersistenceHelpers
{
    /// <summary>
    /// Serialize a session's DTO to JSON and write it to disk. Throws exceptions on failure.
    /// </summary>
    public static void SaveToFile(GameSession session, string path)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        var mapData = session.SaveMap();
        var json = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Read JSON from disk and deserialize a MapData. Returns null if the file is missing or deserialization fails.
    /// </summary>
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

    /// <summary>
    /// Load a session JSON file and populate the supplied GameSession world. Returns true on success.
    /// </summary>
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
}

