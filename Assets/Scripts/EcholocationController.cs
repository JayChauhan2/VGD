using UnityEngine;

public class EcholocationController : MonoBehaviour
{
    [Header("Settings")]
    public Material echolocationMaterial; // Assign the material used by the Render Feature
    public float expandSpeed = 10f;
    public float maxRadius = 30f;
    public float pingInterval = 2f;
    
    [Header("Visuals")]
    public float edgeWidth = 2f;
    public Color rippleColor = Color.cyan;
    public float fadeOutDuration = 0.5f; // Time for ripple to fade after reaching max radius

    private float currentRadius;
    private bool isExpanding = false;
    private bool isFadingOut = false;
    private float fadeTimer = 0f;
    private float currentAlpha = 1f;
    private float timer = 0f;
    
    // Event for Shadow Stalker enemy visibility
    public static event System.Action OnEcholocationPulse;
    
    // Jammer tracking
    private static System.Collections.Generic.List<EcholocationJammerEnemy> activeJammers = new System.Collections.Generic.List<EcholocationJammerEnemy>();

    void Start()
    {
        // Initialize shader with default values
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetFloat("_Radius", -1.0f); // Hide initially
            echolocationMaterial.SetVector("_Center", transform.position);
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

        // Update Shader
        if (echolocationMaterial != null)
        {
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
        
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetVector("_Center", transform.position);
        }
        
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
}
