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

    private float currentRadius;
    private bool isExpanding = false;
    private float timer = 0f;

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

            if (currentRadius > maxRadius)
            {
                currentRadius = maxRadius;
                isExpanding = false;
            }
        }

        // Update Shader
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetFloat("_Radius", isExpanding ? currentRadius : maxRadius); // Keep showing max radius or fade?
            // If we want it to disappear, we should animate a fade or set radius to -1
            if (!isExpanding) echolocationMaterial.SetFloat("_Radius", -1);
            
            echolocationMaterial.SetFloat("_EdgeWidth", edgeWidth);
            echolocationMaterial.SetColor("_Color", rippleColor);
            
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
        // Debug.Log($"[Echolocation] Ping Started at {transform.position}");
        // If the previous ping hasn't finished, it will merely restart.
        // To fix "Max Radius not doing anything", the user just needs to set Ping Interval > MaxRadius / Speed.
        // I will add a warning in Start if this config is detected.
        
        currentRadius = 0;
        isExpanding = true;
        
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetVector("_Center", transform.position);
        }
    }
    
    void OnValidate() {
        if (expandSpeed > 0 && maxRadius > 0 && pingInterval > 0) {
            float travelTime = maxRadius / expandSpeed;
            if (pingInterval < travelTime) {
                Debug.LogWarning($"[Echolocation] Ping Interval ({pingInterval}s) is shorter than Travel Time ({travelTime}s). The ripple will reset before reaching Max Radius.");
            }
        }
    }
}
