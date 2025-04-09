# Troll Lair - Procedural Cave Generator

## Project Overview

This Unity project implements a 2D procedural level generator designed to create cave systems suitable for a game concept titled "Troll Lair". The generator operates using a Cellular Automata approach, building upon the foundation provided by the "Generator" Unity project (based on Sebastian Lague's tutorials) as required by the assessment brief.

The core logic resides within the `MapGenerator.cs` script and follows a defined three-stage process to produce varied and interesting cave layouts. Additional features, including simple autonomous agents, generation visualization, and map analysis, have been integrated directly into the `MapGenerator.cs` script to meet assessment criteria.

## Design Brief

The target design brief for this generator is:

**Game Concept 1 (Troll Lair)**
*   **Level:** A system of caves.
*   **Imagined Gameplay:** The player is a thief navigating the caves to steal treasure from a band of trolls that live there.

## Generator Design and Implementation

The level generation process adheres to the required three stages:

### Stage 1: Initial Population (`PopulateMap`)

*   **Method:** A 2D grid (`map[,]`) of specified `width` and `height` is initialized.
*   **Implementation:** The `RandomFillMap` function populates this grid. Each cell (excluding the outermost edge, which is initially set to walls) has a `randomFillPercent` chance of becoming a wall (value 1); otherwise, it becomes a floor (value 0).
*   **Seeding:** Generation uses a `seed` string. If `useRandomSeed` is enabled, the seed is derived from the system time, otherwise, the provided string `seed` is used, allowing for reproducible generation.
*   **Brief Relevance:** This stage creates the initial chaotic noise pattern that forms the basis of the cave structures. The `randomFillPercent` parameter allows control over the initial density, influencing the overall wall-to-floor ratio in the final map.

### Stage 2: Cellular Automata Smoothing (`SmoothMap`)

*   **Method:** Standard Cellular Automata rules are applied iteratively to the grid generated in Stage 1.
*   **Implementation:** The `SmoothMap` function iterates through each cell. For each cell, it counts its wall neighbours in an 8-cell radius (`GetSurroundingWallCount`).
    *   If a cell has more than 4 wall neighbours, it becomes a wall in the next iteration.
    *   If a cell has fewer than 4 wall neighbours, it becomes a floor.
    *   If a cell has exactly 4 wall neighbours, its state remains unchanged.
    *   Out-of-bounds neighbours are treated as walls to encourage solid map edges.
*   **Iterations:** This smoothing process is repeated for `smoothingIterations` times.
*   **Brief Relevance:** This stage transforms the initial random noise into more organic, cave-like structures with defined open spaces (potential rooms) and solid wall formations, fitting the "system of caves" requirement.

### Stage 3: Processing and Refinement (`ProcessMap`)

*   **Method:** This stage refines the smoothed map to produce a more playable and coherent level structure.
*   **Implementation:**
    1.  **Region Identification:** Uses a Flood Fill (BFS) algorithm (`GetRegionTiles`, `GetAllRegions`) to identify all contiguous regions of wall tiles and floor tiles separately.
    2.  **Small Region Removal:**
        *   Wall regions smaller than `wallThresholdSize` are converted entirely to floor tiles. This removes small, isolated pillars or wall fragments.
        *   Floor regions (caves/rooms) smaller than `roomThresholdSize` are converted entirely to wall tiles. This eliminates tiny, unusable pockets of space.
    3.  **(Optional) Room Connection:** If `connectClosestRooms` is enabled, the generator identifies the remaining large floor regions ("rooms"). It then iteratively finds the closest pair of unconnected rooms (based on distance between their edge tiles) and carves a passage between them using Bresenham's line algorithm (`GetLine`) combined with `DrawCircle` to control the `passageRadius`. This ensures better connectivity between major cave areas.
*   **Brief Relevance:** This stage significantly cleans up the raw CA output. Removing small regions makes navigation clearer. Connecting larger caves ensures the player can traverse the level, crucial for the "navigating the caves" gameplay aspect. The thresholds allow tuning the minimum size of features in the final lair.

### Final Step: Border Addition (`AddMapBorder`)

*   After Stage 3, a solid border of wall tiles with thickness `borderSize` is added around the entire map. This ensures the playable area is fully enclosed. The final map dimensions are larger than the initial `width` and `height`.

### Additional Features

1.  **Autonomous Agents (`SpawnAgents`, `SimulateAgentMovement`):**
    *   **Description:** If `enableAgents` is true, `numberOfAgents` simple agents are spawned on random, valid floor tiles after the map is generated. In `Update`, each agent has a `agentMoveChance` per frame to attempt a move to an adjacent, random, valid floor tile (simple random walk).
    *   **Visualization:** Agents are visualized as red spheres using `OnDrawGizmos` if `visualizeAgents` is true.
    *   **Brief Relevance:** These agents can represent the wandering "trolls" mentioned in the brief, adding a dynamic element (even if just visually in this implementation) to the generated lair. Their logic is kept within `MapGenerator.cs` as per the assessment constraint.

2.  **Generation Visualization (`OnDrawGizmos`, `visualizeRegions`):**
    *   **Description:** If `visualizeRegions` is enabled, the `OnDrawGizmos` method colors the floor tiles based on the contiguous floor regions identified during Stage 3 processing (before small rooms are removed, but only coloring tiles that remain floor tiles). Each distinct region gets a unique random color.
    *   **Brief Relevance:** This helps visualize the structure identified by the generator, aiding in debugging and understanding how parameters like `roomThresholdSize` affect the final map layout. It provides insight into the "system of caves" being formed.

3.  **Map Analysis (`AnalyzeMap`):**
    *   **Description:** After generation, this function calculates the total number of floor tiles in the final (bordered) map and prints the percentage of floor space to the console.
    *   **Brief Relevance:** Provides a quantitative measure of the generated level's openness, which can be useful for balancing gameplay space or tuning generator parameters.

## Generated Output Examples

*(Replace these placeholders with your actual screenshots)*

`[Image: Example of a generated cave map mesh in the Game View]`
![Logo](./Assets/Images/Screenshot%202025-04-09%20003304.png)
*Caption: Typical output showing the generated cave mesh.*

`[Image: Scene view showing Gizmo visualization of the map grid (black=wall, white=floor)]`
*Caption: OnDrawGizmos showing the raw grid data (GameObject selected).*

`[Image: Scene view showing Gizmo visualization with Regions enabled]`
*Caption: OnDrawGizmos with `visualizeRegions` enabled, showing distinct floor areas colored differently.*

`[Image: Scene view showing Gizmo visualization with Agents enabled]`
*Caption: OnDrawGizmos showing agents (red spheres) spawned on floor tiles.*

`[Image: Console output showing the Map Analysis results]`
*Caption: Example console log displaying the floor space percentage.*

## How to Use

1.  Ensure you have the correct Unity version installed (see below).
2.  Open this Unity project.
3.  Open the main scene located at `Assets/Main.unity`.
4.  In the Hierarchy window, select the GameObject that has the `MapGenerator.cs` script attached (likely named "Map Generator" or similar).
5.  In the Inspector window, you can adjust the various parameters under the different header sections (`Map Dimensions`, `Generation Parameters`, `Region Processing`, `Border Settings`, `Autonomous Agents`, `Visualization`).
6.  Run the scene (Play button). A cave mesh will be generated in the Game view.
7.  With the `MapGenerator` GameObject still selected, view the Scene window to see the `OnDrawGizmos` visualizations (map grid, regions, agents, depending on enabled settings).
8.  While the scene is running, left-click in the Game view to trigger `GenerateMap()` again with the current Inspector settings.
9.  Check the Console window for the output of the Map Analysis feature after each generation.

## Technical Details

*   **Unity Version Used:** Unity 6000.0.42
*   **Base Project Requirement:** This project is based on the "Generator" project provided on QMPlus, which was designed for Unity 2023.1. While development occurred in the version listed above, care was taken to adhere to the structure and principles expected based on the 2023.1 project foundation.

## Code Generation and Reuse

The development of this project involved utilizing and adapting code from several sources, documented below as required:

1.  **Sebastian Lague's Tutorials:**
    *   The core structure of the `MapGenerator.cs` script, including the initial random fill (`RandomFillMap`), the cellular automata smoothing logic (`SmoothMap`, `GetSurroundingWallCount`), the border addition (`AddMapBorder`), and the general three-stage concept, is heavily based on Sebastian Lague's "Procedural Cave Generation" tutorial series (https://www.youtube.com/watch?v=v7yyZZjF1z4&list=PLFt_AvWsXl0eZgMK_DT5_biRkWXftAOf9).
    *   The optional room connection logic (`ConnectClosestRooms`, `CreatePassage`, `GetLine`, `DrawCircle`) and the `Room` class structure are adapted from Sebastian Lague's extended tutorials/code covering procedural map connectivity, often associated with the cave generation series. Explicit attribution is included in the code comments where applicable.

2.  **Module Lab Exercises:**
    *   No significant code blocks were directly copied from the module lab exercises for this specific generator implementation. Core concepts learned during the labs informed the overall approach.

3.  **Code Libraries:**
    *   No external code libraries (e.g., NPBehave) were used in `MapGenerator.cs`.

4.  **Generative AI Tools:**
    *   A generative AI tool (Large Language Model) was used interactively during the development process. Its contributions include:
        *   Assisting in structuring the code according to the three-stage requirement and adding comments/regions.
        *   Implementing the Stage 3 region processing logic (`ProcessMap`, `GetAllRegions`, `GetRegionTiles` integration for removing small areas).
        *   Implementing the Additional Features:
            *   Autonomous Agent system (`agentPositions`, `SpawnAgents`, `GetAllFloorTiles`, `SimulateAgentMovement`) and their Gizmo visualization.
            *   Region visualization logic within `OnDrawGizmos` (coloring identified floor regions).
            *   Map analysis logic (`AnalyzeMap`).
        *   Refining and debugging existing code segments (e.g., ensuring correct map bounds checks, optimizing loops where suggested).
        *   Generating helper methods like `GetWorldPosition`.
        *   Generating this README.md file based on the code and requirements provided.
    *   The AI was provided with the base code, the assessment requirements, the design brief, and specific requests for feature implementation or code modification. The final code is a result of this iterative process.