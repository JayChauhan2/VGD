using UnityEngine;

public class EcholocationController : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerTransform; // Assign the Player GameObject transform (leave empty to use this GameObject)
    public Material echolocationMaterial; // Assign the material used by the Render Feature
    public float expandSpeed = 10f;
    public float maxRadius = 30f;
    public float pingInterval = 2f;
    
    [Header("Visuals")]
    public float edgeWidth = 4f;
    public Color rippleColor = Color.cyan;
    [Range(0f, 1f)]
    public float worldDarkness = 0.9f;
    public float fadeOutDuration = 0.5f; // Time for ripple to fade after reaching max radius
    
    private Transform centerTransform; // The actual transform to use for position
    private Vector3 pulseOrigin; // The fixed position where the current pulse started

    private float currentRadius;
    private bool isExpanding = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private float currentAlpha = 1f;
    private float timer = 0f;
    
    // Event for Shadow Stalker enemy visibility
    public static event System.Action OnEcholocationPulse;
    
    // Tracking hit enemies per ping
    private System.Collections.Generic.HashSet<int> hitEnemies = new System.Collections.Generic.HashSet<int>();

    // Jammer tracking
    private static System.Collections.Generic.List<EcholocationJammerEnemy> activeJammers = new System.Collections.Generic.List<EcholocationJammerEnemy>();

    void Start()
    {
        // Determine which transform to use for the center
        centerTransform = playerTransform != null ? playerTransform : transform;
        
        Debug.Log($"[Echolocation] Using transform: {centerTransform.gameObject.name} at position {centerTransform.position}");
        
        // Initialize shader with default values
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetFloat("_Radius", -1.0f); // Hide initially
            pulseOrigin = centerTransform.position;
            echolocationMaterial.SetVector("_Center", pulseOrigin);
            echolocationMaterial.SetFloat("_EdgeWidth", edgeWidth);
            echolocationMaterial.SetColor("_Color", rippleColor);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= pingInterval)
        {
            StartPing();
            timer = 0;
        }

        if (isExpanding)
        {
            currentRadius += expandSpeed * Time.deltaTime;

            if (currentRadius >= maxRadius)
            {
                currentRadius = maxRadius;
                isExpanding = false;
                isFadingOut = true;
                fadeTimer = 0f;
            }

            DetectEnemies();
        }
        else if (isFadingOut)
        {
            fadeTimer += Time.deltaTime;
            currentAlpha = 1f - (fadeTimer / fadeOutDuration);
            
            if (fadeTimer >= fadeOutDuration)
            {
                isFadingOut = false;
                currentAlpha = 0f;
            }
        }

        // Update Shader - Use fixed pulseOrigin instead of moving centerTransform
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetVector("_Center", pulseOrigin);
            echolocationMaterial.SetFloat("_Darkness", worldDarkness);
            
            // Only show ripple if expanding or fading out
            if (isExpanding || isFadingOut)
            {
                echolocationMaterial.SetFloat("_Radius", currentRadius);
                echolocationMaterial.SetFloat("_EdgeWidth", edgeWidth);
                
                // Apply alpha to color for fade-out effect
                Color fadeColor = rippleColor;
                fadeColor.a = currentAlpha;
                echolocationMaterial.SetColor("_Color", fadeColor);
            }
            else
            {
                // Hide the ripple when not active
                echolocationMaterial.SetFloat("_Radius", -1);
            }
            
            // Pass Camera Properties for 2D Reconstruction
            Camera cam = Camera.main;
            if (cam != null)
            {
                echolocationMaterial.SetVector("_CameraPos", cam.transform.position);
                echolocationMaterial.SetFloat("_OrthographicSize", cam.orthographicSize);
                echolocationMaterial.SetFloat("_AspectRatio", cam.aspect);
                echolocationMaterial.SetFloat("_IsOrtho", cam.orthographic ? 1.0f : 0.0f);
            }
        }
    }

    void StartPing()
    {
        currentRadius = 0;
        isExpanding = true;
        isFadingOut = false;
        currentAlpha = 1f;
        fadeTimer = 0f;
        
        // Capture the spawn position
        pulseOrigin = centerTransform.position;
        
        // Debug logging
        Debug.Log($"[Echolocation] Ping started at fixed position: {pulseOrigin}");
        
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetVector("_Center", pulseOrigin);
        }
        else
        {
            Debug.LogError("[Echolocation] Material is NULL!");
        }

        // Clear hit list for new ping
        hitEnemies.Clear();
        
        // Trigger event for Shadow Stalker enemies
        OnEcholocationPulse?.Invoke();
        
        // Notify all Shadow Stalker enemies directly
        ShadowStalkerEnemy[] stalkers = FindObjectsByType<ShadowStalkerEnemy>(FindObjectsSortMode.None);
        foreach (var stalker in stalkers)
        {
            stalker.OnEcholocationPulse();
        }
    }
    
    // Jammer management methods
    public static void RegisterJammer(EcholocationJammerEnemy jammer)
    {
        if (!activeJammers.Contains(jammer))
        {
            activeJammers.Add(jammer);
            Debug.Log("EcholocationController: Jammer registered");
        }
    }
    
    public static void UnregisterJammer(EcholocationJammerEnemy jammer)
    {
        if (activeJammers.Contains(jammer))
        {
            activeJammers.Remove(jammer);
            Debug.Log("EcholocationController: Jammer unregistered");
        }
    }
    
    public static bool IsJammed(Vector3 position)
    {
        foreach (var jammer in activeJammers)
        {
            if (jammer != null && jammer.IsJamming())
            {
                float distance = Vector2.Distance(position, jammer.transform.position);
                if (distance <= jammer.GetJammerRadius())
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    void OnValidate() {
        if (expandSpeed > 0 && maxRadius > 0 && pingInterval > 0) {
            float travelTime = maxRadius / expandSpeed;
            float totalEffectTime = travelTime + fadeOutDuration;
            if (pingInterval < totalEffectTime) {
                Debug.LogWarning($"[Echolocation] Ping Interval ({pingInterval}s) is shorter than Total Effect Time ({totalEffectTime}s = {travelTime}s travel + {fadeOutDuration}s fade). The ripple will reset before completing.");
            }
        }
    }

    void DetectEnemies()
    {
        // Debugging: Check detection
        // cast on ALL layers to ensure we don't miss enemies due to missing "Enemy" layer
        Collider2D[] hits = Physics2D.OverlapCircleAll(pulseOrigin, currentRadius);
        
        foreach (var hit in hits)
        {
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                int id = enemy.GetInstanceID();
                if (!hitEnemies.Contains(id))
                {
                    float dist = Vector2.Distance(pulseOrigin, enemy.transform.position);
                    if (dist <= currentRadius)
                    {
                        hitEnemies.Add(id);
                        Debug.Log($"[Echolocation] Hit Enemy: {enemy.name} at dist {dist}");
                        enemy.MarkAsDetected();
                    }
                }
            }
        }
    }
}
