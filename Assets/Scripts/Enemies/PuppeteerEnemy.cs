using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PuppeteerEnemy : EnemyAI
{
    [Header("Puppeteer Settings")]
    public GameObject puppetPrefab; // Assign in Inspector
    public int puppetCount = 3;
    public float keepDistanceFromPlayer = 8f;
    public float summonCooldown = 2f;
    
    private List<PuppetMinion> puppets = new List<PuppetMinion>();
    private LineRenderer[] tethers;

    private enum State { Spawning, Hiding, Summoning, Panic }
    private State currentState;
    
    private Vector3 hidingSpot;
    private bool isSummoning = false;
    private bool hasSpawned = false;

    protected override void OnEnemyStart()
    {
        maxHealth = 165f;
        currentHealth = maxHealth;
        
        // Start by hiding (don't spawn yet)
        TransitionToState(State.Hiding);
        
        Debug.Log("PuppeteerEnemy: Initialized with State Machine");
    }

    void SpawnPuppets()
    {
        if (parentRoom == null)
        {
            Debug.LogWarning("PuppeteerEnemy: No parent room, cannot spawn puppets");
            return;
        }

        if (puppetPrefab == null)
        {
            Debug.LogError("PuppeteerEnemy: Puppet Prefab is NOT assigned!");
            return;
        }
        
        // CRASH FIX: Prevent infinite recursion if Puppeteer is assigned as its own puppet
        if (puppetPrefab.GetComponent<PuppeteerEnemy>() != null)
        {
            Debug.LogError("PuppeteerEnemy: CRITICAL ERROR! The assigned Puppet Prefab has a PuppeteerEnemy script. This causes infinite recursion/crash. Please assign the Minion/Wanderer prefab instead.");
            return;
        }
        
        tethers = new LineRenderer[puppetCount];
        
        for (int i = 0; i < puppetCount; i++)
        {
            // Calculate spawn position around puppeteer
            float angle = (360f / puppetCount) * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * 2f;
            Vector3 spawnPosition = transform.position + offset;
            
            // Create puppet from PREFAB
            GameObject puppetObj = Instantiate(puppetPrefab, spawnPosition, Quaternion.identity);
            puppetObj.name = $"Puppet_{i}";
            
            // Get valid PuppetMinion script
            PuppetMinion puppet = puppetObj.GetComponent<PuppetMinion>();
            if (puppet == null) puppet = puppetObj.AddComponent<PuppetMinion>();
            
            // Register with room
            parentRoom.RegisterEnemy(puppet);
            
            // Store reference
            puppets.Add(puppet);
            
            // Create tether visual
            CreateTether(i, puppetObj);
            
            Debug.Log($"PuppeteerEnemy: Spawned puppet {i}");
        }
    }

    void CreateTether(int index, GameObject puppet)
    {
        // Create line renderer for visual tether
        GameObject tetherObj = new GameObject($"Tether_{index}");
        tetherObj.transform.SetParent(transform);
        
        LineRenderer lr = tetherObj.AddComponent<LineRenderer>();
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.startColor = new Color(0.6f, 0.4f, 0.8f, 0.5f);
        lr.endColor = new Color(0.6f, 0.4f, 0.8f, 0.2f);
        lr.sortingOrder = -1;
        lr.material = new Material(Shader.Find("Sprites/Default")); // Ensure visibility
        
        tethers[index] = lr;
    }

    // Override UpdatePath to prevent base class from automatically pathfinding to 'target' (Player)
    protected override IEnumerator UpdatePath()
    {
        // We handle our own pathfinding/movement in OnEnemyUpdate state machine
        yield break;
    }

    private Vector3 lastPosition;

    // --- Smart Hiding Variables ---
    private float retargetTimer = 0f;
    private const float RETARGET_INTERVAL = 0.5f;
    
    // Timer to track if we were recently damaged
    private float underAttackTimer = 0f;
    private const float UNDER_ATTACK_DURATION = 3.0f; // Run for 3 seconds after being shot

    protected override void OnEnemyUpdate()
    {
        // Update attack timer
        if (underAttackTimer > 0)
        {
            underAttackTimer -= Time.deltaTime;
        }

        // Clean up destroyed puppets
        puppets.RemoveAll(p => p == null);

        // Update State Machine
        switch (currentState)
        {
            case State.Hiding:
                UpdateHidingState();
                break;
            case State.Summoning:
                UpdateSummoningState();
                break;
            case State.Panic:
                UpdatePanicState();
                break;
        }

        // Always update tethers
        UpdateTethers();
    }
    
    // ... UpdateAnimation ...

    // --- State Logic ---
    
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        
        // Reset the under attack timer whenever we take damage
        underAttackTimer = UNDER_ATTACK_DURATION;
        
        // Trigger Panic if currently summoning
        if (currentState == State.Summoning)
        {
            TransitionToState(State.Panic);
        }
    }

    void TransitionToState(State newState)
    {
        currentState = newState;
        // Debug.Log($"PuppeteerEnemy: Transitioned to {newState}");

        switch (currentState)
        {
            case State.Hiding:
                isSummoning = false;
                FindBestHidingSpot(); 
                MovePuppetsToConfusion(true);
                break;
                
            case State.Summoning:
                isSummoning = true;
                path = null; // Stop moving
                if (rb != null) rb.linearVelocity = Vector2.zero;
                
                // Spawn puppets if first time
                if (!hasSpawned)
                {
                    SpawnPuppets();
                    hasSpawned = true;
                }
                
                MovePuppetsToConfusion(false); // regain control
                break;
                
            case State.Panic:
                isSummoning = false;
                MovePuppetsToConfusion(true);
                // Immediately transition to Hiding to run away
                TransitionToState(State.Hiding); 
                break;
        }
    }

    void UpdateHidingState()
    {
        // 1. Check if we can safely summon
        // Conditions: 
        // - We are hidden from player (Line of Sight blocked)
        // - AND We are NOT under active attack (Timer <= 0)
        if (!CheckLineOfSight() && underAttackTimer <= 0)
        {
            // We are hidden and safe! Switch to summon.
            TransitionToState(State.Summoning);
            return;
        }

        // 2. Navigation & Retargeting
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0)
        {
            retargetTimer = RETARGET_INTERVAL;

            // If we are close to our current "best spot" but still here (meaning still unsafe), 
            // try to find a better one.
            if (Vector3.Distance(transform.position, hidingSpot) < 2.0f)
            {
                FindBestHidingSpot();
            }

            // Refresh Path
            if (pathfinding != null && pathfinding.IsGridReady)
            {
                path = pathfinding.FindPath(transform.position, hidingSpot);
                targetIndex = 0;
            }
        }
    }

    void UpdateSummoningState()
    {
        // If player spots us, run!
        if (CheckLineOfSight())
        {
            // Debug.Log("PuppeteerEnemy: Player saw me! running!");
            TransitionToState(State.Hiding);
        }
    }

    void UpdatePanicState()
    {
        // Transient state, shouldn't stay here
    }

    // --- Helper Methods ---

    bool CheckLineOfSight()
    {
        if (target == null) return false; // Can't see null target

        float distance = Vector3.Distance(transform.position, target.position);
        Vector3 direction = (target.position - transform.position).normalized;

        // Raycast to player
        // Use "Obstacle" layer to check for walls
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, LayerMask.GetMask("Obstacle"));

        // If we hit nothing (or player isn't an obstacle), we have LOS
        // If we hit a wall, we are hidden.
        return hit.collider == null;
    }

    void FindBestHidingSpot()
    {
        if (parentRoom == null || target == null) 
        {
             // Fallback if no room/target
             hidingSpot = transform.position; 
             return;
        }

        Vector3 roomPos = parentRoom.transform.position;
        Vector2 size = parentRoom.roomSize;
        float halfX = size.x / 2f - 2f; // Buffer from walls
        float halfY = size.y / 2f - 2f;

        // Sample candidate points
        List<Vector3> candidates = new List<Vector3>();

        // 1. Corners
        candidates.Add(roomPos + new Vector3(-halfX, -halfY, 0));
        candidates.Add(roomPos + new Vector3(halfX, -halfY, 0));
        candidates.Add(roomPos + new Vector3(-halfX, halfY, 0));
        candidates.Add(roomPos + new Vector3(halfX, halfY, 0));

        // 2. Random points
        for (int i = 0; i < 6; i++)
        {
            float randX = Random.Range(-halfX, halfX);
            float randY = Random.Range(-halfY, halfY);
            candidates.Add(roomPos + new Vector3(randX, randY, 0));
        }

        // Evaluate points
        Vector3 bestSpot = transform.position; // Default stay put
        float bestScore = -1f;

        foreach (Vector3 spot in candidates)
        {
            if (!IsPositionValid(spot)) continue;

            float score = 0;

            // Factor 1: Is it hidden from player? (Heavy Weight)
            float distToPlayer = Vector3.Distance(spot, target.position);
            Vector3 dirToPlayer = (target.position - spot).normalized;
            RaycastHit2D hit = Physics2D.Raycast(spot, dirToPlayer, distToPlayer, LayerMask.GetMask("Obstacle"));
            
            bool isHidden = hit.collider != null;

            if (isHidden) score += 1000f; // Pivotally important
            
            // Factor 2: Distance from player (Further is better)
            score += distToPlayer;

            // Factor 3: Distance from current pos (Closer is better to get there fast)
            // But we prioritize being hidden/far from player more.
            // float distFromSelf = Vector3.Distance(spot, transform.position);
            // score -= distFromSelf * 0.5f;

            if (score > bestScore)
            {
                bestScore = score;
                bestSpot = spot;
            }
        }

        hidingSpot = bestSpot;
        // Debug.Log($"PuppeteerEnemy: New hiding spot: {hidingSpot} (Hidden Score: {bestScore})");
    }

    // FollowPathToHidingSpot logic merged into UpdateHidingState


    void MovePuppetsToConfusion(bool confused)
    {
        foreach (var puppet in puppets)
        {
            if (puppet != null)
            {
                puppet.SetConfusion(confused);
            }
        }
    }

    // UpdateAnimator() is no longer needed as its logic is moved to UpdateAnimation override
    // void UpdateAnimator()
    // {
    //     if (animator != null)
    //     {
    //         animator.SetBool("IsSummoning", isSummoning);
    //     }
    // }

    void UpdateTethers()
    {
        if (tethers == null) return;
        
        for (int i = 0; i < tethers.Length; i++)
        {
            if (tethers[i] == null) continue;
            
            // If puppet is dead/missing, disable tether
            if (i >= puppets.Count || puppets[i] == null)
            {
                tethers[i].enabled = false;
                continue;
            }
            
            tethers[i].enabled = true;
            tethers[i].SetPosition(0, transform.position);
            tethers[i].SetPosition(1, puppets[i].transform.position);
        }
    }

    protected override void OnEnemyDeath()
    {
        MovePuppetsToConfusion(true); // Permanently confused
        Debug.Log("PuppeteerEnemy: Died, puppets are now permanently confused!");
        
        // Destroy tethers
        if (tethers != null)
        {
            foreach (var t in tethers)
            {
                if (t != null) Destroy(t.gameObject);
            }
        }
    }
}
