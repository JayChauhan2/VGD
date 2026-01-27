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
    
    // Store actual Door scripts
    private Door topDoor;
    private Door bottomDoor;
    private Door leftDoor;
    private Door rightDoor;
    
    private List<EnemyAI> activeEnemies = new List<EnemyAI>();
    public bool IsCleared { get; private set; } = false;

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
        if (neighbor == null)
        {
            // No neighbor, deactivate door entirely (wall)
            if (doorObj != null) doorObj.SetActive(false);
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
            }
        }
    }

    public void OnPlayerEnter()
    {
        // 1. Move Camera
        if (CameraController.Instance != null)
        {
            CameraController.Instance.MoveTo(transform.position);
        }

        // 2. Check Enemies
        FindEnemies();
        
        if (activeEnemies.Count > 0 && !IsCleared)
        {
            LockDoors();
        }
        else
        {
            UnlockDoors();
        }
    }

    private void FindEnemies()
    {
        activeEnemies.Clear();
        // Assume enemies are children of the Room for now, or use bounds check?
        // Ideally, RoomManager spawns enemies as children of the Room.
        // For now, let's look for enemies inside this room's hierarchy
        EnemyAI[] enemies = GetComponentsInChildren<EnemyAI>();
        foreach(var e in enemies)
        {
            activeEnemies.Add(e);
        }
        
        // Note: You might need a way to detect enemies if they aren't children.
        // But spawning them as children is good practice for room tiling.
    }

    public void EnemyDefeated(EnemyAI enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        if (activeEnemies.Count == 0)
        {
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
        if (topDoor != null) topDoor.SetLocked(false);
        if (bottomDoor != null) bottomDoor.SetLocked(false);
        if (leftDoor != null) leftDoor.SetLocked(false);
        if (rightDoor != null) rightDoor.SetLocked(false);
    }
}
