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
    
    private List<WandererEnemy> puppets = new List<WandererEnemy>();
    private LineRenderer[] tethers;

    private enum State { Spawning, Hiding, Summoning, Panic }
    private State currentState;
    
    private Vector3 hidingSpot;
    private bool isSummoning = false;

    protected override void OnEnemyStart()
    {
        maxHealth = 165f;
        currentHealth = maxHealth;
        
        // Spawn puppets immediately but set them to confused initially
        SpawnPuppets();
        
        // Start by hiding
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
            
            // Get valid WandererEnemy script
            WandererEnemy puppet = puppetObj.GetComponent<WandererEnemy>();
            if (puppet == null) puppet = puppetObj.AddComponent<WandererEnemy>();
            
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

    protected override void OnEnemyUpdate()
    {
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

    void TransitionToState(State newState)
    {
        currentState = newState;
        Debug.Log($"PuppeteerEnemy: Transitioned to {newState}");

        switch (currentState)
        {
            case State.Hiding:
                isSummoning = false;
                UpdateAnimator();
                FindFurthestHidingSpot();
                MovePuppetsToConfusion(true);
                break;
                
            case State.Summoning:
                isSummoning = true;
                UpdateAnimator();
                path = null; // Stop moving
                if (rb != null) rb.linearVelocity = Vector2.zero;
                MovePuppetsToConfusion(false); // regain control
                break;
                
            case State.Panic:
                isSummoning = false;
                UpdateAnimator();
                MovePuppetsToConfusion(true); // lose control
                // Panic ensures we run away immediately
                FindFurthestHidingSpot();
                TransitionToState(State.Hiding); // Immediately start hiding behavior
                break;
        }
    }

    void UpdateHidingState()
    {
        // Move towards hiding spot
        if (Vector3.Distance(transform.position, hidingSpot) < 0.5f)
        {
            // Reached hiding spot
            TransitionToState(State.Summoning);
        }
        else
        {
            // Continue moving to hiding spot
            // We use simple MoveTowards or Pathfinding if available
            // Since hiding spot is static, we can use Pathfinding
            
            if (pathfinding != null && pathfinding.IsGridReady)
            {
                // Basic Pathfollowing manually or reuse base class logic? 
                // Base class UpdatePath uses 'target'. Let's override 'target' logic or just move manually.
                
                // Let's use MoveSafely to go straight to point for now (simple)
                 Vector3 dir = (hidingSpot - transform.position).normalized;
                 MoveSafely(dir, speed * Time.deltaTime);
            }
        }
    }

    void UpdateSummoningState()
    {
        // Just wait here. 
        // If player gets too close? The request says "When it starts shooting".
        // So we just stay here until we take damage.
    }

    void UpdatePanicState()
    {
        // Transient state, usually immediately goes to Hiding
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        
        // Trigger Panic if currently summoning
        if (currentState == State.Summoning)
        {
            TransitionToState(State.Panic);
        }
    }

    void FindFurthestHidingSpot()
    {
        if (parentRoom == null) return;
        
        // Get room corners
        Vector3 roomPos = parentRoom.transform.position;
        Vector2 size = parentRoom.roomSize;
        
        List<Vector3> corners = new List<Vector3>
        {
            roomPos + new Vector3(-size.x/2 + 2, -size.y/2 + 2, 0), // Bottom Left
            roomPos + new Vector3(size.x/2 - 2, -size.y/2 + 2, 0),  // Bottom Right
            roomPos + new Vector3(-size.x/2 + 2, size.y/2 - 2, 0),  // Top Left
            roomPos + new Vector3(size.x/2 - 2, size.y/2 - 2, 0)    // Top Right
        };
        
        // Find corner furthest from player
        Vector3 bestSpot = transform.position;
        float maxDist = -1f;
        
        Vector3 playerPos = (target != null) ? target.position : transform.position;
        
        foreach (var corner in corners)
        {
            float dist = Vector3.Distance(corner, playerPos);
            if (dist > maxDist)
            {
                maxDist = dist;
                bestSpot = corner;
            }
        }
        
        hidingSpot = bestSpot;
        Debug.Log($"PuppeteerEnemy: New hiding spot at {hidingSpot} (Distance: {maxDist})");
    }

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

    void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsSummoning", isSummoning);
        }
    }

    void UpdateTethers()
    {
        for (int i = 0; i < puppets.Count && i < tethers.Length; i++)
        {
            if (tethers[i] != null)
            {
                if (puppets[i] != null)
                {
                    tethers[i].enabled = true;
                    tethers[i].SetPosition(0, transform.position);
                    tethers[i].SetPosition(1, puppets[i].transform.position);
                    
                    // Dim tether if confused
                    Color color = isSummoning ? new Color(0.6f, 0.4f, 0.8f, 0.5f) : new Color(0.3f, 0.3f, 0.3f, 0.2f);
                    tethers[i].startColor = color;
                    tethers[i].endColor = new Color(color.r, color.g, color.b, 0.1f);
                }
                else
                {
                    tethers[i].enabled = false;
                }
            }
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
