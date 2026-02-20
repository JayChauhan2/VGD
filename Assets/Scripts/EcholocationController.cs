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
    public float pixelSize = 1f; // Controls pixelation level (1 = off)
    
    // Static multiplier from familiars to increase frequency
    public static float pingIntervalMultiplier = 1.0f;
    private float lastIntervalMultiplier = 1.0f;
    
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
            echolocationMaterial.SetFloat("_Radius", -100.0f); // Hide initially
            pulseOrigin = centerTransform.position;
            echolocationMaterial.SetVector("_Center", pulseOrigin);
            echolocationMaterial.SetFloat("_EdgeWidth", edgeWidth);
            echolocationMaterial.SetColor("_Color", rippleColor);
        }
        
        // Safety check configuration of enemyLayerMask
        if (enemyLayerMask.value == 0)
        {
            Debug.LogWarning("[Echolocation] enemyLayerMask is set to Nothing! Auto-assigning 'Enemy' layer.");
            enemyLayerMask = LayerMask.GetMask("Enemy");
            if (enemyLayerMask.value == 0) 
            {
                 // Fallback if "Enemy" layer doesn't exist by that exact name
                 Debug.LogWarning("[Echolocation] 'Enemy' layer not found. Trying 'Default'.");
                 enemyLayerMask = LayerMask.GetMask("Default");
            }
        }
    }

    [Header("Permanent Visibility")]
    public float playerRadius = 0.5f;

    void Update()
    {
        // Detect frequency shift to adjust timer proportionately (prevents abrupt cutoff)
        if (pingIntervalMultiplier != lastIntervalMultiplier)
        {
            if (lastIntervalMultiplier > 0 && pingIntervalMultiplier > 0)
            {
                // Scale timer so the percentage of progress to the next ping remains consistent
                timer = (timer / (pingInterval * lastIntervalMultiplier)) * (pingInterval * pingIntervalMultiplier);
            }
            lastIntervalMultiplier = pingIntervalMultiplier;
        }

        timer += Time.deltaTime;

        // Apply frequency multiplier to the interval
        float activeInterval = pingInterval * pingIntervalMultiplier;
        // Scale expansion speed so the pulse 'fits' the new window
        float activeExpandSpeed = expandSpeed / pingIntervalMultiplier;

        if (timer >= activeInterval)
        {
            StartPing();
            timer = 0;
        }

        if (isExpanding)
        {
            currentRadius += activeExpandSpeed * Time.deltaTime;

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
            echolocationMaterial.SetFloat("_PixelSize", pixelSize);
            
            if (centerTransform != null)
            {
                echolocationMaterial.SetVector("_PlayerPos", centerTransform.position);
                // Back to basic radius (0.5 default)
                echolocationMaterial.SetFloat("_PlayerRadius", playerRadius);
            }
            // --------------------------------
            
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
                // Must be safely negative (lower than -edgeWidth/2) to avoid drawing a small circle at center
                echolocationMaterial.SetFloat("_Radius", -100f);
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
        // Start radius at negative half-width so the outer edge starts at 0 and expands
        currentRadius = -edgeWidth / 2f;
        
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
                    Debug.Log($"[EcholocationController] Position {position} is jammed by {jammer.name} at distance {distance}.");
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
                //Debug.LogWarning($"[Echolocation] Ping Interval ({pingInterval}s) is shorter than Total Effect Time ({totalEffectTime}s = {travelTime}s travel + {fadeOutDuration}s fade). The ripple will reset before completing.");
            }
        }
    }

    [Header("Detection Settings")]
    public LayerMask enemyLayerMask; // Layer mask to filter enemy detection
    private Collider2D[] hitBuffer = new Collider2D[50]; // Pre-allocated buffer for NonAlloc physics

    void DetectEnemies()
    {
        // Optimization: Use NonAlloc to avoid garbage collection
        int hitCount = Physics2D.OverlapCircleNonAlloc(pulseOrigin, currentRadius, hitBuffer, enemyLayerMask);
        
        // Calculate the inner radius of the echolocation ring
        float innerRadius = currentRadius - edgeWidth;
        // Ensure we don't check negative distances
        if (innerRadius < 0f) innerRadius = 0f;
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hitBuffer[i];
            
            // Check if the object is actually an enemy (double check in case layer mask includes other things)
            // Use TryGetComponent to avoid allocation if not found
            if (hit.TryGetComponent<EnemyAI>(out EnemyAI enemy))
            {
                int id = enemy.GetInstanceID();
                if (!hitEnemies.Contains(id))
                {
                    float dist = Vector2.Distance(pulseOrigin, enemy.transform.position);
                    // Check if the enemy is within the INNER ripple radius
                    if (dist <= innerRadius)
                    {
                        hitEnemies.Add(id);
                        Debug.Log($"[Echolocation] Hit Enemy: {enemy.name} at dist {dist} | Sending to GameHUD");
                        enemy.MarkAsDetected();
                    }
                }
            }
            // Fallback for parent components if the collider is on a child
            else 
            {
                 EnemyAI parentEnemy = hit.GetComponentInParent<EnemyAI>();
                 if (parentEnemy != null)
                 {
                    int id = parentEnemy.GetInstanceID();
                    if (!hitEnemies.Contains(id))
                    {
                        float dist = Vector2.Distance(pulseOrigin, parentEnemy.transform.position);
                        if (dist <= innerRadius)
                        {
                            hitEnemies.Add(id);
                            Debug.Log($"[Echolocation] Hit Enemy (via Parent): {parentEnemy.name} from child {hit.name} at dist {dist} | Sending to GameHUD");
                            parentEnemy.MarkAsDetected();
                        }
                    }
                 }
                 else
                 {
                     // Debug failure to find EnemyAI script
                     // Debug.Log($"[Echolocation] Hit {hit.name} but no EnemyAI script found anywhere on it or its parents.");
                 }
            }
        }
    }
}
