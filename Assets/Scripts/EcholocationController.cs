using UnityEngine;

public class EcholocationController : MonoBehaviour
{
    [Header("Settings")]
    public Material echolocationMaterial; // Assign the material used by the Render Feature
    public float expandSpeed = 10f;
    public float maxRadius = 20f;
    public float fadeDuration = 0.5f; // Optional, if we want to fade out

    private float currentRadius;
    private bool isExpanding = false;
    private float timer = 0f;
    public float pingInterval = 2f;

    private Vector3 pingOrigin;

    void Start()
    {
        // Initialize
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetFloat("_Radius", 0);
            echolocationMaterial.SetVector("_Center", transform.position);
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
            echolocationMaterial.SetFloat("_Radius", isExpanding ? currentRadius : -1);
            // _Center is already set in StartPing and should NOT move with the player during the ping
        }
    }

    void StartPing()
    {
        currentRadius = 0;
        isExpanding = true;
        pingOrigin = transform.position;
        
        if (echolocationMaterial != null)
        {
            echolocationMaterial.SetVector("_Center", pingOrigin);
        }
    }
}
