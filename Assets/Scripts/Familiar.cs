using UnityEngine;

public class Familiar : MonoBehaviour
{
    [Tooltip("Time it takes to reach the target. Lower is faster/closer.")]
    public float smoothTime = 0.1f;

    [Header("Fallback (Only if Manager missing)")]
    public Transform fallbackPlayer;
    public Vector3 followOffset = Vector3.zero;

    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private bool controlledByManager = false;

    void Start()
    {
        // Try to register with Manager
        if (FamiliarManager.Instance != null)
        {
            FamiliarManager.Instance.RegisterFamiliar(this);
            controlledByManager = true;
        }
        else
        {
            // Fallback logic
            if (fallbackPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) fallbackPlayer = playerObj.transform;
                else
                {
                    var pm = Object.FindFirstObjectByType<PlayerMovement>();
                    // if (pm == null) pm = Object.FindObjectOfType<PlayerMovement>(); // Removed deprecated fallback
                    if (pm != null) fallbackPlayer = pm.transform;
                }
            }
        }
        
        targetPosition = transform.position;

        // Auto-add Attack component if missing
        if (GetComponent<FamiliarAttack>() == null)
        {
            gameObject.AddComponent<FamiliarAttack>();
        }
    }

    void OnDestroy()
    {
        if (FamiliarManager.Instance != null)
        {
            FamiliarManager.Instance.UnregisterFamiliar(this);
        }
    }

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
        controlledByManager = true;
    }

    void LateUpdate()
    {
        if (!controlledByManager && fallbackPlayer != null)
        {
            targetPosition = fallbackPlayer.position + followOffset;
        }

        // Apply movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
    }
}
