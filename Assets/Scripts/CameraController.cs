using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    public float smoothTime = 0.2f;
    private Vector3 velocity = Vector3.zero;

    private Vector3 internalPos; // Tracks the "actual" smoothed position without shake


    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        internalPos = transform.position;
    }

    public void MoveTo(Vector3 position)
    {
        // SmoothDamp targets this position
        // We only update the target for the existing SmoothDamp call in LateUpdate?
        // Actually, the original code had targetPosition. Let's keep using a target, 
        // but SmoothDamp applies to internalPos.
        targetPosition = new Vector3(position.x, position.y, transform.position.z);
    }

    private Vector3 targetPosition;

    // Zoom Settings
    public float defaultSize = 10f; 
    public float zoomMultiplier = 0.95f; // 5% zoom in
    public float zoomFollowAmount = 0.05f; // Move 5% towards mouse
    public float zoomSmoothTime = 0.2f; // Smooth transition
    
    private Camera cam;
    private float targetSize;
    private float currentSizeVelocity;
    
    private Vector3 currentZoomOffset = Vector3.zero;
    private Vector3 zoomFollowVelocity = Vector3.zero;

    [Header("Camera Settings")]
    public bool autoAdjustSize = true;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            // Initial setup
            if (cam.orthographic) 
            {
                // If autoAdjust is OFF, trust current camera size as default?
                // Or just trust the inspector value. Let's trust inspector value.
                // defaultSize is already set by inspector.
            }
            else 
            {
                defaultSize = 10f; 
            }
        }
    }



    private Vector3 currentRecoilOffset = Vector3.zero;
    private Vector3 targetRecoilOffset = Vector3.zero;
    private Vector3 recoilVelocity = Vector3.zero;
    public float recoilSmoothTime = 0.1f;

    public void SetRecoilOffset(Vector3 offset)
    {
        targetRecoilOffset = offset;
    }

    void LateUpdate()
    {
        // 1. Smoothly move the "internal" base position
        internalPos = Vector3.SmoothDamp(internalPos, targetPosition, ref velocity, smoothTime);

        // 2. Smoothly move Recoil to target
        currentRecoilOffset = Vector3.SmoothDamp(currentRecoilOffset, targetRecoilOffset, ref recoilVelocity, recoilSmoothTime);

        // 3. Determine Base Size
        float baseSize = defaultSize;
        if (autoAdjustSize && RoomManager.Instance != null)
        {
            float targetHeight = RoomManager.Instance.roomHeight;
            float targetWidth = RoomManager.Instance.roomWidth;
            
            // Height requirement
            float sizeBasedOnHeight = (targetHeight / 2f) + 1f; // +1 padding
            
            // Width requirement
            if (cam != null)
            {
                float sizeBasedOnWidth = (targetWidth / 2f) / cam.aspect + 1f; // +1 padding
                baseSize = Mathf.Max(sizeBasedOnHeight, sizeBasedOnWidth);
                baseSize = Mathf.Max(baseSize, 10f); // Minimum size
            }
        }

        // 4. Handle Zoom & Shift
        Vector3 targetZoomOffset = Vector3.zero;
        float desiredSize = baseSize;

        if (cam != null)
        {
            if (Input.GetMouseButton(1)) // Right Click Held
            {
                desiredSize = baseSize * zoomMultiplier;
                
                // Calculate Mouse Offset from Screen Center (World Space)
                Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0;
                Vector3 screenCenter = internalPos; // Use internal pos as anchor
                
                // Vector from Center to Mouse
                Vector3 direction = mousePos - screenCenter;
                
                // Shift small amount towards mouse
                targetZoomOffset = direction * zoomFollowAmount;
            }
            
            // Smooth Zoom Size
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, desiredSize, ref currentSizeVelocity, zoomSmoothTime);
            
            // Smooth Zoom Position Offset
            currentZoomOffset = Vector3.SmoothDamp(currentZoomOffset, targetZoomOffset, ref zoomFollowVelocity, zoomSmoothTime);
        }

        // 5. Apply all
        transform.position = internalPos + currentRecoilOffset + currentZoomOffset;
    }
}
