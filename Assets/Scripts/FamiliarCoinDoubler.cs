using UnityEngine;

/// <summary>
/// Type5_CoinDoubler familiar — while active, doubles all enemy coin drop
/// chance thresholds. EnemyAI.DropLoot() checks IsActive before rolling.
/// </summary>
public class FamiliarCoinDoubler : MonoBehaviour
{
    /// <summary>Set to true while at least one CoinDoubler familiar is active.</summary>
    public static bool IsActive { get; private set; } = false;

    // Track how many doublers are active (supports multiple if desired)
    private static int _activeCount = 0;

    void Start()
    {
        _activeCount++;
        IsActive = _activeCount > 0;
        Debug.Log($"[FamiliarCoinDoubler] Activated. Active doublers: {_activeCount}");
    }

    void OnDestroy()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        IsActive = _activeCount > 0;
        Debug.Log($"[FamiliarCoinDoubler] Destroyed. Active doublers: {_activeCount}");
    }
}
