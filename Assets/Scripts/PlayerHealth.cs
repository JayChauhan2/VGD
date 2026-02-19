using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 6;
    
    [SerializeField]
    private int _currentHealth;
    public int CurrentHealth
    {
        get => _currentHealth;
        private set
        {
            _currentHealth = value;
            Debug.Log($"Player Health: {_currentHealth}/{maxHealth}");
        }
    }
    
    public bool IsInvincible { get; private set; }
    public float invincibilityDuration = 2.0f;
    public GameObject forceFieldPrefab; // Custom prefab support
    [Header("Visual Settings")]
    public float forceFieldScale = 0.6f; // Default smaller size
    public bool autoAdjustDuration = true; // Automatically match invincibility to animation length

    private PlayerMovement playerMovement;
    private GameObject forcefield;
    private SpriteRenderer playerSpriteRenderer;
    private Renderer[] forceFieldRenderers;

    public float knockbackForce = 5f;

    void Awake()
    {
        // Enforce maxHealth to 6 (3 hearts) to override any stale Inspector values
        maxHealth = 6;
    }

    void Start()
    {
        CurrentHealth = maxHealth;
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("PlayerHealth: No PlayerMovement script found on this object!");
        }
        
        playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (playerSpriteRenderer == null)
        {
             Debug.LogWarning("PlayerHealth: No SpriteRenderer found on player for sorting reference.");
        }

        // Ensure a SimpleShadow is attached
        if (GetComponent<SimpleShadow>() == null)
        {
            var shadow = gameObject.AddComponent<SimpleShadow>();
            shadow.debugMode = false; 
            // Debug.Log("PlayerHealth: Added SimpleShadow component.");
        }
    }

    private void CreateForcefieldVisual()
    {
        // Option 1: Instantiate custom prefab if assigned
        if (forceFieldPrefab != null)
        {
            forcefield = Instantiate(forceFieldPrefab, transform.position, Quaternion.identity);
            forcefield.transform.SetParent(transform);
            forcefield.transform.localPosition = Vector3.zero;
            forcefield.transform.localScale = Vector3.one * forceFieldScale;
            forcefield.name = "Forcefield_Custom";
            
            // Cache renderers for sorting
            forceFieldRenderers = forcefield.GetComponentsInChildren<Renderer>();

            // Auto-adjust duration if requested
            if (autoAdjustDuration)
            {
                Animator anim = forcefield.GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    // Try to get length of first clip
                    // Note: This is an approximation. If using a state machine, might need refinement.
                    AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
                    if (clips != null && clips.Length > 0)
                    {
                        invincibilityDuration = clips[0].length;
                        Debug.Log($"PlayerHealth: Auto-adjusted invincibility duration to {invincibilityDuration}s (from Animation Clip: {clips[0].name})");
                    }
                }
                else
                {
                    // Check for Particle System
                    ParticleSystem ps = forcefield.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        invincibilityDuration = ps.main.duration;
                        Debug.Log($"PlayerHealth: Auto-adjusted invincibility duration to {invincibilityDuration}s (from Particle System)");
                    }
                }
            }
        }
        else
        {
            // Option 2: Fallback to procedural LineRenderer
            forcefield = new GameObject("Forcefield_Generated");
            forcefield.transform.SetParent(transform);
            forcefield.transform.localPosition = Vector3.zero;

            LineRenderer lr = forcefield.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 50;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0, 1, 1, 0.5f); // Cyan transparent
            lr.endColor = new Color(0, 0, 1, 0.5f);   // Blue transparent
            lr.sortingLayerName = "Object";
            lr.sortingOrder = 10;
            
            forceFieldRenderers = new Renderer[] { lr };

            float radius = 0.8f; // Adjust based on player size
            for (int i = 0; i < 50; i++)
            {
                float angle = i * Mathf.PI * 2f / 49;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
            }
        }

        if (forcefield != null) forcefield.SetActive(false);
    }
    
    void LateUpdate()
    {
        // Dynamically update sorting order to always stay on top of player
        if (forcefield != null && forcefield.activeInHierarchy && playerSpriteRenderer != null && forceFieldRenderers != null)
        {
            int targetOrder = playerSpriteRenderer.sortingOrder + 10;
            int targetLayer = playerSpriteRenderer.sortingLayerID;
            
            foreach (var r in forceFieldRenderers)
            {
                if (r != null) 
                {
                    r.sortingOrder = targetOrder;
                    r.sortingLayerID = targetLayer;
                }
            }
        }
    }

    public void TakeDamage(float amount, Vector2 knockbackDirection)
    {
        // Ignore damage if dashing or already invincible
        if (IsInvincible || (playerMovement != null && playerMovement.IsDashing))
        {
            return;
        }

        // Taking 1 heart of damage regardless of 'amount' float
        CurrentHealth -= 1;
        
        if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(knockbackDirection * knockbackForce, 0.2f);
        }

        // Notify Room of damage (Pressure System)
        Room currentRoom = Room.GetRoomContaining(transform.position);
        if (currentRoom != null)
        {
            currentRoom.OnPlayerDamaged();
        }

        if (CurrentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvincibilityRoutine());
        }
    }

    private IEnumerator InvincibilityRoutine()
    {
        IsInvincible = true;
        if (forcefield != null) forcefield.SetActive(true);

        yield return new WaitForSeconds(invincibilityDuration);

        IsInvincible = false;
        if (forcefield != null) forcefield.SetActive(false);
    }

    void Die()
    {
        Debug.Log("Player Died!");
        // Add death logic here (e.g., restart level, show game over screen)
        gameObject.SetActive(false);
    }
}
