using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    
    [SerializeField]
    private float _currentHealth;
    public float CurrentHealth
    {
        get => _currentHealth;
        private set
        {
            _currentHealth = value;
            Debug.Log($"Player Health: {_currentHealth}/{maxHealth}");
        }
    }
    
    private PlayerMovement playerMovement;

    public float knockbackForce = 10f;

    void Start()
    {
        CurrentHealth = maxHealth;
        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("PlayerHealth: No PlayerMovement script found on this object!");
        }
    }

    public void TakeDamage(float amount, Vector2 knockbackDirection)
    {
        CurrentHealth -= amount;
        
        if (playerMovement != null)
        {
            playerMovement.ApplyKnockback(knockbackDirection * knockbackForce, 0.2f);
        }

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player Died!");
        // Add death logic here (e.g., restart level, show game over screen)
        // For now, just disable the object to visualize death
        gameObject.SetActive(false);
    }
}
