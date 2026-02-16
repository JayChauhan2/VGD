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

    // Components
    public PressureController Pressure { get; private set; }
    public RoomEffectsController Effects { get; private set; }
    
    // Spawners list
    private List<EnemySpawner> activeSpawners = new List<EnemySpawner>();

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
    
        // Initialize Pressure System
        Pressure = GetComponent<PressureController>();
        if (Pressure == null) Pressure = gameObject.AddComponent<PressureController>();
        Pressure.Initialize(this);
        Pressure.OnPressureChanged += OnPressureChanged;

        Effects = GetComponent<RoomEffectsController>();
        if (Effects == null) Effects = gameObject.AddComponent<RoomEffectsController>();
        Effects.Initialize();
    }
    
    private void OnDestroy()
    {
        if (AllRooms.Contains(this)) AllRooms.Remove(this);
        if (Pressure != null) Pressure.OnPressureChanged -= OnPressureChanged;
    }

    // Ghost Persistence
    private List<Vector3> pendingGhosts = new List<Vector3>();

    public void AddGhost(Vector3 position)
    {
        pendingGhosts.Add(position);
        Debug.Log($"Room {name}: Ghost added at {position}. Pending Ghosts: {pendingGhosts.Count}");
    }

    private void SpawnPendingGhosts()
    {
        if (pendingGhosts.Count == 0) return;
        
        Debug.Log($"Room {name}: Spawning {pendingGhosts.Count} pending ghosts.");
        
        GameObject prefab = null;
        if (RoomManager.Instance != null) prefab = RoomManager.Instance.ghostEnemyPrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning("Room: Cannot spawn ghosts because GhostEnemyPrefab is missing in RoomManager!");
            return;
        }

        foreach (Vector3 pos in pendingGhosts)
        {
            GameObject ghostObj = Instantiate(prefab, pos, Quaternion.identity);
            
            // Register as enemy but make sure it doesn't lock room
            EnemyAI ghostAI = ghostObj.GetComponent<EnemyAI>();
            if (ghostAI != null)
            {
                // Force it to NOT count towards clear, just in case prefab is wrong
                ghostAI.CountTowardsRoomClear = false;
                ghostAI.AssignRoom(this);
                RegisterEnemy(ghostAI);
            }
        }
        
        // Clear list so they don't double-spawn next time (they are now active enemies in the room)
        // Wait, if we clear properties, they become "real" enemies. 
        // If the player leaves and comes back, "activeEnemies" might be cleared?
        // "activeEnemies" are usually destroyed when room is unloaded? 
        // In this game, rooms seem to stay instantiated (List<GameObject> roomObjects in RoomManager).
        // So they persist. Standard enemies might not respawn, but these ghosts are newly spawned.
        // Once spawned, they are in "activeEnemies". If player leaves and comes back, they are still there?
        // Only if we don't destroy them on exit.
        // Assuming rooms persist, clearing this list is correct.
        pendingGhosts.Clear();
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

    public void RegisterSpawner(EnemySpawner spawner)
    {
        hasSpawners = true;
        if (!activeSpawners.Contains(spawner))
        {
            activeSpawners.Add(spawner);
        }
        Debug.Log($"Room {name}: Registered Spawner {spawner.name}.");
    }
    
    // Fallback for old calls
    public void RegisterSpawner()
    {
        hasSpawners = true;
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
        
        // Spawn any ghosts waiting for the player
        SpawnPendingGhosts();

        // If we have enemies OR spawners, we lock.
        // Even if activeEnemies is 0, if we have a spawner, it might spawn them next frame.
        if ((GetLockingEnemyCount() > 0 || hasSpawners) && !IsCleared)
        {
            LockDoors();
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null) enemy.SetActive(true);
            }
            
            if (Pressure != null) Pressure.Activate();
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

        // Notify Pressure
        if (Pressure != null) Pressure.OnEnemyKilled();

        // Check for clearance ONLY if no spawners exist
        // If spawners exist, we rely on Stabilization (or maybe killing them all fast enough?)
        // If hasSpawners is true, we generally wait for StabilizeRoom() via Pressure.
        // BUT, if the user manages to clear the room and spawners are done/empty?
        // Current Spawner logic spawns forever (or until limit). 
        // So with spawners, we MUST use Stabilization.
        
        if (!hasSpawners)
        {
             // If no spawners, we can check for clear immediately
             CheckClearCondition();
        }
        else
        {
             // If we have spawners, we check if they are disabled yet
             CheckClearCondition();
        }
    }
    
    public void OnPlayerDamaged()
    {
        if (Pressure != null) Pressure.OnPlayerDamaged();
    }
    
    private void OnPressureChanged(float pressurePercent)
    {
        // Update Spawners
        foreach(var spawner in activeSpawners)
        {
            if (spawner != null) spawner.AdjustSpawnRate(pressurePercent);
        }
        
        // Update Effects
        if (Effects != null) Effects.UpdateEffects(pressurePercent);
    }
    
    private bool areSpawnersDisabled = false;

    public void StabilizeRoom()
    {
        if (IsCleared) return;
        
        Debug.Log($"Room {name}: Room Stabilized! Disabling spawners...");
        
        // Disable spawners so no new enemies appear
        areSpawnersDisabled = true;
        foreach(var spawner in activeSpawners)
        {
            if (spawner != null) spawner.gameObject.SetActive(false);
        }
        
        if (Pressure != null) Pressure.Deactivate();
        
        // Now that spawners are disabled, we can check if the room is clear
        CheckClearCondition();
    }
    
    private void CheckClearCondition()
    {
        if (IsCleared) return;
        
        // Condition: No active enemies AND (Spawners disabled OR No spawners to begin with)
        bool spawnersDone = !hasSpawners || areSpawnersDisabled;
        
        // We only care about enemies that "CountTowardsRoomClear"
        if (GetLockingEnemyCount() == 0 && spawnersDone)
        {
             IsCleared = true;
             Debug.Log($"Room {name}: Room Cleared! Unlocking Exits.");
             UnlockDoors();
             OnRoomCleared?.Invoke(this);
        }
    }
    
    // Helper to count only enemies that should lock the room
    private int GetLockingEnemyCount()
    {
        int count = 0;
        foreach(var enemy in activeEnemies)
        {
            if (enemy != null && enemy.CountTowardsRoomClear)
            {
                count++;
            }
        }
        return count;
    }

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
