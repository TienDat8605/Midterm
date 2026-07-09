using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Provides access to all handcrafted map definitions loaded from .md files.
/// New maps can be added by placing a .md file in the maps directory — no code changes needed.
/// </summary>
public static class LevelDefinitions
{
    private static List<MapData> _allMaps;
    private static readonly object _lock = new object();

    /// <summary>
    /// Path to the maps directory, relative to the project root.
    /// </summary>
    private static string MapsDirectory
    {
        get
        {
            // Application.dataPath = /path/to/Assets
            return Path.Combine(Application.dataPath, "MapAscii");
        }
    }

    /// <summary>
    /// Returns all available maps, loaded from .md files on first access.
    /// Subsequent calls return the cached list.
    /// </summary>
    public static List<MapData> AllMaps
    {
        get
        {
            if (_allMaps == null)
            {
                lock (_lock)
                {
                    if (_allMaps == null)
                    {
                        _allMaps = MapData.LoadAllFromDirectory(MapsDirectory);

                        if (_allMaps.Count == 0)
                        {
                            Debug.LogWarning("[LevelDefinitions] No map files found. " +
                                $"Expected .md files in: {MapsDirectory}");
                        }
                    }
                }
            }
            return new List<MapData>(_allMaps);
        }
    }

    /// <summary>
    /// Force-reload maps from disk. Call after adding/editing .md files while the editor is open.
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _allMaps = null;
        }
    }
}
