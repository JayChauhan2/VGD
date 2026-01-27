using UnityEngine;

public class Familiar : MonoBehaviour
{
    [Tooltip("The player to follow. If empty, will auto-find tag 'Player' or PlayerMovement script.")]
    public Transform player;

    [Tooltip("Time it takes to reach the target. Lower is faster/closer.")]
    public float smoothTime = 0.1f;

    [Tooltip("Optional offset from the player center.")]
    public Vector3 followOffset = Vector3.zero;

    private Vector3 currentVelocity;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                // Fallback: Find by type
                var pm = Object.FindFirstObjectByType<PlayerMovement>(); // Unity 2023+
                if (pm == null) pm = Object.FindObjectOfType<PlayerMovement>(); // Old Unity
                
                if (pm != null) player = pm.transform;
            }
        }

        if (player == null)
        {
            Debug.LogWarning("Familiar: Could not find Player! Is there an object with tag 'Player' or PlayerMovement script?");
        }
    }

    void LateUpdate()
    {
        if (player != null)
        {
            Vector3 targetPosition = player.position + followOffset;
            
            // SmoothDamp provides a nice "catch up" effect similar to a trailing familiar
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        }
    }
}
