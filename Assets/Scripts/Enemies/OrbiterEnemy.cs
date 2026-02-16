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
        // speed = 5f; // Removed to allow Inspector value
        
        // Random starting angle
        orbitAngle = Random.Range(0f, 360f);
        dartTimer = dartInterval;
        
        Debug.Log("OrbiterEnemy: Initialized");
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
                    // Check for Line of Sight before attacking
                    Vector3 directionToTarget = (target.position - transform.position).normalized;
                    float distanceToTarget = Vector3.Distance(transform.position, target.position);
                    
                    // Cast a ray to see if we hit an obstacle before the player
                    RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, LayerMask.GetMask("Obstacle"));
                    
                    if (hit.collider == null)
                    {
                        // No obstacle, clear to attack!
                        StartDart();
                    }
                    else
                    {
                        // Blocked by wall, wait a bit and keep orbiting
                        dartTimer = 0.5f; 
                        // Debug.Log("OrbiterEnemy: Attack blocked by wall, waiting...");
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

    void UpdateOrbit()
    {
        // Disable pathfinding, use manual orbit movement
        path = null;
        
        // Increment orbit angle
        orbitAngle += orbitSpeed * Time.deltaTime * 50f; // Multiply for reasonable rotation speed
        if (orbitAngle >= 360f) orbitAngle -= 360f;
        
        // Calculate orbit position around player
        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * orbitRadius;
        Vector3 targetPosition = target.position + offset;
        
        // Only move to target if it's valid, otherwise hold position or move closer to player
        if (IsPositionValid(targetPosition))
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        }
        else
        {
            // If orbit position is blocked, try to move slightly closer to player (tighten orbit)
            Vector3 tighterTarget = target.position + (offset * 0.5f);
            if (IsPositionValid(tighterTarget))
            {
                transform.position = Vector3.MoveTowards(transform.position, tighterTarget, speed * Time.deltaTime);
            }
        }
    }

    void StartDart()
    {
        currentState = OrbiterState.Darting;
        dartStartPosition = transform.position;
        dartTimer = dartDuration;
        
        Debug.Log("OrbiterEnemy: Darting toward player!");
    }

    void UpdateDart()
    {
        // Move quickly toward player
        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * dartSpeed * Time.deltaTime;
        
        dartTimer -= Time.deltaTime;
        if (dartTimer <= 0)
        {
            StartReturn();
        }
    }

    void StartReturn()
    {
        currentState = OrbiterState.Returning;
        
        // Find a valid orbit position
        // Try current angle first
        if (TryGetValidOrbitPosition(orbitAngle, orbitRadius, out orbitTargetPosition))
        {
            // Found valid position at current angle
        }
        else
        {
            // Current angle is blocked (in a wall), search for a free spot
            bool found = false;
            
            // Checks 8 directions (0, 45, 90, etc.)
            for (int i = 0; i < 8; i++)
            {
                float testAngle = i * 45f;
                if (TryGetValidOrbitPosition(testAngle, orbitRadius, out orbitTargetPosition))
                {
                    orbitAngle = testAngle; // Update our orbit angle to this new safe spot
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                // If standard radius failed, try closer
                 for (int i = 0; i < 8; i++)
                {
                    float testAngle = i * 45f;
                    if (TryGetValidOrbitPosition(testAngle, orbitRadius * 0.5f, out orbitTargetPosition))
                    {
                        orbitAngle = testAngle;
                        found = true;
                        break;
                    }
                }
            }
            
            if (!found)
            {
                 // Worst case: Return to player's position (or really close to it)
                 orbitTargetPosition = target.position;
            }
        }

        Debug.Log($"OrbiterEnemy: Returning to orbit at {orbitTargetPosition}");
    }

    bool TryGetValidOrbitPosition(float angle, float radius, out Vector3 position)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * radius;
        position = target.position + offset;
        return IsPositionValid(position);
    }

    void UpdateReturn()
    {
        // Return to orbit position
        transform.position = Vector3.MoveTowards(transform.position, orbitTargetPosition, dartSpeed * Time.deltaTime);
        
        // Check if we're back in orbit
        if (Vector3.Distance(transform.position, orbitTargetPosition) < 0.3f)
        {
            currentState = OrbiterState.Orbiting;
            dartTimer = dartInterval;
        }
    }
}
