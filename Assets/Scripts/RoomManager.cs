using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public GameObject coinPrefab;
    public GameObject mimicPrefab;
    public GameObject ghostEnemyPrefab; 
    [Header("Global Assets")]
    public List<GameObject> globalEnemyPrefabs; 
    public GameObject enemyForcefieldPrefab; 
    public Sprite customEchoMarkerSprite; 
    [Tooltip("Drag all animation frames for the echolocation marker here. Overrides customEchoMarkerSprite if not empty.")]
    public Sprite[] customEchoMarkerSprites; 
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

    [Header("Level Progression")]
    [Tooltip("The name of the scene to load after winning this level.")]
    public string nextLevelName;
    [Tooltip("If true, winning this level shows the 'You Win' screen instead of loading a next level.")]
    public bool isFinalLevel;

    [Header("Box Spawning")]
    public GameObject boxPrefab; 
    [Range(0, 20)] public int minBoxesPerRoom = 3;
    [Range(0, 50)] public int maxBoxesPerRoom = 8;
    [Range(0f, 1f)] public float boxSpawnChance = 1.0f; 
    public LayerMask obstacleLayer; 
    public float minBoxDistance = 1.5f; 
    public Vector2 boxGridOffset = new Vector2(0.5f, 0.5f); 


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
    private Room[,] roomComponentGrid; 

    private bool generationComplete = false;
    private bool roomsLinked = false;
    public bool IsInitialized() => generationComplete;

    public Room[,] GetRoomGrid() => roomComponentGrid;



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
        int centerX = gridSizeX / 2;
        int centerY = gridSizeY / 2;
        Vector2Int center = new Vector2Int(centerX, centerY);

        roomGrid[centerX, centerY] = 1;
        roomCount++;

        if (isTestingMode && testingRoomPrefab != null)
        {
            CreateRoomObject(center, testingRoomPrefab);
        }
        else if (isTestingMap)
        {
            CreateRoomObject(center, firstRoomPrefab);

            List<GameObject> mapPool = new List<GameObject>();
            if (testingMapRoomPrefab != null)
            {
                for (int i = 0; i < testingMapRoomCount; i++) mapPool.Add(testingMapRoomPrefab);
            }
            else if (generatedRoomPrefabs != null && generatedRoomPrefabs.Length > 0)
            {
                List<GameObject> sourcePool = new List<GameObject>(generatedRoomPrefabs);
                for (int i = 0; i < sourcePool.Count; i++)
                {
                    int r = Random.Range(i, sourcePool.Count);
                    GameObject tmp = sourcePool[i]; sourcePool[i] = sourcePool[r]; sourcePool[r] = tmp;
                }
                for (int i = 0; i < testingMapRoomCount; i++)
                    mapPool.Add(sourcePool[i % sourcePool.Count]);
            }

            if (mapPool.Count > 0)
            {
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

                    Room placedRoom = roomComponentGrid[spot.x, spot.y];
                    if (placedRoom != null) ScatterBoxes(placedRoom);
                    AddNeighborsToAvailable(spot, availableSpots);
                }
            }
        }
        else
        {
            CreateRoomObject(center, firstRoomPrefab);

            List<GameObject> deck = new List<GameObject>();
            if (shopRoomPrefab != null) deck.Add(shopRoomPrefab);
            if (bossRoomPrefab != null) deck.Add(bossRoomPrefab);

            if (generatedRoomPrefabs != null && generatedRoomPrefabs.Length > 0)
            {
                List<GameObject> pool = new List<GameObject>(generatedRoomPrefabs);
                
                for (int i = 0; i < pool.Count; i++)
                {
                    GameObject temp = pool[i];
                    int r = Random.Range(i, pool.Count);
                    pool[i] = pool[r];
                    pool[r] = temp;
                }

                int maxPossible = Mathf.Min(maxRooms, pool.Count);
                int countToPick = Random.Range(minRooms, maxPossible + 1);
                
                countToPick = Mathf.Clamp(countToPick, 0, pool.Count);

                for (int i = 0; i < countToPick; i++)
                {
                    deck.Add(pool[i]);
                }
            }

            for (int i = 0; i < deck.Count; i++)
            {
                GameObject temp = deck[i];
                int r = Random.Range(i, deck.Count);
                deck[i] = deck[r];
                deck[r] = temp;
            }

            List<Vector2Int> availableSpots = new List<Vector2Int>();
            AddNeighborsToAvailable(center, availableSpots);

            foreach (GameObject prefab in deck)
            {
                if (availableSpots.Count == 0)
                {
                    break;
                }

                int index = Random.Range(0, availableSpots.Count);
                Vector2Int spot = availableSpots[index];
                availableSpots.RemoveAt(index);

                roomGrid[spot.x, spot.y] = 1;
                roomCount++;

                CreateRoomObject(spot, prefab);

                AddNeighborsToAvailable(spot, availableSpots);
            }
            
            foreach(var roomObj in roomObjects)
            {
                Room r = roomObj.GetComponent<Room>();
                if (r != null && r.RoomIndex != center) 
                {
                     ScatterBoxes(r);
                }
            }
        }

        ScatterFamiliars();

        generationComplete = true;

        if (pathfindingGrid != null)
        {
            Vector2 mapSize = new Vector2(gridSizeX * roomWidth, gridSizeY * roomHeight);
            pathfindingGrid.CreateGrid(Vector2.zero, mapSize);
        }

        LinkRooms();

        Room startRoom = roomComponentGrid[centerX, centerY];
        if (startRoom != null)
        {
            startRoom.AlwaysOpen = true;

            startRoom.OnPlayerEnter();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.transform.position = startRoom.transform.position;

            startRoom.SetTutorialLocked(true);

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
            return;
        }

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Room r = roomComponentGrid[x, y];
                if (r != null)
                {
                    if (y + 1 < gridSizeY) r.SetNeighbor(Vector2Int.up, roomComponentGrid[x, y+1]);
                    if (y - 1 >= 0) r.SetNeighbor(Vector2Int.down, roomComponentGrid[x, y-1]);
                    if (x + 1 < gridSizeX) r.SetNeighbor(Vector2Int.right, roomComponentGrid[x+1, y]);
                    if (x - 1 >= 0) r.SetNeighbor(Vector2Int.left, roomComponentGrid[x-1, y]);
                }
            }
        }
        roomsLinked = true;
    }

    public void EnterRoom(Room room, Vector2Int entryDirectionFromPrevious)
    {
        if (!CanTeleport) return;
        lastTeleportTime = Time.time;

        room.OnPlayerEnter();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 roomCenter = room.transform.position;
            
            Vector3 spawnOffset = Vector3.zero;
            
            if (entryDirectionFromPrevious == Vector2Int.up) 
                spawnOffset = new Vector3(0, -roomHeight/2f + doorExitOffset, 0); 
            
            else if (entryDirectionFromPrevious == Vector2Int.down) 
                spawnOffset = new Vector3(0, roomHeight/2f - doorExitOffset, 0);
            
            else if (entryDirectionFromPrevious == Vector2Int.right) 
                spawnOffset = new Vector3(-roomWidth/2f + doorExitOffset, 0, 0);
            
            else if (entryDirectionFromPrevious == Vector2Int.left) 
                spawnOffset = new Vector3(roomWidth/2f - doorExitOffset, 0, 0);
                
            player.transform.position = roomCenter + spawnOffset;
        }
    }



    private void CreateRoomObject(Vector2Int roomIndex, GameObject prefab)
    {
        if (prefab == null)
        {
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
        
        Grid roomGrid = room.GetComponentInChildren<Grid>();
        
        EnemySpawner[] spawners = room.GetComponentsInChildren<EnemySpawner>();
        List<Vector3Int> spawnerCells = new List<Vector3Int>();
        if (roomGrid != null)
        {
            foreach(var sp in spawners)
            {
                spawnerCells.Add(roomGrid.WorldToCell(sp.transform.position));
            }
        }

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
        
        float marginX = 2f; 
        float marginY = 2f;
        float halfW = Mathf.Max(1f, (roomWidth / 2f) - marginX);
        float halfH = Mathf.Max(1f, (roomHeight / 2f) - marginY);

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < attemptsPerBox; attempt++)
            {
                int rx = Mathf.RoundToInt(Random.Range(-halfW, halfW));
                int ry = Mathf.RoundToInt(Random.Range(-halfH, halfH));
                Vector3Int cellPos = new Vector3Int(rx, ry, 0);
                
                Vector3 worldPos;
                
                if (roomGrid != null)
                {
                    worldPos = roomGrid.GetCellCenterWorld(cellPos);
                }
                else
                {
                    worldPos = room.transform.TransformPoint(new Vector3(rx, ry, 0));
                }
                
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
                
                bool blockingDoor = false;
                if (Mathf.Abs(rx) < 2f && Mathf.Abs(Mathf.Abs(ry) - (roomHeight/2f)) < 3f) blockingDoor = true;
                if (Mathf.Abs(ry) < 2f && Mathf.Abs(Mathf.Abs(rx) - (roomWidth/2f)) < 3f) blockingDoor = true;
                
                if (blockingDoor) continue;

                bool nearSpawner = false;
                if (roomGrid != null)
                {
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

                GameObject box = Instantiate(boxPrefab, worldPos, Quaternion.identity);
                box.transform.SetParent(room.transform);
                box.transform.localScale = boxLocalScale; 
                placedPositions.Add(worldPos);
                
                break; 
            }
        }
    }

    private int DetermineFamiliarCount()
    {
        int count = 1;
        float roll = Random.value; 

        if (roll <= 0.001f) count = 5;      
        else if (roll <= 0.01f) count = 4;  
        else if (roll <= 0.05f) count = 3;  
        else if (roll <= 0.20f) count = 2;  
        
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
            if (r.RoomIndex == startIndex) continue; 
            if (r.type == Room.RoomType.Boss) continue; 
            eligibleRooms.Add(r);
        }

        if (eligibleRooms.Count == 0)
        {
            return;
        }

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

            Vector3 dropPos = FamiliarDropper.FindClearSpot(spawnRoom, obstacleLayer, 0.3f);

            GameObject familiarGO = Instantiate(prefab, dropPos, Quaternion.identity);
            Familiar familiar = familiarGO.GetComponent<Familiar>();
            if (familiar != null) familiar.isWild = true;
        }
    }
}
