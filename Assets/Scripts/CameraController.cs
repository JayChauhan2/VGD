using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    public float smoothTime = 0.2f;
    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        targetPosition = transform.position;
    }

    public void MoveTo(Vector3 position)
    {
        // Keep the camera's Z position
        targetPosition = new Vector3(position.x, position.y, transform.position.z);
    }

    void LateUpdate()
    {
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}
