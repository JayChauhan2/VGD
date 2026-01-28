using UnityEngine;

public class Door : MonoBehaviour
{
    public Room ConnectedRoom { get; private set; }
    public Vector2Int Direction { get; private set; }

    // Visuals/Colliders to toggle
    public GameObject doorVisuals; 
    public Collider2D doorCollider;

    public void Initialize(Room connectedRoom, Vector2Int direction)
    {
        ConnectedRoom = connectedRoom;
        Direction = direction;
        
        // Ensure we have references if not set in inspector
        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        
        // Fallback: Check children if not found on root
        if (doorCollider == null)
        {
             doorCollider = GetComponentInChildren<Collider2D>();
             if (doorCollider != null)
             {
                 Debug.LogWarning($"Door {name}: Collider found on child object specifically: {doorCollider.name}. OnTriggerEnter2D might not fire on this parent script!");
             }
        }

        if (doorCollider == null)
        {
            Debug.LogError($"Door {name}: COMPONENT MISSING! No Collider2D found on root or children. Door logic will fail.");
        }
        else
        {
            Debug.Log($"Door {name}: Initialized with Collider on {doorCollider.name} (Trigger: {doorCollider.isTrigger})");
            
            // If collider is on a different object (child), attach forwarder
            if (doorCollider.gameObject != gameObject)
            {
                var forwarder = doorCollider.gameObject.GetComponent<DoorTriggerForwarder>();
                if (forwarder == null) forwarder = doorCollider.gameObject.AddComponent<DoorTriggerForwarder>();
                forwarder.Initialize(this);
                Debug.Log($"Door {name}: Attached Trigger Forwarder to {doorCollider.name}");
            }
        }

        // If visuals not set, assume the entire object is the door visual
        if (doorVisuals == null) doorVisuals = gameObject;
    }

    // Called by DoorTriggerForwarder
    public void OnChildTriggerEnter2D(Collider2D other)
    {
        OnTriggerEnter2D(other);
    }

    public void SetLocked(bool locked)
    {
        // Locked: Visible + Solid Barrier (Not Trigger)
        // Unlocked: Invisible + Exit Trigger (Is Trigger)
        
        Debug.Log($"Door {name}: SetLocked({locked})");

        if (doorCollider != null)
        {
            doorCollider.enabled = true; // Always enabled
            doorCollider.isTrigger = !locked; // Locked=Solid, Unlocked=Trigger
        }

        if (doorVisuals != null)
        {
            if (doorVisuals == gameObject)
            {
                // If visuals are the root object, don't disable gameObject (kills script/collider)
                // Disable Renderer instead if present
                var renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = locked;
            }
            else
            {
                // Visuals are a child/separate object
                doorVisuals.SetActive(locked);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Door {name} triggered by: {other.name} (Tag: {other.tag})");
        if (other.CompareTag("Player"))
        {
            if (ConnectedRoom != null)
            {
                Debug.Log($"Door {name}: Entering room {ConnectedRoom.name} in Direction {Direction}");
                RoomManager.Instance.EnterRoom(ConnectedRoom, Direction);
            }
            else
            {
                Debug.LogError($"Door {name}: Triggered by Player but ConnectedRoom is NULL!");
            }
        }
    }
}
