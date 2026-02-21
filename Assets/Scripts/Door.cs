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
        
        // If there is no neighbor, this door must remain a solid locked wall.
        if (!locked && ConnectedRoom == null)
        {
            locked = true;
        }

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
                // Just keep it visible
                var renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;
            }
            else
            {
                doorVisuals.SetActive(true);
            }
        }

        // Animation control
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            if (locked)
            {
                anim.Rebind();
                anim.Update(0f);
                anim.speed = 0f;
            }
            else
            {
                if (anim.speed == 0f)
                {
                    anim.speed = 1f;
                    anim.Play(anim.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, 0f); // Play from start
                    StartCoroutine(FreezeAnimationAtEnd(anim));
                }
            }
        }
    }

    private System.Collections.IEnumerator FreezeAnimationAtEnd(Animator anim)
    {
        // Wait a frame for the animation to start playing and update state info
        yield return null;
        
        // Wait until the animation finishes (normalized time >= 1)
        while (anim != null && anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
        {
            yield return null;
        }

        // Freeze it near the very end to prevent it wrapping to 0
        if (anim != null)
        {
            anim.speed = 0f;
            anim.Play(anim.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, 0.99f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Door {name} triggered by: {other.name} (Tag: {other.tag})");
        if (other.CompareTag("Player"))
        {
            if (RoomManager.Instance != null && !RoomManager.Instance.CanTeleport) return;

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
