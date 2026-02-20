using UnityEngine;

/// <summary>
/// Type3_Totem familiar — intercepts the player's death event and saves their life once.
/// After activating, the familiar is destroyed and removed from the orbit ring.
/// </summary>
public class FamiliarTotem : MonoBehaviour
{
    [Tooltip("Half-hearts to restore on activation (default 3 = 1.5 full hearts).")]
    public int reviveHealth = 3;

    private PlayerHealth playerHealth;
    private bool hasActivated = false;

    void OnEnable()
    {
        PlayerHealth.OnPlayerDeath += OnPlayerDeath;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= OnPlayerDeath;
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }

    void OnPlayerDeath()
    {
        if (hasActivated) return; // Safety — should only fire once anyway
        if (playerHealth == null) return;

        hasActivated = true;

        // Cancel the death
        playerHealth.CancelDeath();

        // Restore health
        playerHealth.Heal(reviveHealth);

        Debug.Log("[FamiliarTotem] Player death intercepted! Reviving with " + reviveHealth + " half-hearts.");

        // Self-destruct — OnDestroy will call FamiliarManager.UnregisterFamiliar automatically
        Destroy(gameObject);
    }
}
