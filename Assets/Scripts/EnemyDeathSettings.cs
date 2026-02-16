using UnityEngine;

/// <summary>
/// Global settings for enemy death effects.
/// Create this as a ScriptableObject asset: Right-click in Project > Create > Enemy Death Settings
/// </summary>
[CreateAssetMenu(fileName = "EnemyDeathSettings", menuName = "Game/Enemy Death Settings")]
public class EnemyDeathSettings : ScriptableObject
{
    [Header("Explosion Settings")]
    [Tooltip("Drag your explosion prefab here (with Animator if animated)")]
    public GameObject explosionPrefab;
    
    [Tooltip("Or just drag explosion sprite frames here for simple animation")]
    public Sprite[] explosionFrames;
    
    public float explosionDuration = 0.5f;
    public float explosionScale = 1.0f;
    
    [Header("Ghost Settings")]
    [Tooltip("Optional: Custom ghost sprite. Leave empty to use enemy's sprite")]
    public Sprite ghostSprite;
    
    [Range(0.1f, 2.0f)]
    [Tooltip("Scale of the ghost sprite (0.4 = small, 1.0 = normal size)")]
    public float ghostScale = 0.4f;
    
    public float ghostRiseSpeed = 2.5f;
    public float ghostDuration = 0.5f;
    public float sineWaveAmplitude = 0.15f;
    public float sineWaveFrequency = 2.5f;
    public Color ghostTint = new Color(0.8f, 0.8f, 1f, 1f);
    
    // Singleton instance
    private static EnemyDeathSettings _instance;
    public static EnemyDeathSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<EnemyDeathSettings>("EnemyDeathSettings");
                if (_instance == null)
                {
                    Debug.LogWarning("EnemyDeathSettings not found in Resources folder! Using defaults.");
                }
            }
            return _instance;
        }
    }
}
