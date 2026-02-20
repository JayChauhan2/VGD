using UnityEngine;
using System.Collections;

/// <summary>
/// Type2_Health familiar — periodically heals the player by 1 half-heart
/// when their health is at or below the threshold. Spawns a green + particle
/// that flies toward the player as a visual cue.
/// </summary>
public class FamiliarHealth : MonoBehaviour
{
    [Header("Heal Settings")]
    [Tooltip("Seconds between each heal attempt.")]
    public float healInterval = 15f;
    [Tooltip("Player half-hearts at or below which healing triggers. Default 2 = 1 full heart.")]
    public int healthThreshold = 2;
    [Tooltip("Half-hearts to restore each tick.")]
    public int healAmount = 1;

    [Header("Visuals")]
    [Tooltip("Optional prefab for the green + particle that flies to the player. Can be left empty.")]
    public GameObject healParticlePrefab;

    private PlayerHealth playerHealth;

    void Start()
    {
        // Find player health
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();

        StartCoroutine(HealRoutine());
    }

    IEnumerator HealRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(healInterval);

            if (playerHealth == null) continue;

            if (playerHealth.CurrentHealth <= healthThreshold)
            {
                playerHealth.Heal(healAmount);
                SpawnHealEffect();
                Debug.Log($"[FamiliarHealth] Healed player +{healAmount} half-heart(s).");
            }
        }
    }

    void SpawnHealEffect()
    {
        if (healParticlePrefab != null)
        {
            // Instantiate at familiar position — the prefab should handle flying toward the player
            Instantiate(healParticlePrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Fallback: spawn a simple pooled label effect via code
            StartCoroutine(FlyingPlusEffect());
        }
    }

    IEnumerator FlyingPlusEffect()
    {
        // Create a lightweight GameObject with a TextMesh ("+") that floats upward briefly
        GameObject label = new GameObject("HealEffect_+");
        label.transform.position = transform.position;

        var tm = label.AddComponent<TextMesh>();
        tm.text = "+";
        tm.color = new Color(0.2f, 0.9f, 0.2f, 1f); // bright green
        tm.fontSize = 20;
        tm.anchor = TextAnchor.MiddleCenter;

        var mr = label.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "Object";
            mr.sortingOrder = 100;
        }

        // Cache player position for direction
        Vector3 target = playerHealth != null
            ? playerHealth.transform.position + Vector3.up * 0.5f
            : transform.position + Vector3.up * 1.5f;

        float elapsed = 0f;
        float duration = 0.7f;
        Vector3 startPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            label.transform.position = Vector3.Lerp(startPos, target, t);

            // Fade out in the second half
            if (t > 0.5f && tm != null)
            {
                Color c = tm.color;
                c.a = 1f - ((t - 0.5f) / 0.5f);
                tm.color = c;
            }
            yield return null;
        }

        Destroy(label);
    }
}
