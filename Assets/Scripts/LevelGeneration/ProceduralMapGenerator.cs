using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates Jump King-style maps procedurally.
/// Width is fixed at 30 (matching the .md map format with walls).
/// Height is configurable.
/// </summary>
public static class ProceduralMapGenerator
{
    public const int MAP_WIDTH = 30;  // columns 0 and 29 = walls
    private const int LEFT_WALL = 1;  // first non-wall column
    private const int RIGHT_WALL = MAP_WIDTH - 2; // last non-wall column

    /// <summary>
    /// Generate an Easy difficulty map.
    /// Wide platforms, small gaps, recovery platforms, clear path upward.
    /// </summary>
    /// <param name="height">Total rows in the map (must be at least 12)</param>
    public static MapData GenerateEasy(int height)
    {
        height = Mathf.Max(height, 12);
        char[][] grid = CreateEmptyGrid(height);

        // Add side walls for every row
        AddWalls(grid);

        // --- Bottom section: ground floor with spawn ---
        int bottomRow = height - 1;
        FillRow(grid, bottomRow, LEFT_WALL, RIGHT_WALL, '#');

        // Spawn is at center of bottom area
        int spawnX = MAP_WIDTH / 2;
        grid[bottomRow - 1][spawnX] = 'S';

        // Small spawn platform with grass
        int platformCenterX = MAP_WIDTH / 2;
        int currentRow = bottomRow - 2;

        // --- Generate platforms going upward ---
        int iterations = 0;
        bool side = false; // false = left, true = right

        while (currentRow > 4 && iterations < 200)
        {
            side = !side; // alternate sides

            // Platform parameters (easy = wide)
            int platWidth = UnityEngine.Random.Range(6, 9);

            // Calculate X position so the platform stays within walls
            int platX;
            if (side)
            {
                // Left side
                platX = LEFT_WALL + UnityEngine.Random.Range(0, 3);
            }
            else
            {
                // Right side
                platX = RIGHT_WALL - platWidth - UnityEngine.Random.Range(0, 3);
            }

            // Clamp
            platX = Mathf.Clamp(platX, LEFT_WALL, RIGHT_WALL - platWidth);

            // Place platform: grass surface on currentRow, dirt below
            for (int x = platX; x < platX + platWidth && x <= RIGHT_WALL; x++)
            {
                grid[currentRow][x] = 'g';    // grass surface
                if (currentRow + 1 < height && grid[currentRow + 1][x] == '.')
                    grid[currentRow + 1][x] = '#'; // dirt body below
            }

            // Vertical gap: 2-4 rows for easy
            int gap = UnityEngine.Random.Range(2, 5);

            // Add recovery platform under this jump (if gap >= 3)
            if (gap >= 3)
            {
                int recoveryRow = currentRow + 2;
                int recoveryWidth = Mathf.Min(platWidth - 2, 4);
                int recoveryX = platX + (platWidth - recoveryWidth) / 2;
                for (int x = recoveryX; x < recoveryX + recoveryWidth && x <= RIGHT_WALL; x++)
                {
                    if (recoveryRow < height)
                        grid[recoveryRow][x] = 'R';
                }
            }

            currentRow -= gap; // move upward
            iterations++;
        }

        // --- Top section: goal platform ---
        int goalRow = Mathf.Min(currentRow - 2, height - 5);
        if (goalRow < 0) goalRow = 2;
        int goalWidth = UnityEngine.Random.Range(4, 7);
        int goalX = (MAP_WIDTH - goalWidth) / 2;
        for (int x = goalX; x < goalX + goalWidth && x <= RIGHT_WALL; x++)
        {
            grid[goalRow][x] = 'g';
        }

        // Place goal markers
        int goalMarkerCount = Mathf.Min(goalWidth - 2, 3);
        int goalStartX = goalX + (goalWidth - goalMarkerCount) / 2;
        for (int i = 0; i < goalMarkerCount; i++)
        {
            int gx = goalStartX + i;
            if (gx >= LEFT_WALL && gx <= RIGHT_WALL)
                grid[goalRow - 1][gx] = 'G';
        }

        // Ensure goal area is reachable — add small platforms below if gap is too large
        if (goalRow > 6)
        {
            int midY = goalRow + 3;
            if (midY < height && grid[midY][LEFT_WALL + 2] == '.')
            {
                int midWidth = 5;
                int midX = (MAP_WIDTH - midWidth) / 2;
                for (int x = midX; x < midX + midWidth && x <= RIGHT_WALL; x++)
                {
                    grid[midY][x] = 'g';
                    if (midY + 1 < height && grid[midY + 1][x] == '.')
                        grid[midY + 1][x] = '#';
                }
            }
        }

        return GridToMapData(grid, $"Procedural Easy ({height})");
    }

    /// <summary>
    /// Generate a Normal difficulty map.
    /// Tighter platforms, bigger gaps, fewer recovery platforms, occasional shortcuts.
    /// </summary>
    /// <param name="height">Total rows in the map (must be at least 12)</param>
    public static MapData GenerateNormal(int height)
    {
        height = Mathf.Max(height, 12);
        char[][] grid = CreateEmptyGrid(height);

        // Add side walls for every row
        AddWalls(grid);

        // --- Bottom section: ground floor with spawn ---
        int bottomRow = height - 1;
        FillRow(grid, bottomRow, LEFT_WALL, RIGHT_WALL, '#');

        // Spawn offset slightly from center (less predictable)
        int spawnX = MAP_WIDTH / 2 + (UnityEngine.Random.value < 0.5f ? -1 : 1);
        grid[bottomRow - 1][spawnX] = 'S';

        int currentRow = bottomRow - 3; // start a bit higher

        // --- Generate platforms going upward ---
        int iterations = 0;
        bool side = false;
        int lastPlatX = spawnX; // track for placement variety

        while (currentRow > 5 && iterations < 200)
        {
            side = !side;

            // Platform width: 4-6 tiles (tighter than easy)
            int platWidth = UnityEngine.Random.Range(4, 7);

            // Occasional narrow precision platform (1 in 4 chance)
            if (UnityEngine.Random.value < 0.25f)
                platWidth = UnityEngine.Random.Range(3, 5);

            // Calculate X position with more variance
            int platX;
            if (side)
            {
                platX = LEFT_WALL + UnityEngine.Random.Range(0, 5);
            }
            else
            {
                platX = RIGHT_WALL - platWidth - UnityEngine.Random.Range(0, 5);
            }

            platX = Mathf.Clamp(platX, LEFT_WALL, RIGHT_WALL - platWidth);

            // Place platform
            for (int x = platX; x < platX + platWidth && x <= RIGHT_WALL; x++)
            {
                grid[currentRow][x] = 'g';
                if (currentRow + 1 < height && grid[currentRow + 1][x] == '.')
                    grid[currentRow + 1][x] = '#';
            }

            // Vertical gap: 3-5 rows (bigger than easy)
            int gap = UnityEngine.Random.Range(3, 6);

            // Recovery platform only under gaps >= 4 (less safety net)
            if (gap >= 4)
            {
                // Recovery is narrower too
                int recoveryWidth = Mathf.Min(platWidth - 1, 3);
                if (recoveryWidth > 0)
                {
                    int recoveryRow = currentRow + 2;
                    int recoveryX = platX + (platWidth - recoveryWidth) / 2;
                    for (int x = recoveryX; x < recoveryX + recoveryWidth && x <= RIGHT_WALL; x++)
                    {
                        if (recoveryRow < height)
                            grid[recoveryRow][x] = 'R';
                    }
                }
            }

            // ~30% chance: add a small shortcut/platform on the opposite side
            if (UnityEngine.Random.value < 0.30f && gap >= 3)
            {
                int shortcutWidth = UnityEngine.Random.Range(2, 4);
                int shortcutRow = currentRow + 1;
                int shortcutX = side
                    ? RIGHT_WALL - shortcutWidth - UnityEngine.Random.Range(0, 3)
                    : LEFT_WALL + UnityEngine.Random.Range(0, 3);
                shortcutX = Mathf.Clamp(shortcutX, LEFT_WALL, RIGHT_WALL - shortcutWidth);

                for (int x = shortcutX; x < shortcutX + shortcutWidth && x <= RIGHT_WALL; x++)
                {
                    if (shortcutRow < height)
                        grid[shortcutRow][x] = 'R';
                }
            }

            // ~20% chance: fake bait platform that looks helpful but is offset
            if (UnityEngine.Random.value < 0.20f)
            {
                int baitWidth = UnityEngine.Random.Range(2, 4);
                int baitRow = currentRow + UnityEngine.Random.Range(1, 3);
                int baitX = platX + platWidth + UnityEngine.Random.Range(2, 5);
                if (baitX + baitWidth <= RIGHT_WALL)
                {
                    for (int x = baitX; x < baitX + baitWidth; x++)
                    {
                        if (baitRow < height)
                            grid[baitRow][x] = 'g';
                    }
                }
            }

            currentRow -= gap;
            lastPlatX = platX;
            iterations++;
        }

        // --- Top section: goal platform ---
        int goalRow = Mathf.Max(currentRow - 2, 2);
        int goalWidth = UnityEngine.Random.Range(3, 6);
        int goalX = (MAP_WIDTH - goalWidth) / 2;
        for (int x = goalX; x < goalX + goalWidth && x <= RIGHT_WALL; x++)
        {
            grid[goalRow][x] = 'g';
            if (goalRow + 1 < height && grid[goalRow + 1][x] == '.')
                grid[goalRow + 1][x] = '#';
        }

        // Place goal markers
        int goalMarkers = Mathf.Min(goalWidth - 1, 2);
        int goalStart = goalX + (goalWidth - goalMarkers) / 2;
        for (int i = 0; i < goalMarkers; i++)
        {
            int gx = goalStart + i;
            if (gx >= LEFT_WALL && gx <= RIGHT_WALL)
                grid[goalRow - 1][gx] = 'G';
        }

        // Midway bridge if gap between last platform and goal is large
        if (goalRow > 5 && goalRow > currentRow + 5)
        {
            int midY = (goalRow + currentRow) / 2;
            int midWidth = 4;
            int midX = (MAP_WIDTH - midWidth) / 2;
            for (int x = midX; x < midX + midWidth && x <= RIGHT_WALL; x++)
            {
                grid[midY][x] = 'g';
                if (midY + 1 < height && grid[midY + 1][x] == '.')
                    grid[midY + 1][x] = '#';
            }
        }

        return GridToMapData(grid, $"Procedural Normal ({height})");
    }

    /// <summary>
    /// Generate a Hard difficulty map.
    /// Tiny ledges, chain jumps, long punishment falls, hidden recovery, bait platforms.
    /// </summary>
    /// <param name="height">Total rows in the map (must be at least 12)</param>
    public static MapData GenerateHard(int height)
    {
        height = Mathf.Max(height, 12);
        char[][] grid = CreateEmptyGrid(height);

        AddWalls(grid);

        // --- Bottom section ---
        int bottomRow = height - 1;
        FillRow(grid, bottomRow, LEFT_WALL, RIGHT_WALL, '#');

        // Spawn offset more (harder start)
        int spawnX = LEFT_WALL + UnityEngine.Random.Range(3, 8);
        grid[bottomRow - 1][spawnX] = 'S';

        int currentRow = bottomRow - 4;

        // Track direction for chains
        int chainCount = 0;

        while (currentRow > 5 && chainCount < 300)
        {
            // Platform width: 2-4 tiles (very tight)
            int platWidth = UnityEngine.Random.Range(2, 5);

            // 40% chance of a 1-tile-wide micro ledge (brutal)
            if (UnityEngine.Random.value < 0.40f)
                platWidth = 2 + (UnityEngine.Random.value < 0.5f ? 0 : 1);

            // Place platform — can be on either side or in the middle
            int platX;
            float sideRoll = UnityEngine.Random.value;

            if (sideRoll < 0.40f)
            {
                // Left edge
                platX = LEFT_WALL + UnityEngine.Random.Range(0, 3);
            }
            else if (sideRoll < 0.80f)
            {
                // Right edge
                platX = RIGHT_WALL - platWidth - UnityEngine.Random.Range(0, 3);
            }
            else
            {
                // Middle — harder to judge distance
                platX = (MAP_WIDTH - platWidth) / 2 + UnityEngine.Random.Range(-2, 3);
            }

            platX = Mathf.Clamp(platX, LEFT_WALL, RIGHT_WALL - platWidth);

            // Place platform
            for (int x = platX; x < platX + platWidth && x <= RIGHT_WALL; x++)
            {
                grid[currentRow][x] = 'g';
                if (currentRow + 1 < height && grid[currentRow + 1][x] == '.')
                    grid[currentRow + 1][x] = '#';
            }

            // Vertical gap: 3-6 rows
            int gap = UnityEngine.Random.Range(3, 7);

            // Chain jumps: 35% chance to chain 2-3 platforms close together
            if (UnityEngine.Random.value < 0.35f && chainCount < 3)
            {
                chainCount++;
                // Chain platforms are very tight — gap of 2-3
                gap = UnityEngine.Random.Range(2, 4);

                // Narrower in a chain
                int chainWidth = Mathf.Max(platWidth - 1, 2);
                int chainX = platX + UnityEngine.Random.Range(-2, 3);
                chainX = Mathf.Clamp(chainX, LEFT_WALL, RIGHT_WALL - chainWidth);

                // Place chain platform a couple rows above
                int chainRow = currentRow - gap;
                if (chainRow > 5)
                {
                    for (int x = chainX; x < chainX + chainWidth && x <= RIGHT_WALL; x++)
                    {
                        grid[chainRow][x] = 'g';
                        if (chainRow + 1 < height && grid[chainRow + 1][x] == '.')
                            grid[chainRow + 1][x] = '#';
                    }
                    currentRow = chainRow;
                }
                continue; // skip the rest of the loop for this iteration
            }
            else
            {
                chainCount = 0;
            }

            // Hidden recovery: only on gaps >= 5, and OFFSET from the platform (not directly under)
            if (gap >= 5)
            {
                int hiddenWidth = UnityEngine.Random.Range(2, 4);
                int hiddenRow = currentRow + (gap / 2);
                // Place on opposite side from the platform — hard to spot
                int hiddenX = sideRoll < 0.5f
                    ? RIGHT_WALL - hiddenWidth - UnityEngine.Random.Range(0, 3)
                    : LEFT_WALL + UnityEngine.Random.Range(0, 3);
                hiddenX = Mathf.Clamp(hiddenX, LEFT_WALL, RIGHT_WALL - hiddenWidth);

                if (hiddenRow < height)
                {
                    for (int x = hiddenX; x < hiddenX + hiddenWidth && x <= RIGHT_WALL; x++)
                    {
                        grid[hiddenRow][x] = 'R';
                    }
                }
            }

            // Bait platform: 35% chance — looks like the main path but is a dead end
            if (UnityEngine.Random.value < 0.35f)
            {
                int baitWidth = UnityEngine.Random.Range(2, 4);
                int baitRow = currentRow + UnityEngine.Random.Range(1, 3);
                int baitX = platX + platWidth + UnityEngine.Random.Range(3, 7);
                if (baitX + baitWidth <= RIGHT_WALL && baitRow < height)
                {
                    for (int x = baitX; x < baitX + baitWidth; x++)
                        grid[baitRow][x] = 'g';
                }
            }

            currentRow -= gap;
            chainCount++;
        }

        // --- Top section: goal platform (very small) ---
        int goalRow = Mathf.Max(currentRow - 2, 2);
        int goalWidth = UnityEngine.Random.Range(2, 4);
        int goalX = (MAP_WIDTH - goalWidth) / 2 + UnityEngine.Random.Range(-2, 3);
        goalX = Mathf.Clamp(goalX, LEFT_WALL, RIGHT_WALL - goalWidth);

        for (int x = goalX; x < goalX + goalWidth && x <= RIGHT_WALL; x++)
        {
            grid[goalRow][x] = 'g';
            if (goalRow + 1 < height && grid[goalRow + 1][x] == '.')
                grid[goalRow + 1][x] = '#';
        }

        // Place goal markers (just 1 or 2)
        int goalMarkers = Mathf.Min(goalWidth, 2);
        int goalStart = goalX + (goalWidth - goalMarkers) / 2;
        for (int i = 0; i < goalMarkers; i++)
        {
            int gx = goalStart + i;
            if (gx >= LEFT_WALL && gx <= RIGHT_WALL)
                grid[goalRow - 1][gx] = 'G';
        }

        return GridToMapData(grid, $"Procedural Hard ({height})");
    }

    /// <summary>
    /// Creates an empty height×MAP_WIDTH grid filled with '.'.
    /// </summary>
    private static char[][] CreateEmptyGrid(int height)
    {
        var grid = new char[height][];
        for (int y = 0; y < height; y++)
        {
            grid[y] = new char[MAP_WIDTH];
            for (int x = 0; x < MAP_WIDTH; x++)
                grid[y][x] = '.';
        }
        return grid;
    }

    /// <summary>
    /// Adds wall tiles at column 0 and MAP_WIDTH-1 for every row.
    /// </summary>
    private static void AddWalls(char[][] grid)
    {
        for (int y = 0; y < grid.Length; y++)
        {
            grid[y][0] = '[';
            grid[y][MAP_WIDTH - 1] = ']';
        }
    }

    /// <summary>
    /// Fills a horizontal range in a row with the given character.
    /// </summary>
    private static void FillRow(char[][] grid, int row, int fromX, int toX, char c)
    {
        if (row < 0 || row >= grid.Length) return;
        for (int x = fromX; x <= toX && x < MAP_WIDTH; x++)
            grid[row][x] = c;
    }

    /// <summary>
    /// Converts the character grid to a MapData instance.
    /// Grid row 0 = top of map, last row = bottom.
    /// </summary>
    private static MapData GridToMapData(char[][] grid, string name)
    {
        var rows = new string[grid.Length];
        for (int y = 0; y < grid.Length; y++)
            rows[y] = new string(grid[y]);
        return new MapData(name, rows);
    }
}
