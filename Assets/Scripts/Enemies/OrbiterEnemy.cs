using UnityEngine;

public class OrbiterEnemy : EnemyAI
{
    [Header("Orbiter Settings")]
    public float orbitRadius = 4f;
    public float orbitSpeed = 3f;
    public float dartSpeed = 10f;
    public float dartInterval = 3f;
    public float dartDuration = 0.5f;
    
    private enum OrbiterState { Orbiting, Darting, Returning }
    private OrbiterState currentState = OrbiterState.Orbiting;
    
    private float orbitAngle;
    private float dartTimer;
    private Vector3 dartStartPosition;
    private Vector3 orbitTargetPosition;

    protected override void OnEnemyStart()
    {
        maxHealth = 45f;
        currentHealth = maxHealth;
        
        // Disable automatic pathfinding to player, we handle it manually
        usePathfinding = false;
        
        // Allow physics to control movement when path is null (for Darting)
        stopWhenNoPath = false;
        
        // Random starting angle
        orbitAngle = Random.Range(0f, 360f);
        dartTimer = dartInterval;
        
        Debug.Log("OrbiterEnemy: Initialized with Pathfinding");
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        switch (currentState)
        {
            case OrbiterState.Orbiting:
                UpdateOrbit();
                
                // Check if it's time to dart
                dartTimer -= Time.deltaTime;
                if (dartTimer <= 0)
                {
                    if (CheckLineOfSight())
                    {
                        StartDart();
                    }
                    else
                    {
                        dartTimer = 0.5f; // Wait for LOS
                    }
                }
                break;
                
            case OrbiterState.Darting:
                UpdateDart();
                break;
                
            case OrbiterState.Returning:
                UpdateReturn();
                break;
        }
    }

    private float pathUpdateTimer;

    void UpdateOrbit()
    {
        // Increment orbit angle
        orbitAngle += orbitSpeed * Time.deltaTime * 50f; 
        if (orbitAngle >= 360f) orbitAngle -= 360f;
        
        // Calculate orbit position
        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * orbitRadius;
        Vector3 desiredPosition = target.position + offset;
        
        // Validate position or find better one
        if (!IsPositionValid(desiredPosition))
        {
             // Try to find a valid spot if current one is blocked
             if (TryGetValidOrbitPosition(orbitAngle, orbitRadius, out Vector3 validPos))
             {
                 desiredPosition = validPos;
             }
        }

        // Move towards invalid position anyway? No, let pathfinding handle it.
        // If we are far, pathfind. If close, direct move?
        // Actually, just Pathfind to the desired spot.
        
        RequestPath(desiredPosition);
    }

    void RequestPath(Vector3 destination)
    {
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0 && pathfinding != null && pathfinding.IsGridReady)
        {
            path = pathfinding.FindPath(transform.position, destination);
            targetIndex = 0;
            pathUpdateTimer = 0.2f; // Limit path updates
        }
    }

    bool CheckLineOfSight()
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        
        // Raycast only against Obstacles (Walls), ignoring enemies/projectiles
        // Ensure the enemy itself defaults to a layer that is NOT "Obstacle"
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, LayerMask.GetMask("Obstacle"));
        
        // If we hit nothing (on the Obstacle layer), line of sight is clear
        return hit.collider == null;
    }

    void StartDart()
    {
        currentState = OrbiterState.Darting;
        dartStartPosition = transform.position;
        dartTimer = dartDuration;
        path = null; // Stop pathfinding movement
        
        // Initial burst velocity
        Vector3 direction = (target.position - transform.position).normalized;
        if (rb != null) rb.linearVelocity = direction * dartSpeed;
        
        Debug.Log("OrbiterEnemy: Darting!");
    }

    void UpdateDart()
    {
        // Direct physics movement for impact
        // Velocity is set in StartDart, we can maintain it or home slightly?
        // Let's just let physics carry it for now, like a bullet.
        
        dartTimer -= Time.deltaTime;
        if (dartTimer <= 0)
        {
            StartReturn();
        }
    }

    void StartReturn()
    {
        currentState = OrbiterState.Returning;
        if (rb != null) rb.linearVelocity = Vector2.zero; // Stop dart momentum

        // Find a valid orbit position
        if (TryGetValidOrbitPosition(orbitAngle, orbitRadius, out orbitTargetPosition))
        {
            // Valid
        }
        else
        {
             // Search logic... (simplified from previous iteration for brevity, assuming TryGetValid handles some)
             // Actually, let's keep the search logic to be safe
             bool found = false;
             for (int i = 0; i < 8; i++)
             {
                float testAngle = i * 45f;
                if (TryGetValidOrbitPosition(testAngle, orbitRadius, out orbitTargetPosition))
                {
                    orbitAngle = testAngle;
                    found = true;
                    break;
                }
             }
             
             if (!found) orbitTargetPosition = target.position; // Fallback
        }

        Debug.Log($"OrbiterEnemy: Returning to {orbitTargetPosition}");
    }

    void UpdateReturn()
    {
        RequestPath(orbitTargetPosition);
        
        // Check if we reached it
        if (Vector3.Distance(transform.position, orbitTargetPosition) < 1.0f)
        {
            currentState = OrbiterState.Orbiting;
            dartTimer = dartInterval;
        }
    }

    bool TryGetValidOrbitPosition(float angle, float radius, out Vector3 position)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * radius;
        position = target.position + offset;
        return IsPositionValid(position);
    }
}
