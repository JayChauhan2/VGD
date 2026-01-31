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

    public void OnPlayerEnter()
    {
        // 1. Move Camera
        if (CameraController.Instance != null)
        {
            CameraController.Instance.MoveTo(transform.position);
        }

        // 2. Check Enemies (They should have registered themselves by now)
        // No longer finding by children, relying on manual registration
        
        Debug.Log($"Room {name} Entered. Active Enemies: {activeEnemies.Count}");
        PlayerHasEntered = true;

        OnRoomEntered?.Invoke(this);

        if (activeEnemies.Count > 0 && !IsCleared)
        {
            LockDoors();
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null) enemy.SetActive(true);
            }
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

        if (activeEnemies.Count == 0)
        {
            Debug.Log("Room: All enemies dead. Unlocking doors.");
            IsCleared = true;
            UnlockDoors();
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
