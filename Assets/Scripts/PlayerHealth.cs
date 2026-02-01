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

    private PlayerMovement playerMovement;
    private GameObject forcefield;

    public float knockbackForce = 10f;

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

        CreateForcefieldVisual();
    }

    private void CreateForcefieldVisual()
    {
        forcefield = new GameObject("Forcefield");
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

        float radius = 0.8f; // Adjust based on player size
        for (int i = 0; i < 50; i++)
        {
            float angle = i * Mathf.PI * 2f / 49;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
        }

        forcefield.SetActive(false);
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
