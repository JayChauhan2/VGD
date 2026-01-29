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
        // If Laser stops calling SetRecoilOffset, we should ensure logic elsewhere resets it?
        // Or better: The Laser will reset it to Zero when not shooting.
        currentRecoilOffset = Vector3.SmoothDamp(currentRecoilOffset, targetRecoilOffset, ref recoilVelocity, recoilSmoothTime);

        // 3. Apply both
        transform.position = internalPos + currentRecoilOffset;
    }
}
