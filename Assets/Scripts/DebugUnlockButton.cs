using UnityEngine;

public class DebugUnlockButton : MonoBehaviour
{
    void Update()
    {
        // Press U key to unlock doors in current room
        if (Input.GetKeyDown(KeyCode.U))
        {
            Debug.Log("DebugUnlock: U key pressed - attempting to unlock current room doors");
            UnlockCurrentRoom();
        }
    }

    public void UnlockCurrentRoom()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("DebugUnlock: Player not found!");
            return;
        }

        Room currentRoom = Room.GetRoomContaining(player.transform.position);
        if (currentRoom == null)
        {
            Debug.LogWarning("DebugUnlock: Player is not in any room!");
            return;
        }

        Debug.Log($"DebugUnlock: Unlocking doors in room {currentRoom.name}");
        currentRoom.ForceUnlockDoors();
    }
}
