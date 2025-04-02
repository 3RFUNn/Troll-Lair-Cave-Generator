using UnityEngine;
using System.Collections.Generic; // Required for Lists

/*
 * Based on "Procedural Cave Generation" by Sebastian Lague
 * https://www.youtube.com/watch?v=v7yyZZjF1z4&list=PLFt_AvWsXl0eZgMK_DT5_biRkWXftAOf9
 *
 * Modified for Assessment Requirements:
 * - Adheres to the three-stage generation process.
 * - Stage 1: Uses random fill (consciously chosen implementation).
 * - Stage 2: Uses cellular automata smoothing (kept from base).
 * - Stage 3: Implements region processing to remove small caves and walls.
 * - Additional Feature: Analyzes and reports the final floor space percentage.
 * - Design Brief: Generates cave systems suitable for a "Troll Lair".
 * - Includes OnDrawGizmos for scene view visualization.
 */
public class MapGenerator : MonoBehaviour
{
    #region Public Variables
    [Header("Map Dimensions")]
    public int width;
    public int height;

    [Header("Generation Parameters")]
    public string seed;
    public bool useRandomSeed;

    [Range(0, 100)]
    public int randomFillPercent; // Used in Stage 1
    public int smoothingIterations = 5; // Used in Stage 2

    [Header("Region Processing (Stage 3)")]
    public int wallThresholdSize = 50; // Connected walls smaller than this will be removed
    public int roomThresholdSize = 50; // Connected rooms smaller than this will be removed

    [Header("Border Settings")]
    public int borderSize = 1; // Size of the impassable border around the map

    #endregion

    #region Private Variables
    int[,] map; // 0 = empty/floor, 1 = wall
    #endregion

    #region Unity Lifecycle Methods
    /*
     * Generate the map on start, and regenerate on mouse click
     */
    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        // Regenerate map on left mouse click for easy testing
        if (Input.GetMouseButtonDown(0))
        {
            GenerateMap();
        }
    }

    /*
     * Draws gizmos in the Scene view to visualize the map grid.
     * Only runs when the GameObject is selected in the editor.
     */
    void OnDrawGizmos()
    {
        if (map != null)
        {
            // Use GetLength to get dimensions of the potentially bordered map
            int finalWidth = map.GetLength(0);
            int finalHeight = map.GetLength(1);

            for (int x = 0; x < finalWidth; x++)
            {
                for (int y = 0; y < finalHeight; y++)
                {
                    // Set color based on tile type (wall or floor)
                    Gizmos.color = (map[x, y] == 1) ? Color.black : Color.white;

                    // Calculate world position for the gizmo cube
                    // Centered around the GameObject's origin
                    Vector3 pos = new Vector3(-finalWidth / 2f + x + .5f, 0, -finalHeight / 2f + y + .5f);

                    // Draw a 1x1x1 cube at the calculated position
                    Gizmos.DrawCube(pos, Vector3.one);
                }
            }
        }
    }

    #endregion

    #region Map Generation Pipeline
    /*
     * Main function to orchestrate the map generation process.
     */
    void GenerateMap()
    {
        // Initialize the map array
        map = new int[width, height];

        // === STAGE 1: Generate Initial Grid ===
        PopulateMap();
        Debug.Log("Stage 1: Initial Population Complete.");

        // === STAGE 2: Apply Cellular Automata ===
        for (int i = 0; i < smoothingIterations; i++)
        {
            SmoothMap();
        }
        Debug.Log($"Stage 2: Cellular Automata Smoothing ({smoothingIterations} iterations) Complete.");

        // === STAGE 3: Process Grid ===
        ProcessMap();
        Debug.Log("Stage 3: Map Processing Complete.");


        // === FINAL STEP: Add Border ===
        AddMapBorder();
        Debug.Log($"Final Step: Border (size {borderSize}) Added.");

        // === ADDITIONAL FEATURE: Analyze Map ===
        AnalyzeMap();

        // === Generate Mesh ===
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        if (meshGen != null)
        {
            meshGen.GenerateMesh(map, 1);
            Debug.Log("Mesh Generation Requested.");
        }
        else
        {
            Debug.LogError("MeshGenerator component not found on this GameObject!");
        }
    }
    #endregion

    #region Stage 1: Populate Map
    /*
     * STAGE 1: Populate the initial map grid.
     * Currently uses random filling.
     */
    void PopulateMap()
    {
        RandomFillMap();
    }

    /*
     * Fills the map based on a random percentage.
     * Also ensures the outermost layer is initially walls (before border is added later).
     */
    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }
    #endregion

    #region Stage 2: Smooth Map (Cellular Automata)
    /*
     * STAGE 2: Apply Cellular Automata rules to smooth the map.
     * This version uses the rule: Become wall if > 4 neighbours are walls, become floor if < 4.
     */
    void SmoothMap()
    {
        int[,] tempMap = (int[,])map.Clone();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if (neighbourWallTiles > 4)
                    tempMap[x, y] = 1;
                else if (neighbourWallTiles < 4)
                    tempMap[x, y] = 0;
            }
        }
        map = tempMap;
    }

    /*
     * Counts the number of wall tiles in the 8 surrounding neighbours of a given cell.
     * Treats out-of-bounds neighbours as walls.
     */
    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    wallCount++;
                }
            }
        }
        return wallCount;
    }
    #endregion

    #region Stage 3: Process Map
    /*
     * STAGE 3: Process the smoothed map to finalize the level structure.
     * This involves removing small, isolated regions of walls and floors.
     */
    void ProcessMap()
    {
        RemoveSmallRegions(1, wallThresholdSize); // Remove small wall chunks
        RemoveSmallRegions(0, roomThresholdSize); // Remove small floor caves
    }

    /*
     * Finds and modifies regions of a specific tile type that are smaller than a threshold.
     */
    void RemoveSmallRegions(int tileType, int threshold)
    {
        int fillType = (tileType == 1) ? 0 : 1;
        int[,] mapFlags = new int[width, height]; // 0 = not visited

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y, tileType, mapFlags);

                    if (newRegion.Count < threshold)
                    {
                        foreach (Coord tile in newRegion)
                        {
                            map[tile.tileX, tile.tileY] = fillType;
                        }
                    }
                }
            }
        }
    }

    /*
     * Flood Fill Algorithm (BFS) to find connected tiles of a specific type.
     */
    List<Coord> GetRegionTiles(int startX, int startY, int tileType, int[,] mapFlags)
    {
        List<Coord> tiles = new List<Coord>();
        Queue<Coord> queue = new Queue<Coord>();

        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1; // Mark visited

        while (queue.Count > 0)
        {
            Coord currentTile = queue.Dequeue();
            tiles.Add(currentTile);

            // Check 4 cardinal neighbours
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int neighbourX = currentTile.tileX + dx[i];
                int neighbourY = currentTile.tileY + dy[i];

                if (IsInMapRange(neighbourX, neighbourY) && mapFlags[neighbourX, neighbourY] == 0 && map[neighbourX, neighbourY] == tileType)
                {
                    mapFlags[neighbourX, neighbourY] = 1;
                    queue.Enqueue(new Coord(neighbourX, neighbourY));
                }
            }
        }
        return tiles;
    }
    #endregion

    #region Border Addition
    /*
     * Creates a new, larger map with a border of walls around the existing map.
     */
    void AddMapBorder()
    {
        int borderedWidth = width + borderSize * 2;
        int borderedHeight = height + borderSize * 2;
        int[,] borderedMap = new int[borderedWidth, borderedHeight];

        for (int x = 0; x < borderedWidth; x++)
        {
            for (int y = 0; y < borderedHeight; y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1; // Wall
                }
            }
        }
        map = borderedMap; // Replace map with bordered version
        // Note: Original width/height fields are NOT updated intentionally.
        // They represent the *generation* area, not the final bordered dimensions.
    }
    #endregion

    #region Additional Feature: Map Analysis
    /*
     * Performs simple analysis on the final generated map.
     * Calculates and prints the percentage of floor tiles.
     */
    void AnalyzeMap()
    {
        if (map == null || map.Length == 0)
        {
            Debug.LogWarning("Analysis skipped: Map is not generated yet.");
            return;
        }

        int floorCount = 0;
        int finalWidth = map.GetLength(0);
        int finalHeight = map.GetLength(1);
        int totalTiles = finalWidth * finalHeight;

        for (int x = 0; x < finalWidth; x++)
        {
            for (int y = 0; y < finalHeight; y++)
            {
                if (map[x, y] == 0) // Floor
                {
                    floorCount++;
                }
            }
        }

        if (totalTiles > 0)
        {
            float floorPercentage = (float)floorCount / totalTiles * 100f;
            Debug.Log($"--- Map Analysis ---");
            Debug.Log($"Final Map Dimensions: {finalWidth}x{finalHeight}");
            Debug.Log($"Total Tiles: {totalTiles}");
            Debug.Log($"Floor Tiles: {floorCount}");
            Debug.Log($"Floor Space Percentage: {floorPercentage:F2}%");
            Debug.Log($"--------------------");
        }
        else
        {
            Debug.LogWarning("Analysis skipped: Map has zero tiles.");
        }
    }
    #endregion

    #region Helper Methods & Structs
    /*
     * Checks if a given coordinate is within the original map bounds (before bordering).
     */
    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    /*
     * Simple struct to hold map coordinates.
     */
    struct Coord
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }
    #endregion
}