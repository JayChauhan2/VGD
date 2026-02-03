using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Settings")]
    public float detectionRadius = 5f;
    public float collectionRadius = 0.8f; // Slightly larger for easier pickup
    public float smoothTime = 0.005f;
    public float maxSpeed = 40f;
    public float lifetime = 5f;
    
    [Header("References")]
    public Transform playerTransform;

    private Vector3 velocity = Vector3.zero;
    private bool isMagnetized = false;

    private float age = 0f;

    private void Start()
    {
        // Manual timer handles destruction now
        // Destroy(gameObject, lifetime); 

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
            
            // Increment age while waiting, destruction is still valid if no player found
            age += Time.deltaTime;
            if (age >= lifetime) Destroy(gameObject);
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // Check if we should start pulling
        if (distance <= detectionRadius)
        {
            isMagnetized = true;
        }

        // Only count down lifetime if NOT magnetized (not being pulled)
        if (!isMagnetized)
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            // Magnetized behavior
            // Move towards player "reluctantly" then speeding up
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
