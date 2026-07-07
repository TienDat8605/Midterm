using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Represents a handcrafted Jump King-style map as a grid of characters.
/// Each character maps to a tile type (see legend below).
/// Can be loaded from .md files or constructed programmatically.
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
    ///   g = Grass surface tile (top of platforms)
    ///   # = Ground tile (dirt / body of platforms)
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

    /// <summary>
    /// Load a map from a .md file in the markdown format.
    /// Expects: first # heading = map name, ``` block = raw map data rows.
    /// </summary>
    public static MapData LoadFromMarkdownFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[MapData] File not found: {filePath}");
            return null;
        }

        string[] lines = File.ReadAllLines(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);
        var dataRows = new List<string>();
        bool inCodeBlock = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();

            // Extract name from first # heading
            if (line.StartsWith("# ") && !inCodeBlock)
            {
                name = line.Substring(2).Trim();
                continue;
            }

            // Toggle code block
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            // Collect data rows inside the code block
            if (inCodeBlock && line.Length > 0)
            {
                dataRows.Add(line);
            }
        }

        if (dataRows.Count == 0)
        {
            Debug.LogError($"[MapData] No data found in {filePath}");
            return null;
        }

        return new MapData(name, dataRows.ToArray());
    }

    /// <summary>
    /// Load all .md map files from a directory.
    /// </summary>
    public static List<MapData> LoadAllFromDirectory(string directoryPath)
    {
        var maps = new List<MapData>();

        if (!Directory.Exists(directoryPath))
        {
            Debug.LogError($"[MapData] Directory not found: {directoryPath}");
            return maps;
        }

        string[] files = Directory.GetFiles(directoryPath, "*.md");
        System.Array.Sort(files); // consistent ordering

        foreach (string file in files)
        {
            MapData map = LoadFromMarkdownFile(file);
            if (map != null)
                maps.Add(map);
        }

        return maps;
    }

    /// <summary>
    /// Returns true if the given character represents a solid ground tile.
    /// </summary>
    public static bool IsGroundChar(char c)
    {
        return c == '#' || c == 'g' || c == 'R' || c == 'S';
    }
}
