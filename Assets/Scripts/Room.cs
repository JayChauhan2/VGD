using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    [Header("Door Objects (Assign in Prefab)")]
    [SerializeField] GameObject topDoorObj;
    [SerializeField] GameObject bottomDoorObj;
    [SerializeField] GameObject leftDoorObj;
    [SerializeField] GameObject rightDoorObj;

    public Vector2Int RoomIndex { get; set; }
    
    // Global tracking for unmanaged rooms (manual placement)
    public static List<Room> AllRooms = new List<Room>();

    // Approximate size (should match RoomManager)
    public Vector2 roomSize = new Vector2(20, 12); 

    private void Awake()
    {
        if (!AllRooms.Contains(this)) AllRooms.Add(this);
        
        Debug.Log($"Room {name}: Awake. Checked Inspector Assignments -> Top: {topDoorObj}, Bottom: {bottomDoorObj}, Left: {leftDoorObj}, Right: {rightDoorObj}");

        // Validation & Auto-Assign
        Transform doorsParent = transform.Find("Doors");
        if (doorsParent == null) doorsParent = transform; // Fallback to root if "Doors" container missing

        if (topDoorObj == null) { topDoorObj = doorsParent.Find("TopDoor")?.gameObject; if (topDoorObj == null) Debug.LogError($"Room {name}: 'TopDoor' object is missing! Assign in Inspector or place under 'Doors'."); }
        if (bottomDoorObj == null) { bottomDoorObj = doorsParent.Find("BottomDoor")?.gameObject; if (bottomDoorObj == null) Debug.LogError($"Room {name}: 'BottomDoor' object is missing! Assign in Inspector or place under 'Doors'."); }
        if (leftDoorObj == null) { leftDoorObj = doorsParent.Find("LeftDoor")?.gameObject; if (leftDoorObj == null) Debug.LogError($"Room {name}: 'LeftDoor' object is missing! Assign in Inspector or place under 'Doors'."); }
        if (rightDoorObj == null) { rightDoorObj = doorsParent.Find("RightDoor")?.gameObject; if (rightDoorObj == null) Debug.LogError($"Room {name}: 'RightDoor' object is missing! Assign in Inspector or place under 'Doors'."); }
    }
    
    private void OnDestroy()
    {
        if (AllRooms.Contains(this)) AllRooms.Remove(this);
    }

    public static Room GetRoomContaining(Vector3 position)
    {
        foreach (var r in AllRooms)
        {
            // Simple AABB check assuming room is centered
            Vector3 center = r.transform.position;
            float halfW = r.roomSize.x / 2f;
            float halfH = r.roomSize.y / 2f;
            
            if (position.x >= center.x - halfW && position.x <= center.x + halfW &&
                position.y >= center.y - halfH && position.y <= center.y + halfH)
            {
                return r;
            }
        }
        return null;
    }
    
    // Store actual Door scripts
    private Door topDoor;
    private Door bottomDoor;
    private Door leftDoor;
    private Door rightDoor;
    
    private List<EnemyAI> activeEnemies = new List<EnemyAI>();
    public bool IsCleared { get; private set; } = false;
    public bool PlayerHasEntered { get; private set; } = false;

    // Called by RoomManager after generation
    public void SetNeighbor(Vector2Int direction, Room neighbor)
    {
        if (direction == Vector2Int.up) SetupDoor(topDoorObj, neighbor, direction, ref topDoor);
        else if (direction == Vector2Int.down) SetupDoor(bottomDoorObj, neighbor, direction, ref bottomDoor);
        else if (direction == Vector2Int.left) SetupDoor(leftDoorObj, neighbor, direction, ref leftDoor);
        else if (direction == Vector2Int.right) SetupDoor(rightDoorObj, neighbor, direction, ref rightDoor);
    }

    private void SetupDoor(GameObject doorObj, Room neighbor, Vector2Int dir, ref Door doorRef)
    {
        Debug.Log($"Room {name}: SetupDoor called for Dir {dir}. Neighbor: {neighbor}, DoorObj: {doorObj}");
        if (neighbor == null)
        {
            // No neighbor, deactivate door entirely (wall)
            if (doorObj != null) 
            {
                doorObj.SetActive(false);
                Debug.Log($"Room {name}: Deactivated door {dir} (No neighbor)");
            }
        }
        else
        {
            // Activate and Setup
            if (doorObj != null)
            {
                doorObj.SetActive(true);
                doorRef = doorObj.GetComponent<Door>();
                if (doorRef == null) doorRef = doorObj.AddComponent<Door>(); // Add script if missing
                
                doorRef.Initialize(neighbor, dir);
                // Start doors in locked state (Visible/Solid) - they'll unlock when room is entered/cleared
                doorRef.SetLocked(true);
                
                Debug.Log($"Room {name}: SetupDoor Success for {dir}. DoorRef: {doorRef}");
            }
            else
            {
                Debug.LogError($"Room {name}: Trying to setup door towards {dir} (Neighbor: {neighbor.name}), but Door Object is NULL!");
            }
        }
    }

    private void Start()
    {
        // Decorate this room
        if (RoomDecorator.Instance != null)
        {
            RoomDecorator.Instance.Decorate(this);
        }
    }

    public void RegisterEnemy(EnemyAI enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
            Debug.Log($"Room {name}: Registered enemy. Total: {activeEnemies.Count}");
            
            // If player already entered (combat started), activate immediately
            if (PlayerHasEntered && !IsCleared)
            {
                enemy.SetActive(true);
            }
        }
    }

    public static event System.Action<Room> OnRoomEntered;
    public static event System.Action<Room> OnRoomCleared; // New event

    private bool hasSpawners = false;

    public void RegisterSpawner()
    {
        hasSpawners = true;
        Debug.Log($"Room {name}: Registered Spawner.");
    }

    public void OnPlayerEnter()
    {
        // 1. Move Camera
        if (CameraController.Instance != null)
        {
            CameraController.Instance.MoveTo(transform.position);
        }

        Debug.Log($"Room {name} Entered. Active Enemies: {activeEnemies.Count}");
        PlayerHasEntered = true;

        OnRoomEntered?.Invoke(this);

        // If we have enemies OR spawners, we lock.
        // Even if activeEnemies is 0, if we have a spawner, it might spawn them next frame.
        if ((activeEnemies.Count > 0 || hasSpawners) && !IsCleared)
        {
            LockDoors();
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null) enemy.SetActive(true);
            }
        }
        else if (!IsCleared) // No enemies, no spawners -> Auto Clear Logic?
        {
             // If truly empty, mark cleared immediately
             Debug.Log("Room: Empty room entered. marking cleared.");
             IsCleared = true;
             UnlockDoors();
             OnRoomCleared?.Invoke(this); // Check win condition
        }
        else
        {
            UnlockDoors();
        }
    }

    // Removed broken FindEnemies() method relying on hierarchy


    public void EnemyDefeated(EnemyAI enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            Debug.Log($"Room: Enemy defeated. Remaining: {activeEnemies.Count}");
        }
        else
        {
            Debug.LogWarning("Room: Enemy died but was not in my active list!");
        }

        // Check clearance
        // If we have a spawner, we technically rely on the Spawner to STOP spawning 
        // effectively, but the room itself just checks "Are there active enemies?".
        // IF there is a spawner, it might spawn MORE active enemies later.
        // However, the Spawner stops spawning if the room is cleared.
        // So we need a condition: 
        // If activeEnemies == 0
        // AND (Spawner is done? Spawner doesn't tell us it's done).
        // Actually, if activeEnemies hits 0, and we have a spawner, it might immediately spawn more?
        // But Spawner spawns periodically.
        
        // ISSUE: If I kill the last enemy, activeEnemies is 0.
        // Spawner timer might not be ready.
        // Room thinks it's cleared -> Unlocks doors -> Spawner sees Room.IsCleared -> Stops spawning.
        // This effectively means "You cleared the current WAVE".
        // If we want "Kill X enemies total", we need more logic.
        // Assuming current logic is "Target Active".
        // Since `EnemySpawner` spawns based on `maxActiveEnemies`, if the user kills them faster than they spawn, the count hits 0.
        // But the spawner might still want to spawn more?
        // The user implementation of EnemySpawner just spawns infinitely until room is cleared?
        // No, `activeSpawns.Count < maxActiveEnemies`. It doesn't have a "Total Waves" count.
        // So effectively, "Clear Room" = "Kill all currently extant enemies".
        // If the Spawner hasn't spawned any yet (very start), count is 0.
        // But we locked doors on Entry if `hasSpawners` is true.
        // So if I enter, doors lock. Spawner spawns 1.
        // I kill 1. Count 0. Room Clears?
        // Yes, ensuring the spawner spawning interval isn't super fast relative to my kill?
        // Or essentially, "You only need to kill the current set".
        // This seems to be the current design. We'll stick to it.
        
        if (activeEnemies.Count == 0)
        {
            Debug.Log("Room: All active enemies defeated. Unlocking doors.");
            IsCleared = true;
            UnlockDoors();
            OnRoomCleared?.Invoke(this);
        }
    }
    
    // Call this logic if you have enemies separate from hierarchy but within bounds
    // (Optional implementation detail depending on how User spawns enemies)

    private void LockDoors()
    {
        if (topDoor != null) topDoor.SetLocked(true);
        if (bottomDoor != null) bottomDoor.SetLocked(true);
        if (leftDoor != null) leftDoor.SetLocked(true);
        if (rightDoor != null) rightDoor.SetLocked(true);
    }

    private void UnlockDoors()
    {
        Debug.Log($"Room {name}: UnlockDoors called.");
        if (topDoor != null) { topDoor.SetLocked(false); Debug.Log($"Room {name}: Unlocked top door"); } else Debug.Log($"Room {name}: TopDoor is NULL (no neighbor or failed setup)");
        if (bottomDoor != null) { bottomDoor.SetLocked(false); Debug.Log($"Room {name}: Unlocked bottom door"); } else Debug.Log($"Room {name}: BottomDoor is NULL (no neighbor or failed setup)");
        if (leftDoor != null) { leftDoor.SetLocked(false); Debug.Log($"Room {name}: Unlocked left door"); } else Debug.Log($"Room {name}: LeftDoor is NULL (no neighbor or failed setup)");
        if (rightDoor != null) { rightDoor.SetLocked(false); Debug.Log($"Room {name}: Unlocked right door"); } else Debug.Log($"Room {name}: RightDoor is NULL (no neighbor or failed setup)");
    }

    // Public method for debug/testing purposes
    public void ForceUnlockDoors()
    {
        UnlockDoors();
    }
}
