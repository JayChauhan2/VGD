using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Settings")]
    public float detectionRadius = 5f;
    public float collectionRadius = 0.8f; // Slightly larger for easier pickup
    public float smoothTime = 0.005f;
    public float maxSpeed = 40f;
    
    [Header("References")]
    public Transform playerTransform;

    private Vector3 velocity = Vector3.zero;
    private bool isMagnetized = false;

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            // Try to find player if not found yet (e.g. if spawned dynamically)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Check if we should start pulling
        if (distance <= detectionRadius)
        {
            isMagnetized = true;
        }

        if (isMagnetized)
        {
            // Move towards player "reluctantly" then speeding up
            // SmoothDamp is perfect for this "spring-like" behavior
            transform.position = Vector3.SmoothDamp(transform.position, playerTransform.position, ref velocity, smoothTime, maxSpeed);

            // Check for collection
            if (distance <= collectionRadius)
            {
                Collect();
            }
        }
    }

    private void Collect()
    {
        Debug.Log("Coin collected");
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.AddCoin(1);
        }
        
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, collectionRadius);
    }
}
