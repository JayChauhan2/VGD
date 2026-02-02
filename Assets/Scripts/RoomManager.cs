using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [SerializeField] GameObject roomPrefab;
    public GameObject coinPrefab;
    [SerializeField] PathfindingGrid pathfindingGrid;

    [SerializeField] private int maxRooms = 15; 
    [SerializeField] private int minRooms = 10;
    public int roomWidth = 20; 
    public int roomHeight = 12;

    int gridSizeX = 10;
    int gridSizeY = 10;

    private List<GameObject> roomObjects = new List<GameObject>();
    private Queue<Vector2Int> roomQueue = new Queue<Vector2Int>();
    private int[,] roomGrid;
    private int roomCount;
    private Room[,] roomComponentGrid; // Store Room references for quick lookup

    private bool generationComplete = false;
    private bool roomsLinked = false;
    public bool IsInitialized() => generationComplete;

    public Room[,] GetRoomGrid() => roomComponentGrid;

    [SerializeField] private float generationChance = 0.5f;

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

    private bool hasStarted = false;

    private void Start()
    {
        hasStarted = true;
        roomGrid = new int[gridSizeX, gridSizeY];

        roomComponentGrid = new Room[gridSizeX, gridSizeY];
        roomQueue = new Queue<Vector2Int>();

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

        Vector2Int initialRoomIndex = new Vector2Int(gridSizeX / 2, gridSizeY / 2);
        StartRoomGenerationFromRoom(initialRoomIndex);
    }

    private void Update()
    {
        if (!hasStarted) return;

        if(roomQueue.Count > 0 && !generationComplete)
        {
            Vector2Int roomIndex = roomQueue.Dequeue();

            if (roomCount < maxRooms)
            {
                int gridX = roomIndex.x;
                int gridY = roomIndex.y;

                // Randomize directions
                List<Vector2Int> directions = new List<Vector2Int> 
                { 
                    Vector2Int.up, 
                    Vector2Int.down, 
                    Vector2Int.left, 
                    Vector2Int.right 
                };
                
                // Fisher-Yates shuffle
                for (int i = 0; i < directions.Count; i++)
                {
                    Vector2Int temp = directions[i];
                    int randomIndex = Random.Range(i, directions.Count);
                    directions[i] = directions[randomIndex];
                    directions[randomIndex] = temp;
                }

                foreach(var dir in directions)
                {
                    if (Random.value < generationChance)
                    {
                         TryGenerateRoom(new Vector2Int(gridX + dir.x, gridY + dir.y));
                    }
                }
            }
        }
        else if (!generationComplete && roomQueue.Count == 0)
        {
            // Check if we met the minimum room requirement
            if (roomCount < minRooms && roomObjects.Count > 0)
            {
                Debug.Log($"Generation stalled at {roomCount} rooms (Min: {minRooms}). Restarting from random room.");
                // Pick a random existing room to continue generation from
                GameObject randomRoom = roomObjects[Random.Range(0, roomObjects.Count)];
                Room r = randomRoom.GetComponent<Room>();
                if (r != null)
                {
                   roomQueue.Enqueue(r.RoomIndex);
                }
                return; // Continue next frame
            }

            // Generation Finished
            Debug.Log($"GenerationComplete, {roomCount} rooms created");
            generationComplete = true;

            if (pathfindingGrid != null)
            {
                Vector2 mapSize = new Vector2(gridSizeX * roomWidth, gridSizeY * roomHeight);
                pathfindingGrid.CreateGrid(Vector2.zero, mapSize);
            }
            else
            {
                Debug.LogError("RoomManager: Cannot creating PathfindingGrid because reference is null!");
            }

            // Link Rooms
            LinkRooms();
            
            // Enter First Room (Start Room)
            Room startRoom = roomComponentGrid[gridSizeX / 2, gridSizeY / 2];
            if (startRoom != null)
            {
                startRoom.OnPlayerEnter();
                // Optionally move player to center of start room
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) player.transform.position = startRoom.transform.position;
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

    private void StartRoomGenerationFromRoom(Vector2Int roomIndex)
    {
        roomQueue.Enqueue(roomIndex);
        int x = roomIndex.x;
        int y = roomIndex.y;
        roomGrid[x, y] = 1;
        roomCount++;
        
        CreateRoomObject(roomIndex);
    }

    private bool TryGenerateRoom(Vector2Int roomIndex)
    {
        int x = roomIndex.x;
        int y = roomIndex.y;

        if (x < 0 || x >= gridSizeX || y < 0 || y >= gridSizeY) return false;
        if (roomGrid[x, y] != 0) return false;
        if (roomCount >= maxRooms) return false;
        
        // Removed old forced randomness logic since we handle it in Update now

        roomQueue.Enqueue(roomIndex);
        roomGrid[x, y] = 1;
        roomCount++;

        CreateRoomObject(roomIndex);

        return true;
    }

    private void CreateRoomObject(Vector2Int roomIndex)
    {
        var newRoomObj = Instantiate(roomPrefab, GetPositionFromGridIndex(roomIndex), Quaternion.identity);
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
