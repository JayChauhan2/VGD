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
        // If visuals not set, assume the entire object is the door visual
        if (doorVisuals == null) doorVisuals = gameObject;
    }

    public void SetLocked(bool locked)
    {
        // "Locked" means the door is invisible/inactive in this context (Isaac style)
        // Or if we want them visible but closed, we'd change a sprite.
        // User asked: "invisible by default" and "unlock" when enemies dead.
        // So Locked = Invisible (Inactive).
        
        bool active = !locked;
        Debug.Log($"Door {name}: SetLocked({locked}) -> Active: {active}. Visuals: {doorVisuals?.name}, Collider: {doorCollider?.name}");
        
        if (doorVisuals != null) doorVisuals.SetActive(active);
        if (doorCollider != null) doorCollider.enabled = active;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (ConnectedRoom != null)
            {
                RoomManager.Instance.EnterRoom(ConnectedRoom, Direction);
            }
        }
    }
}
