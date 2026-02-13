using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public GameObject coinPrefab;
    [SerializeField] PathfindingGrid pathfindingGrid;

    [Header("Room Prefabs")]
    public GameObject firstRoomPrefab;
    public GameObject shopRoomPrefab;
    public GameObject[] generatedRoomPrefabs;

    [Header("Testing Mode")]
    public bool isTestingMode;
    public GameObject testingRoomPrefab;



    public int roomWidth = 20; 
    public int roomHeight = 12;

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

        // Ensure RoomDecorator exists
        if (RoomDecorator.Instance == null)
        {
            GameObject decoratorObj = new GameObject("RoomDecorator");
            decoratorObj.AddComponent<RoomDecorator>();
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
        else
        {
            CreateRoomObject(center, firstRoomPrefab);

            // 3. Prepare Deck
            List<GameObject> deck = new List<GameObject>();
            if (shopRoomPrefab != null) deck.Add(shopRoomPrefab);
            if (generatedRoomPrefabs != null) deck.AddRange(generatedRoomPrefabs);

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
        }

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
            startRoom.OnPlayerEnter();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.transform.position = startRoom.transform.position;
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
}
