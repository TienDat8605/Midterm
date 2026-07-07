using UnityEngine;

/// <summary>
/// Represents a handcrafted Jump King-style map as a grid of characters.
/// Each character maps to a tile type (see legend below).
/// </summary>
[System.Serializable]
public class MapData
{
    [SerializeField] private string[] rows;
    [SerializeField] private string mapName;

    public string[] Rows => rows;
    public string MapName => mapName;
    public int Width => rows != null && rows.Length > 0 ? rows[0].Length : 0;
    public int Height => rows != null ? rows.Length : 0;

    /// <summary>
    /// Legend:
    ///   # = Ground tile
    ///   . = Empty
    ///   S = Spawn point (ground placed underneath)
    ///   G = Goal point
    ///   C = Checkpoint (future)
    ///   R = Recovery platform
    ///   D = Decoration (future)
    ///   L = Ladder (future)
    /// </summary>
    public MapData(string name, string[] data)
    {
        mapName = name;
        rows = data;
    }

    public char GetTile(int x, int y)
    {
        if (y < 0 || y >= Height || x < 0 || x >= Width)
            return '.';
        if (x >= rows[y].Length)
            return '.';
        return rows[y][x];
    }
}
