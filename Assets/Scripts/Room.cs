using UnityEngine;
using System.Collections.Generic;

public class Room : MonoBehaviour
{
    [Header("Door Objects (Assign in Prefab)")]
    [SerializeField] GameObject topDoorObj;
    [SerializeField] GameObject bottomDoorObj;
    [SerializeField] GameObject leftDoorObj;
    [SerializeField] GameObject rightDoorObj;

    public enum RoomType { Normal, Mysterious, Boss }
    public RoomType type;

    public Vector2Int RoomIndex { get; set; }
    
    // Global tracking for unmanaged rooms (manual placement)
    public static List<Room> AllRooms = new List<Room>();

    // Approximate size (should match RoomManager)
    public Vector2 roomSize = new Vector2(32, 16); 

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

        if (Effects == null) Effects = gameObject.AddComponent<RoomEffectsController>();
        Effects.Initialize();

        // Add Vignette
        if (GetComponent<RoomVignette>() == null)
        {
            gameObject.AddComponent<RoomVignette>();
        }
    }
    
    private void Start()
    {
        // Dynamic Enemy Balancing
        // 1 Spawner -> 4 enemies
        // 2 Spawners -> 3 enemies
        // 3 Spawners -> 2 enemies
        // 4+ Spawners -> 1 enemy
        
        int spawnerCount = activeSpawners.Count;
        int countPerSpawner = 1;

        if (spawnerCount <= 1) countPerSpawner = 3;
        else if (spawnerCount == 2) countPerSpawner = 2;
        else countPerSpawner = 1; // 3+
        
        foreach(var sp in activeSpawners)
        {
            if (sp != null)
            {
                sp.enemiesPerSpawn = countPerSpawner;
                sp.maxActiveEnemies = countPerSpawner + 1; // Allow slightly more max active to prevent stalling? Or just match?
                // User requirement was "three enemies spawn each time". 
                // Let's interpret this as "enemiesPerSpawn".
                // maxActiveEnemies should probably scale too, otherwise we might spawn 4 but max is 5 (default).
                // Let's set max active to roughly 1.5x - 2x spawn count so waves can overlap slightly?
                // Or just keep it simple. If we spawn 3, max should be at least 3.
                // Let's set maxActiveEnemies to countPerSpawner * 2.
                sp.maxActiveEnemies = countPerSpawner * 2;
            }
        }
        
        Debug.Log($"Room {name} Balanced: {spawnerCount} Spawners -> {countPerSpawner} Enemies/Spawn each.");
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
        
        if (doorObj != null)
        {
            doorObj.SetActive(true);
            doorRef = doorObj.GetComponent<Door>();
            if (doorRef == null) doorRef = doorObj.AddComponent<Door>(); // Add script if missing
            
            doorRef.Initialize(neighbor, dir);
            // Start doors in locked state (Visible/Solid) - they'll unlock when room is entered/cleared
            doorRef.SetLocked(true);
            
            if (neighbor != null)
                Debug.Log($"Room {name}: SetupDoor Success for {dir}. DoorRef: {doorRef}");
            else
                Debug.Log($"Room {name}: Door {dir} has no neighbor, will remain locked. DoorRef: {doorRef}");
        }
        else
        {
            Debug.LogError($"Room {name}: Trying to setup door towards {dir} (Neighbor: {neighbor?.name}), but Door Object is NULL!");
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

    // Flag to force doors to stay open (e.g. for Start Room or special rooms)
    public bool AlwaysOpen = false;

    // Tutorial locking: keeps doors locked until tutorial is dismissed
    public bool TutorialLocked { get; private set; } = false;

    /// <summary>Called by RoomManager when the tutorial overlay is spawned.</summary>
    public void SetTutorialLocked(bool locked)
    {
        TutorialLocked = locked;
        if (locked) LockDoors();
    }

    /// <summary>Called by TutorialOverlay when the player finishes all pages.</summary>
    public void UnlockTutorial()
    {
        TutorialLocked = false;
        // Only open the doors if combat locking isn't active
        if (AlwaysOpen || IsCleared)
        {
            UnlockDoors();
        }
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

        // 3. Decide on Doors & Activation
        // Even if AlwaysOpen is true, we still want to activate enemies/spawners so the room feels alive.
        // We just won't LOCK the doors.

        bool shouldLock = false;
        if (!IsCleared && !AlwaysOpen)
        {
            // Normal locking logic: Lock if enemies or spawners exist
             if (GetLockingEnemyCount() > 0 || hasSpawners)
             {
                 shouldLock = true;
             }
        }

        // Activate Enemies
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) enemy.SetActive(true);
        }
        
        if (shouldLock)
        {
             LockDoors();
             if (Pressure != null) Pressure.Activate();
        }
        else
        {
             // If we're not locking, we should probably just Unlock immediately.
             // If the room was empty/cleared, we handle that below.
             UnlockDoors();

             // Special Case: Room had enemies but is "AlwaysOpen" -> We still activate pressure?
             // Probably not pressure if it's the start room? 
             // Let's say: If AlwaysOpen, no pressure, no locking.
             
             // BUT: If the room is empty (no enemies/spawners) and not cleared, we should clear it.
             if (!IsCleared && GetLockingEnemyCount() == 0 && !hasSpawners)
             {
                  Debug.Log("Room: Empty room entered. marking cleared.");
                  IsCleared = true;
                  OnRoomCleared?.Invoke(this); 
             }
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
             
             // Visually drain the pressure bar
             if (Pressure != null) Pressure.Stabilize();

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
        // Don't open while tutorial is still active
        if (TutorialLocked) return;

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
