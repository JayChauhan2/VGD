using UnityEngine;

public enum FamiliarType
{
    Type1,
    Type2,
    Type3,
    Type4,
    Type5
}

public enum FamiliarCharacteristic
{
    Characteristic1,
    Characteristic2,
    Characteristic3,
    Characteristic4,
    Characteristic5
}

public class Familiar : MonoBehaviour
{
    [Header("Identity")]
    public FamiliarType familiarType;
    public FamiliarCharacteristic characteristic;

    [Header("Settings")]
    [Tooltip("Time it takes to reach the target. Lower is faster/closer.")]
    public float smoothTime = 0.3f;
    [Tooltip("Maximum speed the familiar can travel.")]
    public float maxSpeed = 20f;
    [Tooltip("If true, this familiar waits to be picked up.")]
    public bool isWild = true;
    public float pickupRange = 3f;

    [Header("Fallback (Only if Manager missing)")]
    public Transform fallbackPlayer;
    public Vector3 followOffset = Vector3.zero;

    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private bool controlledByManager = false;
    private Transform playerTransform;

    void Start()
    {
        // Auto-add Attack component if missing
        if (GetComponent<FamiliarAttack>() == null)
        {
            gameObject.AddComponent<FamiliarAttack>();
        }

        targetPosition = transform.position;
        
        // Cache player transform
        if (FamiliarManager.Instance != null) playerTransform = FamiliarManager.Instance.player;
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        // If not wild, try to register immediately (e.g. pre-owned familiars)
        if (!isWild)
        {
            RegisterWithManager();
        }
        else
        {
            // Ensure wild familiars are effectively detached logic-wise
            // Visual detachment happens below
        }

        // Detach from parent to ensure independent rotation
        transform.SetParent(null);
    }

    void OnDestroy()
    {
        if (FamiliarManager.Instance != null)
        {
            FamiliarManager.Instance.UnregisterFamiliar(this);
        }
    }

    private void RegisterWithManager()
    {
        if (FamiliarManager.Instance != null)
        {
            FamiliarManager.Instance.RegisterFamiliar(this);
            controlledByManager = true;
        }
        else
        {
            // Fallback logic check
            if (fallbackPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) fallbackPlayer = playerObj.transform;
                else
                {
                    var pm = Object.FindFirstObjectByType<PlayerMovement>();
                    if (pm != null) fallbackPlayer = pm.transform;
                }
            }
        }
    }

    void Update()
    {
        if (isWild)
        {
            CheckForPickup();
        }
    }

    private void CheckForPickup()
    {
        if (playerTransform == null)
        {
            if (FamiliarManager.Instance != null && FamiliarManager.Instance.player != null)
            {
                playerTransform = FamiliarManager.Instance.player;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) playerTransform = playerObj.transform;
            }
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= pickupRange)
            {
                Pickup();
            }
        }
    }

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
        controlledByManager = true;
    }

    private void Pickup()
    {
        isWild = false;
        RegisterWithManager();
        
        Debug.Log($"Picked up Familiar: Type={familiarType}, Characteristic={characteristic}");
        
        // TODO: Trigger notification UI here
    }

    void LateUpdate()
    {
        // If wild, stay put (or maybe idle animation later)
        if (isWild) return;

        if (!controlledByManager && fallbackPlayer != null)
        {
            targetPosition = fallbackPlayer.position + followOffset;
        }

        // Apply movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime, maxSpeed);
    }
}
