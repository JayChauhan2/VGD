using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public GameObject coinPrefab;
    public GameObject mimicPrefab;
    public GameObject ghostEnemyPrefab; // Assigned by User
    [Header("Global Assets")]
    public List<GameObject> globalEnemyPrefabs; // Fallback pool
    public GameObject enemyForcefieldPrefab; // Assigned by User
    public Sprite customEchoMarkerSprite; // Legacy static sprite
    [Tooltip("Drag all animation frames for the echolocation marker here. Overrides customEchoMarkerSprite if not empty.")]
    public Sprite[] customEchoMarkerSprites; // Animated sprites
    public float enemyForcefieldScale = 0.8f;
    [SerializeField] PathfindingGrid pathfindingGrid;

    [Header("Room Prefabs")]
    public GameObject firstRoomPrefab;
    public GameObject shopRoomPrefab;
    public GameObject bossRoomPrefab;
    
    [Tooltip("Minimum number of random rooms to generate (excluding Start, Shop, Boss)")]
    public int minRooms = 5;
    [Tooltip("Maximum number of random rooms to generate. Will be capped by available prefabs.")]
    public int maxRooms = 8;

    public GameObject[] generatedRoomPrefabs;

    [Header("MiniMap Sprites")]
    public Sprite normalRoomSprite;
    public Sprite mysteriousRoomSprite;
    public Sprite bossRoomSprite;
    public Color unvisitedRoomColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public Color visitedRoomColor = Color.white;
    public Color currentRoomColor = new Color(0f, 1f, 0f, 1f);

    [Header("Testing Mode")]
    public bool isTestingMode;
    public GameObject testingRoomPrefab;

    [Tooltip("Testing Map: Only spawns the Start room + a small set of random generated rooms. All gameplay (familiar, win condition, etc.) works normally.")]
    public bool isTestingMap;
    [Range(1, 10), Tooltip("How many generated rooms to spawn in Testing Map mode (not counting the Start room).")]
    public int testingMapRoomCount = 1;
    [Tooltip("Optional override prefab for Testing Map mode. If assigned, this same prefab is used for ALL generated slots instead of random picks.")]
    public GameObject testingMapRoomPrefab;

    [Header("Box Spawning")]
    public GameObject boxPrefab; // Drag BreakableBox prefab here
    [Range(0, 20)] public int minBoxesPerRoom = 3;
    [Range(0, 50)] public int maxBoxesPerRoom = 8;
    [Range(0f, 1f)] public float boxSpawnChance = 1.0f; // Chance for a room to have boxes at all
    public LayerMask obstacleLayer; // Assign "Obstacle" and "Default" (walls)
    public float minBoxDistance = 1.5f; // Minimum distance between boxes
    public Vector2 boxGridOffset = new Vector2(0.5f, 0.5f); // Use this to center boxes on tiles (e.g. 0.5, 0.5)


    [Header("Familiar Scatter")]
    [Tooltip("Assign all Familiar prefabs here. They will be scattered across the map during generation.")]
    public List<GameObject> familiarPrefabs = new List<GameObject>();

    public int roomWidth = 32; 
    public int roomHeight = 16;

    int gridSizeX = 10;
    int gridSizeY = 10;

    private List<GameObject> roomObjects = new List<GameObject>();

    private int[,] roomGrid;
    private int roomCount;
    private Room[,] roomComponentGrid; // Store Room references for quick lookup

    private bool generationComplete = false;
    private bool roomsLinked = false;
    public bool IsInitialized() => generationComplete;

    public Room[,] GetRoomGrid() => roomComponentGrid;



    // Movement after entering door
    public float doorExitOffset = 3f; 
    
    public float teleportCooldown = 0.5f;
    private float lastTeleportTime = -1f;

    public bool CanTeleport => Time.time >= lastTeleportTime + teleportCooldown; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }



    private void Start()
    {
        roomGrid = new int[gridSizeX, gridSizeY];
        roomComponentGrid = new Room[gridSizeX, gridSizeY];

        if (pathfindingGrid == null)
        {
            pathfindingGrid = FindFirstObjectByType<PathfindingGrid>();
            if (pathfindingGrid == null) Debug.LogWarning("RoomManager: PathfindingGrid still not found after auto-search.");
        }



        GenerateLevel();
    }

    private void GenerateLevel()
    {
        // 1. Helpers
        int centerX = gridSizeX / 2;
        int centerY = gridSizeY / 2;
        Vector2Int center = new Vector2Int(centerX, centerY);

        // 2. Place Start Room
        roomGrid[centerX, centerY] = 1;
        roomCount++;

        if (isTestingMode && testingRoomPrefab != null)
        {
            CreateRoomObject(center, testingRoomPrefab);
            Debug.Log("RoomManager: Testing Mode Enabled. Spawning only testing room.");
        }
        else if (isTestingMap)
        {
            // --- TESTING MAP MODE ---
            // Spawn: Start room + testingMapRoomCount generated rooms placed adjacently.
            // Shop, Boss, and all other rooms are skipped.
            // All gameplay systems (familiar, win condition, boxes) function normally.
            CreateRoomObject(center, firstRoomPrefab);
            Debug.Log($"RoomManager: Testing Map Mode Enabled. Spawning Start + {testingMapRoomCount} generated room(s).");

            // Build a shuffled pool of prefabs to pick from (wraps around if count > pool size)
            List<GameObject> mapPool = new List<GameObject>();
            if (testingMapRoomPrefab != null)
            {
                // Override: fill pool with copies of the override prefab
                for (int i = 0; i < testingMapRoomCount; i++) mapPool.Add(testingMapRoomPrefab);
            }
            else if (generatedRoomPrefabs != null && generatedRoomPrefabs.Length > 0)
            {
                // Shuffle the source pool
                List<GameObject> sourcePool = new List<GameObject>(generatedRoomPrefabs);
                for (int i = 0; i < sourcePool.Count; i++)
                {
                    int r = Random.Range(i, sourcePool.Count);
                    GameObject tmp = sourcePool[i]; sourcePool[i] = sourcePool[r]; sourcePool[r] = tmp;
                }
                // Fill mapPool up to testingMapRoomCount, cycling through the shuffled pool
                for (int i = 0; i < testingMapRoomCount; i++)
                    mapPool.Add(sourcePool[i % sourcePool.Count]);
            }
            else
            {
                Debug.LogWarning("RoomManager: Testing Map Mode is ON but no generated room prefabs are assigned!");
            }

            if (mapPool.Count > 0)
            {
                // Grow rooms outward using the same available-spots logic
                List<Vector2Int> availableSpots = new List<Vector2Int>();
                AddNeighborsToAvailable(center, availableSpots);

                foreach (GameObject prefab in mapPool)
                {
                    if (availableSpots.Count == 0) break;

                    int idx = Random.Range(0, availableSpots.Count);
                    Vector2Int spot = availableSpots[idx];
                    availableSpots.RemoveAt(idx);

                    roomGrid[spot.x, spot.y] = 1;
                    roomCount++;
                    CreateRoomObject(spot, prefab);
                    Debug.Log($"RoomManager: Testing Map placed '{prefab.name}' at {spot}.");

                    // Scatter boxes and expose new neighbours for the next room
                    Room placedRoom = roomComponentGrid[spot.x, spot.y];
                    if (placedRoom != null) ScatterBoxes(placedRoom);
                    AddNeighborsToAvailable(spot, availableSpots);
                }
            }
        }
        else
        {
            CreateRoomObject(center, firstRoomPrefab);

            // 3. Prepare Deck
            List<GameObject> deck = new List<GameObject>();
            if (shopRoomPrefab != null) deck.Add(shopRoomPrefab);
            if (bossRoomPrefab != null) deck.Add(bossRoomPrefab);

            if (generatedRoomPrefabs != null && generatedRoomPrefabs.Length > 0)
            {
                // Create a temporary list to shuffle and pick from
                List<GameObject> pool = new List<GameObject>(generatedRoomPrefabs);
                
                // Shuffle pool
                for (int i = 0; i < pool.Count; i++)
                {
                    GameObject temp = pool[i];
                    int r = Random.Range(i, pool.Count);
                    pool[i] = pool[r];
                    pool[r] = temp;
                }

                // Determine how many to pick
                int maxPossible = Mathf.Min(maxRooms, pool.Count);
                int countToPick = Random.Range(minRooms, maxPossible + 1);
                
                // Clamp in case min > total available
                countToPick = Mathf.Clamp(countToPick, 0, pool.Count);

                Debug.Log($"RoomManager: Picking {countToPick} random rooms from pool of {pool.Count} (Min: {minRooms}, Max: {maxRooms})");

                for (int i = 0; i < countToPick; i++)
                {
                    deck.Add(pool[i]);
                }
            }

            // Shuffle Deck
            for (int i = 0; i < deck.Count; i++)
            {
                GameObject temp = deck[i];
                int r = Random.Range(i, deck.Count);
                deck[i] = deck[r];
                deck[r] = temp;
            }

            // 4. Available Spots
            List<Vector2Int> availableSpots = new List<Vector2Int>();
            AddNeighborsToAvailable(center, availableSpots);

            // 5. Place Rooms
            foreach (GameObject prefab in deck)
            {
                if (availableSpots.Count == 0)
                {
                    Debug.LogWarning("RoomManager: No space left for rooms! Grid might be too small.");
                    break;
                }

                // Pick random spot from available
                int index = Random.Range(0, availableSpots.Count);
                Vector2Int spot = availableSpots[index];
                availableSpots.RemoveAt(index);

                // Mark occupied
                roomGrid[spot.x, spot.y] = 1;
                roomCount++;

                // Create
                CreateRoomObject(spot, prefab);

                // Add new neighbors
                AddNeighborsToAvailable(spot, availableSpots);
            }
            
            // 5b. Scatter Boxes in all generated rooms (except Start)
            foreach(var roomObj in roomObjects)
            {
                Room r = roomObj.GetComponent<Room>();
                if (r != null && r.RoomIndex != center) // Don't crash spawn boxes in Start Room
                {
                     ScatterBoxes(r);
                }
            }
        }

        // 5c. Scatter familiars across the map
        ScatterFamiliars();

        // 6. Finish
        generationComplete = true;
        Debug.Log($"Generation Complete. Rooms: {roomCount}");

        if (pathfindingGrid != null)
        {
            Vector2 mapSize = new Vector2(gridSizeX * roomWidth, gridSizeY * roomHeight);
            pathfindingGrid.CreateGrid(Vector2.zero, mapSize);
        }
        else
        {
            Debug.LogError("RoomManager: Cannot creating PathfindingGrid because reference is null!");
        }

        LinkRooms();

        Room startRoom = roomComponentGrid[centerX, centerY];
        if (startRoom != null)
        {
            // Ensure the first room never locks the player in via combat
            startRoom.AlwaysOpen = true;

            startRoom.OnPlayerEnter();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.transform.position = startRoom.transform.position;

            // Lock the start room behind the tutorial overlay
            startRoom.SetTutorialLocked(true);

            // Spawn the tutorial overlay (it will call startRoom.UnlockTutorial() when dismissed)
            GameObject tutorialGO = new GameObject("TutorialOverlay");
            TutorialOverlay tutorial = tutorialGO.AddComponent<TutorialOverlay>();
            tutorial.Initialize(startRoom);
        }
    }

    private void AddNeighborsToAvailable(Vector2Int pos, List<Vector2Int> list)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int n = pos + d;
            if (n.x >= 0 && n.x < gridSizeX && n.y >= 0 && n.y < gridSizeY)
            {
                // Must not be occupied and not already in list
                if (roomGrid[n.x, n.y] == 0 && !list.Contains(n))
                {
                    list.Add(n);
                }
            }
        }
    }

    private void LinkRooms()
    {
        if (roomsLinked) return;
        if (roomComponentGrid == null)
        {
            Debug.LogError("RoomManager: roomComponentGrid is null in LinkRooms! Generation failed or Start didn't run.");
            return;
        }

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Room r = roomComponentGrid[x, y];
                if (r != null)
                {
                    // Check all 4 neighbors
                    // Up
                    if (y + 1 < gridSizeY) r.SetNeighbor(Vector2Int.up, roomComponentGrid[x, y+1]);
                    // Down
                    if (y - 1 >= 0) r.SetNeighbor(Vector2Int.down, roomComponentGrid[x, y-1]);
                    // Right
                    if (x + 1 < gridSizeX) r.SetNeighbor(Vector2Int.right, roomComponentGrid[x+1, y]);
                    // Left
                    if (x - 1 >= 0) r.SetNeighbor(Vector2Int.left, roomComponentGrid[x-1, y]);
                }
            }
        }
        roomsLinked = true;
        Debug.Log("RoomManager: Rooms Linked");
    }

    public void EnterRoom(Room room, Vector2Int entryDirectionFromPrevious)
    {
        if (!CanTeleport) return;
        lastTeleportTime = Time.time;

        // Direction is: Previous -> New Room.
        // e.g. We walked UP (0,1) to get here.
        // So we should spawn at the BOTTOM of the new room.
        
        // 1. Notify Room
        room.OnPlayerEnter();

        // 2. Teleport Player slightly inside the door
        // Entry from UP = Player came from Bottom -> Spawn at Top? 
        // Wait, "entryDirectionFromPrevious" means the door direction we took.
        // If we took the UP door, we move UP (+y).
        // So in the NEW room, we are at the Bottom.
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Calculate spawn position relative to room center
            Vector3 roomCenter = room.transform.position;
            
            // If we walked UP to get here, current room is ABOVE previous.
            // We should emerge from the BOTTOM door of current room.
            // Bottom door is at -Height/2.
            
            Vector3 spawnOffset = Vector3.zero;
            
            if (entryDirectionFromPrevious == Vector2Int.up) // Came from Bottom
                spawnOffset = new Vector3(0, -roomHeight/2f + doorExitOffset, 0); 
            
            else if (entryDirectionFromPrevious == Vector2Int.down) // Came from Top
                spawnOffset = new Vector3(0, roomHeight/2f - doorExitOffset, 0);
            
            else if (entryDirectionFromPrevious == Vector2Int.right) // Came from Left
                spawnOffset = new Vector3(-roomWidth/2f + doorExitOffset, 0, 0);
            
            else if (entryDirectionFromPrevious == Vector2Int.left) // Came from Right
                spawnOffset = new Vector3(roomWidth/2f - doorExitOffset, 0, 0);
                
            player.transform.position = roomCenter + spawnOffset;
        }
    }



    private void CreateRoomObject(Vector2Int roomIndex, GameObject prefab)
    {
        if (prefab == null)
        {
             Debug.LogError($"RoomManager: Trying to create room at {roomIndex} but Prefab is NULL!");
             return;
        }

        var newRoomObj = Instantiate(prefab, GetPositionFromGridIndex(roomIndex), Quaternion.identity);
        newRoomObj.name = $"Room-{roomIndex.x}-{roomIndex.y}";
        
        Room r = newRoomObj.GetComponent<Room>();
        if (r == null) r = newRoomObj.AddComponent<Room>();
        
        r.RoomIndex = roomIndex;
        
        roomObjects.Add(newRoomObj);
        roomComponentGrid[roomIndex.x, roomIndex.y] = r;
    }

    private Vector3 GetPositionFromGridIndex(Vector2Int gridIndex)
    {
        int gridX = gridIndex.x;
        int gridY = gridIndex.y;
        return new Vector3(roomWidth * (gridX - gridSizeX / 2), roomHeight * (gridY - gridSizeY / 2));
    }
    
    public Room GetRoomAt(Vector3 position)
    {
        // Inverse of GetPositionFromGridIndex
        // pos.x = width * (gridX - gridSizeX/2)
        // gridX = (pos.x / width) + gridSizeX/2
        
        int gridX = Mathf.RoundToInt((position.x / roomWidth) + (gridSizeX / 2));
        int gridY = Mathf.RoundToInt((position.y / roomHeight) + (gridSizeY / 2));
        
        if (gridX >= 0 && gridX < gridSizeX && gridY >= 0 && gridY < gridSizeY)
        {
            return roomComponentGrid[gridX, gridY];
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (roomWidth <= 0 || roomHeight <= 0 || gridSizeX <= 0 || gridSizeY <= 0) return;

        Color gizmoColor = new Color(0, 1, 1, 0.05f);
        Gizmos.color = gizmoColor;
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 position = GetPositionFromGridIndex(new Vector2Int(x, y));
                Gizmos.DrawWireCube(position, new Vector3(roomWidth, roomHeight, 1));
            }
        }
    }
    
    private void ScatterBoxes(Room room)
    {
        if (boxPrefab == null) return;
        if (Random.value > boxSpawnChance) return;
        
        // Find the Grid component in the room
        // This handles alignment perfectly regardless of Room scaling
        Grid roomGrid = room.GetComponentInChildren<Grid>();
        if (roomGrid == null)
        {
             Debug.LogWarning($"RoomManager: Could not find 'Grid' component in room {room.name}. Boxes might be unaligned.");
        }
        
        // Disable box spawning near spawners
        EnemySpawner[] spawners = room.GetComponentsInChildren<EnemySpawner>();
        List<Vector3Int> spawnerCells = new List<Vector3Int>();
        if (roomGrid != null)
        {
            foreach(var sp in spawners)
            {
                spawnerCells.Add(roomGrid.WorldToCell(sp.transform.position));
            }
        }
        else
        {
            // Fallback if no grid: just treat spawner positions as "points to avoid" in world space
            // Logic handled inside loop
        }

        // Determine Box Scale based on Room Scale to maintain World Size ~0.6
        float roomScale = room.transform.localScale.x; 
        float targetBoxWorldScale = 0.6f;
        Vector3 boxLocalScale = Vector3.one;
        if (Mathf.Abs(roomScale) > 0.01f)
        {
             boxLocalScale = Vector3.one * (targetBoxWorldScale / roomScale);
        }

        int count = Random.Range(minBoxesPerRoom, maxBoxesPerRoom + 1);
        int attemptsPerBox = 10;
        
        List<Vector3> placedPositions = new List<Vector3>();
        
        // Bounds for random selection
        // We still need to know "how many tiles" wide/high the room is.
        // Assuming roomWidth/Height are correct tile counts.
        float marginX = 2f; 
        float marginY = 2f;
        float halfW = Mathf.Max(1f, (roomWidth / 2f) - marginX);
        float halfH = Mathf.Max(1f, (roomHeight / 2f) - marginY);

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < attemptsPerBox; attempt++)
            {
                // Random Cell Position (Integer)
                int rx = Mathf.RoundToInt(Random.Range(-halfW, halfW));
                int ry = Mathf.RoundToInt(Random.Range(-halfH, halfH));
                Vector3Int cellPos = new Vector3Int(rx, ry, 0);
                
                Vector3 worldPos;
                
                if (roomGrid != null)
                {
                    // Use Grid to get precise world position of the cell center
                    // Note: roomGrid is inside the room, so it handles the room's rotation/scale/position!
                    worldPos = roomGrid.GetCellCenterWorld(cellPos);
                }
                else
                {
                    // Fallback manual calculation
                    worldPos = room.transform.TransformPoint(new Vector3(rx, ry, 0));
                }
                
                // 1. Check against obstacles
                Collider2D hit = Physics2D.OverlapCircle(worldPos, 0.3f, obstacleLayer);
                if (hit != null) continue; 
                
                bool tooClose = false;
                foreach(var pos in placedPositions)
                {
                    if (Vector3.Distance(worldPos, pos) < (minBoxDistance * roomScale)) 
                    {
                        tooClose = true; 
                        break;
                    }
                }
                if (tooClose) continue;
                
                // 3. Clear path from doors? 
                bool blockingDoor = false;
                if (Mathf.Abs(rx) < 2f && Mathf.Abs(Mathf.Abs(ry) - (roomHeight/2f)) < 3f) blockingDoor = true;
                if (Mathf.Abs(ry) < 2f && Mathf.Abs(Mathf.Abs(rx) - (roomWidth/2f)) < 3f) blockingDoor = true;
                
                if (blockingDoor) continue;

                // 4. Check Spawner Adjacency
                bool nearSpawner = false;
                if (roomGrid != null)
                {
                    // Grid Logic: Check integer adjacency (Chebyshev distance <= 1)
                    foreach(var spCell in spawnerCells)
                    {
                        if (Mathf.Abs(cellPos.x - spCell.x) <= 1 && Mathf.Abs(cellPos.y - spCell.y) <= 1)
                        {
                            nearSpawner = true;
                            break;
                        }
                    }
                }
                else
                {
                    // Fallback World Distance Logic
                    // "Adjacent" roughly means within 1 diagonals + buffer. 
                    // If tile world size is ~0.6 (roomScale), diag is ~0.85. 
                    // Let's use 1.5 * roomScale as safe avoidance radius.
                    float avoidRadius = 1.5f * roomScale; 
                    foreach(var sp in spawners)
                    {
                        if (Vector3.Distance(worldPos, sp.transform.position) < avoidRadius)
                        {
                            nearSpawner = true;
                            break;
                        }
                    }
                }
                
                if (nearSpawner) continue;

                // Found a valid spot!

                // Found a valid spot!
                GameObject box = Instantiate(boxPrefab, worldPos, Quaternion.identity);
                box.transform.SetParent(room.transform);
                box.transform.localScale = boxLocalScale; 
                placedPositions.Add(worldPos);
                
                break; // Next box
            }
        }
    }

    // -----------------------------------------------------------------------
    // Familiar Scatter System
    // -----------------------------------------------------------------------

    private int DetermineFamiliarCount()
    {
        int count = 1;
        float roll = Random.value; // 0.0 to 1.0

        if (roll <= 0.001f) count = 5;      // 0.1% chance for 5
        else if (roll <= 0.01f) count = 4;  // 1% chance for 4 (cumulative)
        else if (roll <= 0.05f) count = 3;  // 5% chance for 3
        else if (roll <= 0.20f) count = 2;  // 20% chance for 2
        
        return count;
    }

    private void ScatterFamiliars()
    {
        if (familiarPrefabs == null || familiarPrefabs.Count == 0) return;

        int numFamiliars = DetermineFamiliarCount();
        numFamiliars = Mathf.Min(numFamiliars, familiarPrefabs.Count);

        int centerX = gridSizeX / 2;
        int centerY = gridSizeY / 2;
        Vector2Int startIndex = new Vector2Int(centerX, centerY);

        var eligibleRooms = new System.Collections.Generic.List<Room>();
        foreach (var obj in roomObjects)
        {
            if (obj == null) continue;
            Room r = obj.GetComponent<Room>();
            if (r == null) continue;
            if (r.RoomIndex == startIndex) continue; // Skip start room
            if (r.type == Room.RoomType.Boss) continue; // Skip boss room
            eligibleRooms.Add(r);
        }

        if (eligibleRooms.Count == 0)
        {
            Debug.LogWarning("RoomManager: No eligible rooms for familiar scattering.");
            return;
        }

        // Shuffle familiar prefabs to ensure uniqueness
        List<GameObject> shuffledPrefabs = new List<GameObject>(familiarPrefabs);
        for (int i = 0; i < shuffledPrefabs.Count; i++)
        {
            int rnd = Random.Range(i, shuffledPrefabs.Count);
            GameObject temp = shuffledPrefabs[i];
            shuffledPrefabs[i] = shuffledPrefabs[rnd];
            shuffledPrefabs[rnd] = temp;
        }

        for (int i = 0; i < numFamiliars; i++)
        {
            Room spawnRoom = eligibleRooms[Random.Range(0, eligibleRooms.Count)];
            GameObject prefab = shuffledPrefabs[i];

            // Find a clear spot using FamiliarDropper's logic
            Vector3 dropPos = FamiliarDropper.FindClearSpot(spawnRoom, obstacleLayer, 0.3f);

            GameObject familiarGO = Instantiate(prefab, dropPos, Quaternion.identity);
            Familiar familiar = familiarGO.GetComponent<Familiar>();
            if (familiar != null) familiar.isWild = true;

            Debug.Log($"RoomManager: Scattered Familiar '{prefab.name}' at {dropPos} in room '{spawnRoom.name}'.");
        }
    }
}
