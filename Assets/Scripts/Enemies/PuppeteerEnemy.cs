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
        
        // Puppeteer is a ranged summoner, should not deal touch damage
        touchDamageRange = 0f;
        
        // Don't let base class zero our velocity when we have no path (we handle our own movement)
        stopWhenNoPath = false;
        
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
        
        try
        {
            // CRASH FIX: cache the tether material to avoid memory leak
            if (tetherMaterial == null)
            {
                tetherMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            tethers = new LineRenderer[puppetCount];
            
            for (int i = 0; i < puppetCount; i++)
            {
                // Calculate spawn position around puppeteer
                float angle = (360f / puppetCount) * i;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * 2f;
                Vector3 spawnPosition = transform.position + offset;
                
                // BUG FIX: Validate spawn position (must be inside room and not in a wall).
                // Fallback chain: full offset -> half offset -> puppeteer position -> room center.
                // Using room center as final fallback (guaranteed inside bounds) instead of
                // transform.position, which can be at a wall corner and also out of bounds.
                if (!IsPositionValid(spawnPosition))
                {
                    spawnPosition = transform.position + offset * 0.5f;
                    if (!IsPositionValid(spawnPosition))
                    {
                        spawnPosition = transform.position;
                        if (!IsPositionValid(spawnPosition))
                        {
                            // Final safe fallback: room center is always inside bounds
                            spawnPosition = parentRoom.transform.position;
                        }
                    }
                }
                
                // Create puppet from PREFAB
                GameObject puppetObj = Instantiate(puppetPrefab, spawnPosition, Quaternion.identity);
                puppetObj.name = $"Puppet_{i}";
                
                // Get valid PuppetMinion script
                PuppetMinion puppet = puppetObj.GetComponent<PuppetMinion>();
                if (puppet == null) puppet = puppetObj.AddComponent<PuppetMinion>();
                
                // BUG FIX: Assign parentRoom to the puppet BEFORE RegisterEnemy so that
                // when the puppet's Start() runs (which may call IsPositionValid / GetRoomBounds),
                // it already knows which room it belongs to and will stay within its bounds.
                puppet.AssignRoom(parentRoom);
                
                // Register with room
                parentRoom.RegisterEnemy(puppet);
                
                // Store reference
                puppets.Add(puppet);
                
                // Create tether visual
                CreateTether(i, puppetObj);
                
                Debug.Log($"PuppeteerEnemy: Spawned puppet {i} at {spawnPosition}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PuppeteerEnemy: CRASH PREVENTED during SpawnPuppets! Error: {e.Message}\nStack Trace: {e.StackTrace}");
            // Ensure we don't end up in infinite spawning loop if this fails
            hasSpawned = true; 
        }
    }

    private static Material tetherMaterial; // Static cache

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
        lr.material = tetherMaterial; // Use cached material
        
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

    // State stability to prevent jittering
    private float stateTimer = 0f;
    private const float MIN_STATE_DURATION = 0.5f; // Minimum time in a state before switching

    // Store flee state for Update override
    private bool shouldFleeDirectly = false;
    
    // Cache flee direction to prevent jitter near walls
    private Vector2 cachedFleeDirection = Vector2.zero;
    private float fleeDirectionUpdateTimer = 0f;
    private const float FLEE_DIRECTION_UPDATE_INTERVAL = 0.15f; // Update flee direction every 150ms
    
    // Track actual velocity for animation (since base class may see zero velocity when we clear path)
    private Vector2 actualVelocity = Vector2.zero;
    
    // Track if all puppets are dead (permanent panic mode)
    private bool allPuppetsDead = false;
    
    // Random panic movement
    private Vector3 panicTarget;
    private float panicRetargetTimer = 0f;
    
    protected override void OnEnemyUpdate()
    {
        // Update timers
        if (underAttackTimer > 0)
        {
            underAttackTimer -= Time.deltaTime;
        }
        
        stateTimer += Time.deltaTime;

        // Clean up destroyed puppets
        puppets.RemoveAll(p => p == null);
        
        // Check if all puppets are dead (only after spawning has happened)
        if (hasSpawned && puppets.Count == 0 && !allPuppetsDead)
        {
            allPuppetsDead = true;
            Debug.Log("PuppeteerEnemy: All puppets dead! Entering permanent panic mode!");
            TransitionToState(State.Panic);
        }

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
    
    protected override void UpdateAnimation(Vector2 velocity)
    {
        // Use actualVelocity instead of the passed velocity (which may be zero when path is cleared)
        base.UpdateAnimation(actualVelocity);
        
        if (animator != null)
        {
            animator.SetBool("IsSummoning", isSummoning);
        }
    }

    // Apply flee movement in LateUpdate AFTER base Update() physics
    void LateUpdate()
    {
        if (!IsActive || IsKnockedBack) return;
        
        // FALLBACK: Apply smart flee if flagged (overrides base pathfinding velocity)
        if (shouldFleeDirectly && target != null && rb != null)
        {
            // Update flee direction periodically, not every frame (prevents jitter)
            fleeDirectionUpdateTimer -= Time.deltaTime;
            if (fleeDirectionUpdateTimer <= 0f || cachedFleeDirection == Vector2.zero)
            {
                cachedFleeDirection = GetSmartFleeDirection();
                fleeDirectionUpdateTimer = FLEE_DIRECTION_UPDATE_INTERVAL;
            }
            
            Vector2 fleeVelocity = cachedFleeDirection * speed;
            rb.linearVelocity = fleeVelocity;
            
            // Store velocity for animation
            actualVelocity = fleeVelocity;
        }
        else
        {
            // Reset cache when not fleeing
            cachedFleeDirection = Vector2.zero;
            fleeDirectionUpdateTimer = 0f;
            
            // Use rigidbody velocity for animation (from pathfinding)
            if (rb != null)
            {
                actualVelocity = rb.linearVelocity;
            }
        }
    }

    // Smart flee that avoids walls
    Vector2 GetSmartFleeDirection()
    {
        if (target == null) return Vector2.zero;
        
        // Primary flee direction: away from player
        Vector2 awayFromPlayer = (transform.position - target.position).normalized;
        
        // Check if path ahead is clear
        int mask = (obstacleLayers.value != 0) ? obstacleLayers.value : LayerMask.GetMask("Obstacle");
        float checkDistance = 1.5f;
        
        RaycastHit2D hitAhead = Physics2D.Raycast(transform.position, awayFromPlayer, checkDistance, mask);
        
        // If clear, flee directly away
        if (hitAhead.collider == null)
        {
            return awayFromPlayer;
        }
        
        // Wall detected! Try perpendicular directions
        Vector2 perpLeft = new Vector2(-awayFromPlayer.y, awayFromPlayer.x);
        Vector2 perpRight = new Vector2(awayFromPlayer.y, -awayFromPlayer.x);
        
        RaycastHit2D hitLeft = Physics2D.Raycast(transform.position, perpLeft, checkDistance, mask);
        RaycastHit2D hitRight = Physics2D.Raycast(transform.position, perpRight, checkDistance, mask);
        
        // Choose the clearer perpendicular direction
        if (hitLeft.collider == null && hitRight.collider == null)
        {
            // Both clear, pick one that moves us away from player more
            float leftScore = Vector2.Dot(perpLeft, awayFromPlayer);
            float rightScore = Vector2.Dot(perpRight, awayFromPlayer);
            return (leftScore > rightScore) ? perpLeft : perpRight;
        }
        else if (hitLeft.collider == null)
        {
            return perpLeft;
        }
        else if (hitRight.collider == null)
        {
            return perpRight;
        }
        
        // Completely cornered, try to slide along the wall
        if (hitAhead.collider != null)
        {
            Vector2 wallNormal = hitAhead.normal;
            Vector2 slideDir = Vector2.Perpendicular(wallNormal);
            
            // Pick the slide direction that moves us away from player
            if (Vector2.Dot(slideDir, awayFromPlayer) < 0)
            {
                slideDir = -slideDir;
            }
            
            return slideDir;
        }
        
        // Last resort: just move away from player even if blocked
        return awayFromPlayer;
    }

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
        stateTimer = 0f; // Reset timer on state change
        Debug.Log($"PuppeteerEnemy: Transitioned to {newState}");

        switch (currentState)
        {
            case State.Hiding:
                isSummoning = false;
                FindBestHidingSpot(); 
                MovePuppetsToConfusion(true);
                
                // IMMEDIATELY compute path so we don't stand still
                retargetTimer = 0f;
                if (pathfinding != null && pathfinding.IsGridReady)
                {
                    path = pathfinding.FindPath(transform.position, hidingSpot);
                    targetIndex = 0;
                    Debug.Log($"PuppeteerEnemy: Immediate path to {hidingSpot}, path valid: {path != null && path.Count > 0}");
                }
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
                
                // If all puppets are dead, STAY in panic mode permanently
                // Otherwise, immediately transition to Hiding to run away
                if (!allPuppetsDead)
                {
                    TransitionToState(State.Hiding);
                }
                else
                {
                    // Initialize panic target for random movement
                    PickRandomPanicTarget();
                    panicRetargetTimer = Random.Range(0.8f, 1.5f);
                }
                break;
        }
    }

    void UpdateHidingState()
    {
        // 1. Check if we can safely summon
        // Conditions: 
        // - We are hidden from player (Line of Sight blocked)
        // - AND We are NOT under active attack (Timer <= 0)
        // - AND We've been hiding for minimum duration (prevent oscillation)
        bool hasLOS = CheckLineOfSight();
        if (!hasLOS && underAttackTimer <= 0 && stateTimer >= MIN_STATE_DURATION)
        {
            // We are hidden and safe! Switch to summon.
            shouldFleeDirectly = false;
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

        // 3. Determine movement mode
        // Use direct flee if: no valid path AND (player can see us OR we're under attack)
        bool isInDanger = hasLOS || underAttackTimer > 0;
        bool needDirectFlee = (path == null || path.Count == 0) && isInDanger && target != null;
        
        if (needDirectFlee)
        {
            // Clear path so base Update() doesn't apply conflicting movement
            path = null;
            shouldFleeDirectly = true;
        }
        else
        {
            // Use pathfinding normally
            shouldFleeDirectly = false;
        }
    }

    void UpdateSummoningState()
    {
        // If player spots us OR we're being shot at, AND we've been summoning for minimum duration, run!
        bool isInDanger = CheckLineOfSight() || underAttackTimer > 0;
        if (isInDanger && stateTimer >= MIN_STATE_DURATION)
        {
            Debug.Log("PuppeteerEnemy: Player saw me or shot at me! Running!");
            TransitionToState(State.Hiding);
        }
    }

    void UpdatePanicState()
    {
        // If all puppets are dead, stay in panic mode permanently
        if (allPuppetsDead)
        {
            // Determine behavior based on distance to player
            if (target != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, target.position);
                
                // If player is close (within 12 units), flee directly
                if (distanceToPlayer < 12f)
                {
                    // Flee from player using direct flee
                    path = null; // Clear path
                    shouldFleeDirectly = true;
                }
                else
                {
                    // Player is far away, panic run around randomly
                    shouldFleeDirectly = false;
                    
                    // Update panic target periodically
                    panicRetargetTimer -= Time.deltaTime;
                    if (panicRetargetTimer <= 0f)
                    {
                        PickRandomPanicTarget();
                        panicRetargetTimer = Random.Range(0.8f, 1.5f);
                    }
                    
                    // Pathfind to panic target
                    if (pathfinding != null && pathfinding.IsGridReady)
                    {
                        path = pathfinding.FindPath(transform.position, panicTarget);
                        targetIndex = 0;
                    }
                }
            }
            return; // Stay in panic, don't transition out
        }
        
        // Original behavior: Transient state when damaged during summoning
        // (This shouldn't happen anymore since we transition immediately in TransitionToState)
    }

    // --- Helper Methods ---

    // Added to allow configuring which layers block LOS (e.g. for new map tilesets)
    public LayerMask obstacleLayers; 

    bool CheckLineOfSight()
    {
        if (target == null) return false; // Can't see null target

        float distance = Vector3.Distance(transform.position, target.position);
        Vector3 direction = (target.position - transform.position).normalized;

        // Raycast to player using configurable mask
        // If obstacleLayers is not set (Nothing/0), fallback to "Obstacle" string for backward compatibility
        int mask = (obstacleLayers.value != 0) ? obstacleLayers.value : LayerMask.GetMask("Obstacle");

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, mask);

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
            
            int mask = (obstacleLayers.value != 0) ? obstacleLayers.value : LayerMask.GetMask("Obstacle");
            RaycastHit2D hit = Physics2D.Raycast(spot, dirToPlayer, distToPlayer, mask);
            
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

    void PickRandomPanicTarget()
    {
        // Pick a random nearby point to run to (looks panicked and erratic)
        Vector2 randomOffset = Random.insideUnitCircle * 4f;
        Vector3 potentialTarget = transform.position + (Vector3)randomOffset;
        
        // Ensure it's valid (inside room and not in a wall)
        if (IsPositionValid(potentialTarget))
        {
            panicTarget = potentialTarget;
        }
        else
        {
            // Fallback: try room center or just stay put
            if (parentRoom != null)
                panicTarget = parentRoom.transform.position;
            else
                panicTarget = transform.position;
        }
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
