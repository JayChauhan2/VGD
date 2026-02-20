using UnityEngine;
using System.Collections;
using System;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 6;
    
    private bool _isDying;

    [SerializeField]
    private int _currentHealth;
    public int CurrentHealth
    {
        get => _currentHealth;
        private set
        {
            _currentHealth = Mathf.Clamp(value, 0, maxHealth);
            Debug.Log($"Player Health: {_currentHealth}/{maxHealth}");
            
            if (_currentHealth <= 0 && gameObject.activeInHierarchy && !_isDying)
            {
                Die();
            }
        }
    }
    
    // Safety check for Inspector changes
    void OnValidate()
    {
        // Added time buffer to prevent death during initial play-mode loading
        if (Application.isPlaying && _currentHealth <= 0 && Time.timeSinceLevelLoad > 0.1f && gameObject.activeInHierarchy)
        {
            Die();
        }
    }
    
    // Fired just before the player dies — listeners can call CancelDeath() to prevent it
    public static event Action OnPlayerDeath;

    public bool IsInvincible { get; private set; }
    private bool _deathCancelled;
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
        // Ensure health is full on awake so the proactive death check doesn't trip on spawn
        _currentHealth = maxHealth;
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
        
        CreateForcefieldVisual();
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
            foreach(var r in forceFieldRenderers)
            {
                r.sortingLayerName = "Object";
            }

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
            // User requested "Object" layer specifically.
            string targetLayerName = "Object";
            
            foreach (var r in forceFieldRenderers)
            {
                if (r != null) 
                {
                    r.sortingOrder = targetOrder;
                    r.sortingLayerName = targetLayerName;
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

        // Death check is now handled automatically by the CurrentHealth property!
        if (CurrentHealth > 0)
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

    /// <summary>Called by FamiliarTotem to prevent the current death.</summary>
    public void CancelDeath()
    {
        _deathCancelled = true;
    }

    /// <summary>Heals the player by the given number of half-hearts, clamped to maxHealth.</summary>
    public void Heal(int halfHearts)
    {
        int newHealth = Mathf.Clamp(_currentHealth + halfHearts, 0, maxHealth);
        CurrentHealth = newHealth;
    }

    void Die()
    {
        if (_isDying) return;
        _isDying = true;
        Debug.Log("Player Died!");

        // Give familiars (e.g. Totem) a chance to cancel death
        _deathCancelled = false;
        OnPlayerDeath?.Invoke();

        if (_deathCancelled)
        {
            // Death was cancelled by a totem — grant brief invincibility
            Debug.Log("PlayerHealth: Death cancelled by Totem!");
            _isDying = false; // Reset so they can die again later
            
            // Make sure health is greater than 0 so the setter doesn't trigger Die() again
            // The Totem familiar calls Heal() which sets CurrentHealth. 
            // Heal() should run BEFORE we reach this point if called in the event handler.
            
            StartCoroutine(InvincibilityRoutine());
            return;
        }

        // Add death logic here (e.g., restart level, show game over screen)
        gameObject.SetActive(false);
    }
}
