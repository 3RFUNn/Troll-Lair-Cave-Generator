using System;
using UnityEngine;
using System.Collections.Generic; // Required for Lists
using System.Linq;
using Random = UnityEngine.Random; // Required for OrderBy

/*
 * Based on "Procedural Cave Generation" by Sebastian Lague
 * https://www.youtube.com/watch?v=v7yyZZjF1z4&list=PLFt_AvWsXl0eZgMK_DT5_biRkWXftAOf9
 *
 * Modified for Assessment Requirements:
 * - Adheres to the three-stage generation process.
 * - Stage 1: Uses random fill.
 * - Stage 2: Uses cellular automata smoothing.
 * - Stage 3: Implements region processing.
 * - Additional Feature 1: Analyzes floor space percentage.
 * - Additional Feature 2: Adds simple autonomous agents (visualized with Gizmos).
 * - Additional Feature 3: Visualizes identified regions during Stage 3 processing (via Gizmos).
 * - Design Brief: Generates cave systems suitable for a "Troll Lair".
 * - Includes OnDrawGizmos for scene view visualization of map, agents, and regions.
 */
public class MapGenerator : MonoBehaviour
{
    #region Public Variables

    [Header("Map Dimensions")]
    public int width = 64; // Default value
    public int height = 64; // Default value

    [Header("Generation Parameters")]
    public string seed;
    public bool useRandomSeed = true;

    [Range(0, 100)]
    public int randomFillPercent = 48; // Default value
    public int smoothingIterations = 5; // Default value

    [Header("Region Processing (Stage 3)")]
    public int wallThresholdSize = 50;
    public int roomThresholdSize = 50;
    public bool connectClosestRooms = false; // Optional: Connect remaining rooms after processing
    public int passageRadius = 1; // Radius of connection passages

    [Header("Border Settings")]
    public int borderSize = 2; // Slightly larger default

    [Header("Autonomous Agents")]
    public bool enableAgents = true;
    public int numberOfAgents = 3; // How many agents to spawn
    [Range(0f, 0.1f)]
    public float agentMoveChance = 0.02f; // Chance per Update agent tries to move

    [Header("Visualization")]
    public bool visualizeRegions = false; // Toggle to see regions in Gizmos
    public bool visualizeAgents = true;  // Toggle agent Gizmos

    #endregion

    #region Private Variables
    int[,] map; // 0 = empty/floor, 1 = wall

    // Agent Data (kept simple as per constraint)
    List<Coord> agentPositions; // Stores current position of each agent

    // Visualization Data
    List<Room> survivingRoomsForVisualization; // Store rooms for potential connection/visualization
    List<List<Coord>> regionsToVisualize; // Store regions found in Stage 3 for Gizmo drawing
    List<Color> regionColors; // Colors for visualizing regions

    #endregion

    #region Unity Lifecycle Methods

    void Start()
    {
        GenerateMap();
    }

    void Update()
    {
        // Regenerate map on left mouse click
        if (Input.GetMouseButtonDown(0))
        {
            GenerateMap();
        }

        // Simulate agent movement if enabled
        if (enableAgents && agentPositions != null && agentPositions.Count > 0)
        {
            SimulateAgentMovement();
        }
    }

    void OnDrawGizmos()
    {
        // Draw Base Map
        if (map != null)
        {
            int finalWidth = map.GetLength(0);
            int finalHeight = map.GetLength(1);

            for (int x = 0; x < finalWidth; x++)
            {
                for (int y = 0; y < finalHeight; y++)
                {
                    // Base map colors (black/white)
                    Color baseColor = (map[x, y] == 1) ? Color.black : Color.white;
                    Gizmos.color = baseColor;

                    // Visualize Regions (if enabled)
                    if (visualizeRegions && regionsToVisualize != null && map[x, y] == 0) // Only color floor tiles for regions
                    {
                        bool colored = false;
                        for (int i = 0; i < regionsToVisualize.Count; i++)
                        {
                            // Check if this coordinate belongs to the current region being checked
                            // This is inefficient for large numbers of regions/tiles, but simple
                            if (regionsToVisualize[i].Contains(new Coord(x, y)))
                            {
                                Gizmos.color = regionColors[i]; // Use pre-assigned region color
                                colored = true;
                                break; // Tile found in a region, stop checking
                            }
                        }
                         // Optional: Slightly dim un-regioned floor tiles if desired when visualizing
                         // if (!colored) Gizmos.color = Color.grey;
                    }

                    Vector3 pos = GetWorldPosition(x, y, finalWidth, finalHeight);
                    Gizmos.DrawCube(pos, Vector3.one * 0.9f); // Slightly smaller cubes
                }
            }
        }

        // Draw Agents (if enabled)
        if (visualizeAgents && enableAgents && agentPositions != null)
        {
            Gizmos.color = Color.red; // Agent color
            int finalWidth = map.GetLength(0);
            int finalHeight = map.GetLength(1);
            foreach (Coord agentPos in agentPositions)
            {
                Vector3 pos = GetWorldPosition(agentPos.tileX, agentPos.tileY, finalWidth, finalHeight);
                Gizmos.DrawSphere(pos, 0.4f); // Draw agents as spheres
            }
        }

        // --- Optional: Visualize Room Connections ---
        if (connectClosestRooms && survivingRoomsForVisualization != null) {
            Gizmos.color = Color.cyan;
            foreach (Room room in survivingRoomsForVisualization) {
                if (room.connectedRooms.Count > 0) { // Check if connections were made
                    foreach (Room connectedRoom in room.connectedRooms) {
                        Vector3 startPos = GetWorldPosition(room.centerTile.tileX, room.centerTile.tileY, map.GetLength(0), map.GetLength(1));
                        Vector3 endPos = GetWorldPosition(connectedRoom.centerTile.tileX, connectedRoom.centerTile.tileY, map.GetLength(0), map.GetLength(1));
                        Gizmos.DrawLine(startPos, endPos);
                    }
                }
            }
        }
        // --- End Optional ---
    }

    // Helper to convert map coords to world position for Gizmos
    Vector3 GetWorldPosition(int x, int y, int mapWidth, int mapHeight)
    {
        return new Vector3(-mapWidth / 2f + x + .5f, 0, -mapHeight / 2f + y + .5f);
    }

    #endregion

    #region Map Generation Pipeline

    void GenerateMap()
    {
        // --- Initialization ---
        map = new int[width, height];
        // Clear previous agent/visualization data
        agentPositions = new List<Coord>();
        regionsToVisualize = new List<List<Coord>>();
        regionColors = new List<Color>();
        survivingRoomsForVisualization = new List<Room>(); // Clear rooms
        Random.InitState(useRandomSeed ? (int)System.DateTime.Now.Ticks : seed.GetHashCode()); // Initialize Unity's random


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
        ProcessMap(); // Includes region removal and potential connection
        Debug.Log("Stage 3: Map Processing Complete.");

        // === FINAL STEP: Add Border ===
        AddMapBorder();
        Debug.Log($"Final Step: Border (size {borderSize}) Added.");

        // === Spawn Agents ===
        if (enableAgents)
        {
            SpawnAgents();
        }

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

    void PopulateMap()
    {
        RandomFillMap();
    }

    void RandomFillMap()
    {
        // Use Unity's Random for consistency if seed is controlled
        System.Random pseudoRandom = useRandomSeed ? new System.Random() : new System.Random(seed.GetHashCode());

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
                    // Use Unity's Random or seeded System.Random
                    int randValue = useRandomSeed ? Random.Range(0, 100) : pseudoRandom.Next(0, 100);
                    map[x, y] = (randValue < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }
    #endregion

    #region Stage 2: Smooth Map (Cellular Automata)

    void SmoothMap()
    {
        int[,] tempMap = (int[,])map.Clone(); // Operate on a copy

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y); // Count neighbours in original map

                // Apply CA rules to the temporary map
                if (neighbourWallTiles > 4)
                    tempMap[x, y] = 1;
                else if (neighbourWallTiles < 4)
                    tempMap[x, y] = 0;
                // if neighbourWallTiles == 4, tile stays as it was
            }
        }
        map = tempMap; // Update the main map
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                // Use IsInMapRange check before accessing map array
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    // Don't count the central tile
                    if (neighbourX != gridX || neighbourY != gridY)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    // Out of bounds counts as wall
                    wallCount++;
                }
            }
        }
        return wallCount;
    }
    #endregion

    #region Stage 3: Process Map

    void ProcessMap()
    {
        // --- 1. Remove Small Wall Regions ---
        List<List<Coord>> wallRegions = GetAllRegions(1); // Get all wall regions
        foreach (List<Coord> region in wallRegions)
        {
            // Store for visualization BEFORE removal
            // (Only useful if you specifically want to see removed wall regions)
            // if (visualizeRegions) regionsToVisualize.Add(new List<Coord>(region));

            if (region.Count < wallThresholdSize)
            {
                foreach (Coord tile in region)
                {
                    map[tile.tileX, tile.tileY] = 0; // Turn small wall regions into floor
                }
            }
        }

        // --- 2. Remove Small Floor Regions (Caves) & Identify Major Rooms ---
        List<List<Coord>> floorRegions = GetAllRegions(0); // Get all floor regions
        survivingRoomsForVisualization.Clear(); // Clear previous list

        foreach (List<Coord> region in floorRegions)
        {
            // Store for visualization BEFORE removal
            if (visualizeRegions)
            {
                regionsToVisualize.Add(new List<Coord>(region)); // Add a copy
                // Assign a random color for this region
                regionColors.Add(Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f)); // Generate distinct colors
            }

            if (region.Count < roomThresholdSize)
            {
                foreach (Coord tile in region)
                {
                    map[tile.tileX, tile.tileY] = 1; // Turn small floor regions into wall
                }
            }
            else
            {
                 // This is a surviving room, store it
                 survivingRoomsForVisualization.Add(new Room(region, map));
            }
        }

        // --- 3. Optional: Connect Surviving Rooms ---
        if (connectClosestRooms && survivingRoomsForVisualization.Count > 1)
        {
            ConnectClosestRooms(survivingRoomsForVisualization);
        }

        // Ensure regionsToVisualize is consistent if rooms were filled
        if (visualizeRegions)
        {
             // Re-filter regionsToVisualize to only include tiles that are still floors
             // (more accurate visualization after small rooms are filled)
             List<List<Coord>> finalRegions = new List<List<Coord>>();
             List<Color> finalColors = new List<Color>();
             for(int i=0; i < regionsToVisualize.Count; i++)
             {
                 List<Coord> currentRegion = regionsToVisualize[i];
                 List<Coord> validTilesInRegion = new List<Coord>();
                 foreach(Coord tile in currentRegion)
                 {
                     if (IsInMapRange(tile.tileX, tile.tileY) && map[tile.tileX, tile.tileY] == 0) {
                         validTilesInRegion.Add(tile);
                     }
                 }
                 // Only keep regions that still have floor tiles
                 if (validTilesInRegion.Count > 0 && validTilesInRegion.Count >= roomThresholdSize) {
                    finalRegions.Add(validTilesInRegion);
                    finalColors.Add(regionColors[i]); // Keep original color
                 }
             }
             regionsToVisualize = finalRegions;
             regionColors = finalColors;
        }
    }

    // --- Region Finding Helpers (Modified for Stage 3) ---

    /*
     * Gets all contiguous regions of a specific tile type.
     */
    List<List<Coord>> GetAllRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height]; // 0 = not visited

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    List<Coord> newRegion = GetRegionTiles(x, y, tileType, mapFlags);
                    regions.Add(newRegion);
                }
            }
        }
        return regions;
    }

    /*
     * Flood Fill (BFS) to find connected tiles (identical to previous version).
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

    // --- Optional Room Connection Logic ---

    void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false) {
		List<Room> roomListA = new List<Room> ();
		List<Room> roomListB = new List<Room> ();

		if (forceAccessibilityFromMainRoom) {
			foreach (Room room in allRooms) {
				if (room.isAccessibleFromMainRoom) {
					roomListB.Add (room);
				} else {
					roomListA.Add (room);
				}
			}
		} else {
			roomListA = allRooms;
			roomListB = allRooms;
		}

		int bestDistance = 0;
		Coord bestTileA = new Coord();
		Coord bestTileB = new Coord();
		Room bestRoomA = new Room();
		Room bestRoomB = new Room();
		bool possibleConnectionFound = false;

		foreach (Room roomA in roomListA) {
			if (!forceAccessibilityFromMainRoom) {
				possibleConnectionFound = false;
				if (roomA.connectedRooms.Count > 0) {
					continue; // Skip if already connected in this non-forced mode
				}
			}

			foreach (Room roomB in roomListB) {
				if (roomA == roomB || roomA.IsConnected(roomB)) { // Don't connect to self or already connected
					continue;
				}

				for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA ++) {
					for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB ++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];
						// Manhattan distance is simpler/faster than square root
						int distanceBetweenRooms = (int)(Mathf.Abs(tileA.tileX - tileB.tileX) + Mathf.Abs(tileA.tileY - tileB.tileY));

						if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
							bestDistance = distanceBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}
			}
			if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
				CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}

		if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            // Recursively connect remaining unconnected rooms
			ConnectClosestRooms(allRooms, true);
		}

        // If not forcing accessibility, ensure all rooms are connected eventually
        if (!forceAccessibilityFromMainRoom) {
             ConnectClosestRooms(allRooms, true);
        }
	}

    void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
		Room.ConnectRooms (roomA, roomB);
        // Debug.Log ("Connecting " + roomA.roomTiles.Count + " to " + roomB.roomTiles.Count + " via " + tileA.tileX + "," + tileA.tileY + " and " + tileB.tileX + "," + tileB.tileY);

		// Use Bresenham's line algorithm to draw the passage
        List<Coord> line = GetLine(tileA, tileB);
        foreach(Coord c in line) {
            DrawCircle(c, passageRadius); // Draw circles along the line for thickness
        }
	}

    // Draw a filled circle (carve out floor tiles)
    void DrawCircle(Coord c, int r) {
        for (int x = -r; x <= r; x++) {
            for (int y = -r; y <= r; y++) {
                if (x*x + y*y <= r*r) { // Check if within circle radius
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if (IsInMapRange(drawX, drawY)) {
                        map[drawX, drawY] = 0; // 0 = floor
                    }
                }
            }
        }
    }

    // Bresenham's line algorithm implementation
    List<Coord> GetLine(Coord from, Coord to) {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest) {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++) {
            line.Add(new Coord(x, y));

            if (inverted) {
                y += step;
            } else {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest) {
                if (inverted) {
                    x += gradientStep;
                } else {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }
        return line;
    }


    #endregion

    #region Border Addition

    void AddMapBorder()
    {
        if (borderSize <= 0) return; // Skip if no border needed

        int originalWidth = map.GetLength(0);
        int originalHeight = map.GetLength(1);
        int borderedWidth = originalWidth + borderSize * 2;
        int borderedHeight = originalHeight + borderSize * 2;
        int[,] borderedMap = new int[borderedWidth, borderedHeight];

        for (int x = 0; x < borderedWidth; x++)
        {
            for (int y = 0; y < borderedHeight; y++)
            {
                // Check if inside the original map area
                if (x >= borderSize && x < originalWidth + borderSize && y >= borderSize && y < originalHeight + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1; // Wall
                }
            }
        }
        map = borderedMap; // Replace map
    }
    #endregion

    #region Autonomous Agents Feature

    /*
     * Spawns the specified number of agents onto random floor tiles.
     * Must be called AFTER the map is fully processed and bordered.
     */
    void SpawnAgents()
    {
        agentPositions.Clear(); // Clear any previous agents
        if (map == null) return;

        List<Coord> possibleSpawnPoints = GetAllFloorTiles();

        if (possibleSpawnPoints.Count == 0) {
            Debug.LogWarning("No floor tiles found to spawn agents!");
            return;
        }

        for (int i = 0; i < numberOfAgents && possibleSpawnPoints.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, possibleSpawnPoints.Count);
            agentPositions.Add(possibleSpawnPoints[randomIndex]);
            possibleSpawnPoints.RemoveAt(randomIndex); // Prevent spawning multiple agents on the same tile
        }
        Debug.Log($"Spawned {agentPositions.Count} agents.");
    }

    /*
     * Gets a list of all floor tile coordinates in the current (final) map.
     */
    List<Coord> GetAllFloorTiles() {
        List<Coord> floorTiles = new List<Coord>();
         if (map == null) return floorTiles;

        int finalWidth = map.GetLength(0);
        int finalHeight = map.GetLength(1);

        for (int x = 0; x < finalWidth; x++) {
            for (int y = 0; y < finalHeight; y++) {
                if (map[x, y] == 0) { // 0 is floor
                    floorTiles.Add(new Coord(x, y));
                }
            }
        }
        return floorTiles;
    }


    /*
     * Very basic agent simulation: random walk on floor tiles.
     * Called from Update().
     */
    void SimulateAgentMovement()
    {
        if (map == null) return;

        int finalWidth = map.GetLength(0);
        int finalHeight = map.GetLength(1);

        // Cardinal directions
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        // Need to use a regular loop because we modify the list
        for(int i=0; i < agentPositions.Count; i++)
        {
             // Only move sometimes based on move chance
            if (Random.value < agentMoveChance) // Random.value is 0.0 to 1.0
            {
                Coord currentPos = agentPositions[i];
                List<Coord> possibleMoves = new List<Coord>();

                // Check neighbours
                for (int j = 0; j < 4; j++)
                {
                    int nextX = currentPos.tileX + dx[j];
                    int nextY = currentPos.tileY + dy[j];

                    // Check bounds AND if the target tile is a floor
                    if (nextX >= 0 && nextX < finalWidth && nextY >= 0 && nextY < finalHeight && map[nextX, nextY] == 0)
                    {
                        possibleMoves.Add(new Coord(nextX, nextY));
                    }
                }

                // If there are valid moves, pick one randomly
                if (possibleMoves.Count > 0)
                {
                    agentPositions[i] = possibleMoves[Random.Range(0, possibleMoves.Count)];
                }
            }
        }
    }

    #endregion

    #region Additional Feature: Map Analysis

    void AnalyzeMap()
    {
        if (map == null || map.Length == 0) return;

        int floorCount = 0;
        int finalWidth = map.GetLength(0);
        int finalHeight = map.GetLength(1);
        int totalTiles = finalWidth * finalHeight;

        if (totalTiles == 0) return;

        for (int x = 0; x < finalWidth; x++)
        {
            for (int y = 0; y < finalHeight; y++)
            {
                if (map[x, y] == 0) floorCount++;
            }
        }

        float floorPercentage = (float)floorCount / totalTiles * 100f;
        Debug.Log($"--- Map Analysis ---");
        Debug.Log($"Final Map Dimensions: {finalWidth}x{finalHeight}");
        Debug.Log($"Total Tiles: {totalTiles}");
        Debug.Log($"Floor Tiles: {floorCount}");
        Debug.Log($"Floor Space Percentage: {floorPercentage:F2}%");
        Debug.Log($"--------------------");
    }
    #endregion

    #region Helper Methods & Structs

    /*
     * Checks if coordinates are within the ORIGINAL map dimensions (before border).
     * Crucial for algorithms running before AddMapBorder().
     */
    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Simple coordinate struct (unchanged)
    // Make it comparable for potential use in Sets or Dictionaries later
    [System.Serializable] // Make it viewable in inspector if needed, e.g., inside Room class
    public struct Coord : System.IEquatable<Coord>
    {
        public int tileX;
        public int tileY;

        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }

         // Implement IEquatable for efficient comparisons and use in collections
        public bool Equals(Coord other)
        {
            return tileX == other.tileX && tileY == other.tileY;
        }

        public override bool Equals(object obj)
        {
            return obj is Coord other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Simple hash code calculation
            return tileX * 1000 + tileY; // Adjust multiplier if map dimensions are huge
        }

         public static bool operator ==(Coord c1, Coord c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(Coord c1, Coord c2)
        {
            return !c1.Equals(c2);
        }
    }

    // Room class to store room data (tiles, edge tiles, connections)
    // Added for optional room connection logic and better organization
    [System.Serializable] // Allow viewing in inspector potentially
    class Room : System.IEquatable<Room>
    {
        public List<Coord> roomTiles; // All tiles in the room
        public List<Coord> edgeTiles; // Tiles adjacent to walls
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom; // Usually the largest room
        public Coord centerTile; // Approximate center

        // Default constructor for safety
        public Room() {
             roomTiles = new List<Coord>();
             edgeTiles = new List<Coord>();
             connectedRooms = new List<Room>();
             roomSize = 0;
        }

        public Room(List<Coord> tiles, int[,] map) {
            roomTiles = tiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();

             // Calculate approximate center (average position)
            float avgX = 0, avgY = 0;

            // Identify edge tiles (room tiles next to a wall tile)
            foreach (Coord tile in tiles) {
                avgX += tile.tileX;
                avgY += tile.tileY;
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++) {
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++) {
                         // Check diagonals (8 neighbours) for edges
                        if (x == tile.tileX || y == tile.tileY) { // Use this line for 4-neighbour check instead
                            // Check if neighbour is within the original map bounds (where map processing occurs)
                            // Note: This relies on the Room being created *before* AddMapBorder
                            if (x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1)) {
                                if (map[x,y] == 1) { // If neighbour is a wall
                                    edgeTiles.Add(tile);
                                    goto NextTile; // Use goto for early exit from inner loops once edge is found
                                }
                            } else {
                                // Tile is at the edge of the *processing* map, consider it an edge tile
                                edgeTiles.Add(tile);
                                goto NextTile;
                            }
                        }
                    }
                }
                NextTile:; // Label for goto jump
            }
             centerTile = new Coord(Mathf.RoundToInt(avgX / roomSize), Mathf.RoundToInt(avgY / roomSize));
        }

        public void SetAccessibleFromMainRoom() {
            if (!isAccessibleFromMainRoom) {
                isAccessibleFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms) {
                    connectedRoom.SetAccessibleFromMainRoom();
                }
            }
        }

        // Static method to connect two rooms
        public static void ConnectRooms(Room roomA, Room roomB) {
             if (roomA.isAccessibleFromMainRoom) {
                 roomB.SetAccessibleFromMainRoom();
             } else if (roomB.isAccessibleFromMainRoom) {
                 roomA.SetAccessibleFromMainRoom();
             }
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        public bool IsConnected(Room otherRoom) {
            return connectedRooms.Contains(otherRoom);
        }

        // Implement IEquatable based on reference equality or a unique ID if needed
         public bool Equals(Room other)
        {
            if (other == null) return false;
            // For simplicity, assume two Room objects are the same if their tile lists are identical sets
            // A more robust way might involve a unique Room ID if mutation is expected
            return this.roomTiles.Count == other.roomTiles.Count && this.roomTiles.All(other.roomTiles.Contains);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Room);
        }

        public override int GetHashCode()
        {
            // Base hashcode on room size maybe? Or sum of tile hashes? Be careful with mutable objects.
            return roomSize.GetHashCode();
        }
    }

    #endregion
}