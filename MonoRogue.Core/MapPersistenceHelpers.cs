using System;
using System.IO;
using System.Text.Json;

namespace MonoRogue.Core;

/// <summary>
/// Helper class to centralize map persistence (file I/O) logic.
/// Keeps JSON/file I/O out of MapBase so core logic remains UI-agnostic and testable.
/// </summary>
public static class MapPersistenceHelpers
{
    /// <summary>
    /// Serialize a map's DTO to JSON and write it to disk. Throws exceptions on failure.
    /// </summary>
    public static void SaveToFile(MapBase map, string path, IGlyphMapper? mapper = null)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        var mapData = map.SaveMap(mapper);
        var json = JsonSerializer.Serialize(mapData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Read JSON from disk and deserialize a MapData. Returns null if file missing or deserialization fails.
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
    /// Load a map JSON file and populate the supplied MapBase world. Returns true on success.
    /// </summary>
    public static bool LoadIntoWorld(MapBase map, string path, IGlyphMapper? mapper = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        
        var mapData = LoadFromFile(path);
        if (mapData == null)
        {
            return false;
        }
        
        map.LoadMap(mapData, mapper);
        return true;
    }
}

