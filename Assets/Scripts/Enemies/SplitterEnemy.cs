using UnityEngine;

public class SplitterEnemy : EnemyAI
{
    [Header("Splitter Settings")]
    public int splitCount = 2;
    public GameObject wandererPrefab;
    public float spawnHealth = 25f;

    [Header("Recursion Safety")]
    public int maxGenerations = 2; // Limit how many times it can split (if it spawns more Splitters)
    public int generation = 0; // Current generation index

    protected override void OnEnemyStart()
    {
        maxHealth = 90f;
        currentHealth = maxHealth;
        // speed = 3f; // Removed to allow Inspector value
        
        Debug.Log("SplitterEnemy: Initialized");
    }

    protected override void OnEnemyDeath()
    {
        // Recursion Limit Check
        if (generation >= maxGenerations)
        {
            Debug.Log($"SplitterEnemy: Recursion limit reached (Gen {generation} >= {maxGenerations}). Not spawning splits.");
            return;
        }

        // Spawn smaller enemies on death
        SpawnSplits();
    }

    protected override void UpdateAnimation(Vector2 velocity)
    {
        // Call base for sorting order
        base.UpdateAnimation(velocity);

        // Override sprite flipping to face TOWARD player (opposite of default)
        if (spriteRenderer != null && velocity.sqrMagnitude > 0.01f)
        {
            // Invert the default logic: flip when moving RIGHT, don't flip when moving LEFT
            if (velocity.x < -0.01f) spriteRenderer.flipX = false;
            else if (velocity.x > 0.01f) spriteRenderer.flipX = true;
        }
    }

    void SpawnSplits()
    {
        if (parentRoom == null)
        {
            Debug.LogWarning("SplitterEnemy: No parent room, cannot spawn splits");
            return;
        }

        if (wandererPrefab == null)
        {
            Debug.LogWarning("SplitterEnemy: Wanderer Prefab is NOT assigned in the Inspector! Cannot spawn splits.");
            return;
        }
        
        for (int i = 0; i < splitCount; i++)
        {
            // Calculate spawn position around this enemy
            float angle = (360f / splitCount) * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * 0.5f;
            Vector3 spawnPosition = transform.position + offset;
            
            // Create wanderer enemy from Prefab
            GameObject splitObj = Instantiate(wandererPrefab, spawnPosition, Quaternion.identity);
            
            // Get EnemyAI component
            EnemyAI enemyAI = splitObj.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                // Pass generation count if it's another Splitter
                SplitterEnemy splitSplitter = splitObj.GetComponent<SplitterEnemy>();
                if (splitSplitter != null)
                {
                    splitSplitter.generation = this.generation + 1;
                }

                // IMPORTANT: Start coroutine on the SPAWNED enemy, not this dying one
                // This enemy is about to be destroyed, so coroutines won't complete
                enemyAI.StartCoroutine(InitializeSplitCoroutine(enemyAI, parentRoom, IsActive, spawnHealth));
            }
            else
            {
                Debug.LogWarning($"SplitterEnemy: Spawned object {splitObj.name} is missing an EnemyAI component!");
            }
            
            Debug.Log($"SplitterEnemy: Spawned split {i} from prefab");
        }
    }
    
    
    static System.Collections.IEnumerator InitializeSplitCoroutine(EnemyAI enemyAI, Room room, bool shouldActivate, float healthAmount)
    {
        // Wait for the enemy's Start() to complete
        yield return null;
        
        Debug.Log($"SplitterEnemy: Initializing split {enemyAI.name}");
        
        // Now configure the enemy
        enemyAI.AssignRoom(room);
        
        // Using Safe Setter + Max Health Init
        enemyAI.maxHealth = healthAmount;
        enemyAI.SetCurrentHealth(healthAmount); // Safe initialization
        
        Debug.Log($"SplitterEnemy: Adding SpawnProtection to {enemyAI.name}");
        
        // Add spawn protection (forcefield)
        SpawnProtection protection = enemyAI.gameObject.AddComponent<SpawnProtection>();
        
        Debug.Log($"SplitterEnemy: SpawnProtection added. Component exists: {protection != null}");
        
        // Activate if parent was active
        if (shouldActivate)
        {
            enemyAI.SetActive(true);
        }
        
        Debug.Log($"SplitterEnemy: Split {enemyAI.name} fully initialized with Health {healthAmount}");
    }
}
