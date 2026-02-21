using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float speed = 3f;
    public float pathUpdateDelay = 0.2f;
    
    [Header("Loot Settings")]
    public GameObject coinPrefab;

    public virtual Vector3? HealthBarOffsetOverride => null;

    protected Pathfinding pathfinding;
    protected List<Node> path;
    protected int targetIndex;
    
    public bool usePathfinding = true;

    public bool IsActive { get; private set; } = false;
    public bool IsKnockedBack { get; private set; } = false; 
    protected bool isDying = false;
    
    [Tooltip("If true, this enemy counts towards locking the room. Set false for summoned minions or ghosts.")]
    public bool CountTowardsRoomClear = true;

    public float maxHealth = 100f;
    protected float currentHealth;
    protected Room parentRoom;

    public event System.Action<float, float> OnHealthChanged;

    protected Rigidbody2D rb;

    protected Animator animator;
    protected SpriteRenderer spriteRenderer;

    public void AssignRoom(Room room)
    {
        parentRoom = room;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
        }

        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        {
            spriteRenderer.sortingLayerName = "Object";
        }

        if (GetComponent<SimpleShadow>() == null)
        {
            gameObject.AddComponent<SimpleShadow>();
        }
        
        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
            
            if (parentRoom == null && RoomManager.Instance != null && RoomManager.Instance.IsInitialized())
            {
                parentRoom = RoomManager.Instance.GetRoomAt(transform.position);
            }
            
            if (parentRoom == null)
            {
                parentRoom = Room.GetRoomContaining(transform.position);
            }
        }

        if (parentRoom != null)
        {
            parentRoom.RegisterEnemy(this);
        }

        if (target == null)
        {
            FindPlayer();
        }

        CheckPathfinding();
        StartCoroutine(UpdatePath());
        
        OnEnemyStart();
    }
    
    protected virtual void OnEnemyStart() { }
    
    void CheckPathfinding()
    {
        pathfinding = FindFirstObjectByType<Pathfinding>(); 


        if (pathfinding == null) Debug.LogError("EnemyAI: Pathfinding component not found!");
    }

    public void SetCurrentHealth(float amount)
    {
        currentHealth = amount;
        if (currentHealth > maxHealth) maxHealth = currentHealth; 
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public virtual void TakeDamage(float damage)
    {
        if (isDying) return;

        var protection = GetComponent<SpawnProtection>();
        if (protection != null && protection.IsActive)
        {
            return;
        }

        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (currentHealth <= 0)
        {
            isDying = true;
            Die();
        }
    }

    protected virtual void Die()
    {
        EnemyDeathEffect.PlayDeathEffect(transform.position, spriteRenderer?.sprite, transform.localScale.x, parentRoom);
        
        DropLoot();
        OnEnemyDeath();
        
        if (parentRoom != null)
        {
            parentRoom.EnemyDefeated(this);
        }
        Destroy(gameObject);
    }
    
    protected virtual void OnEnemyDeath() { }

    protected virtual IEnumerator UpdatePath()
    {
        while (pathfinding == null || !pathfinding.IsGridReady)
        {
            if (!usePathfinding) yield break; 
            yield return new WaitForSeconds(1.0f);
        }

        while (true)
        {
            if (!usePathfinding) yield break; 

            if (!IsActive || IsKnockedBack) 
            {
                yield return new WaitForSeconds(pathUpdateDelay);
                continue;
            }

            if (target == null)
            {
                FindPlayer();
            }

            if (target != null && pathfinding != null && pathfinding.IsGridReady)
            {
                path = pathfinding.FindPath(transform.position, target.position);
                if (path != null && path.Count > 0)
                {
                    targetIndex = 0;
                }
            }
            yield return new WaitForSeconds(pathUpdateDelay);
        }
    }

    protected bool stopWhenNoPath = true;

    void Update()
    {
        if (!IsActive) return;
        
        if (IsKnockedBack) return; 
        
        OnEnemyUpdate();
        
        Vector2 velocity = Vector2.zero;

        if (path == null || targetIndex >= path.Count) 
        {
            if (stopWhenNoPath && rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            Vector3 currentWaypoint = path[targetIndex].worldPosition;
            
            if (rb != null)
            {
                Vector2 dir = (currentWaypoint - transform.position).normalized;
                velocity = dir * speed;
                rb.linearVelocity = velocity;
            }
            else
            {
                Vector3 nextPosition = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);
                velocity = (nextPosition - transform.position) / Time.deltaTime;
                transform.position = nextPosition;
            }

            if (Vector3.Distance(transform.position, currentWaypoint) < 0.1f)
            {
                targetIndex++;
            }
        }
        
        UpdateAnimation(velocity);
        CheckTouchDamage();
    }
    
    protected virtual void UpdateAnimation(Vector2 velocity)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity.magnitude);
        }

        if (spriteRenderer != null)
        {
             spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * -100);

            if (velocity.sqrMagnitude > 0.01f)
            {
                if (velocity.x < -0.01f) spriteRenderer.flipX = true;
                else if (velocity.x > 0.01f) spriteRenderer.flipX = false;
            }
        }
    }
    
    protected virtual void OnEnemyUpdate() { }

    protected virtual void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            
            IgnoreCollisionWithPlayer(player);
        }
    }

    void IgnoreCollisionWithPlayer(GameObject player)
    {
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D myCol in myColliders)
        {
            foreach (Collider2D playerCol in playerColliders)
            {
                Physics2D.IgnoreCollision(myCol, playerCol, true);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (path != null)
        {
            for (int i = targetIndex; i < path.Count; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(path[i].worldPosition, Vector3.one * 0.2f);

                if (i == targetIndex)
                {
                    Gizmos.DrawLine(transform.position, path[i].worldPosition);
                }
                else
                {
                    Gizmos.DrawLine(path[i-1].worldPosition, path[i].worldPosition);
                }
            }
        }
    }


    private float lastDamageTime;
    public float damageCooldown = 1.0f;
    public float touchDamageRange = 0.8f; 

    protected void CheckTouchDamage()
    {
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.position);
            
            if (distance <= touchDamageRange)
            {
                if (Time.time >= lastDamageTime + damageCooldown)
                {
                    PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        Vector2 knockbackDir = (target.position - transform.position).normalized;
                        playerHealth.TakeDamage(10f, knockbackDir);
                        lastDamageTime = Time.time;
                    }
                }
            }
        }
    }

    private Coroutine activationCoroutine;

    public void SetActive(bool active)
    {
        if (active)
        {
            if (IsActive) return; 

            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            activationCoroutine = StartCoroutine(ActivateRoutine());
        }
        else
        {
            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            IsActive = false;
        }
    }

    private IEnumerator ActivateRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        IsActive = true;
        OnEnemyActivated();
        activationCoroutine = null;
    }
    
    protected virtual void OnEnemyActivated() { }
    
    protected Bounds GetRoomBounds()
    {
        if (parentRoom != null)
        {
            Vector3 center = parentRoom.transform.position;
            Vector2 size = parentRoom.roomSize;
            return new Bounds(center, new Vector3(size.x, size.y, 1f));
        }
        return new Bounds(transform.position, new Vector3(32f, 16f, 1f));
    }
    protected bool IsPositionValid(Vector3 position, float radius = 0.3f)
    {
        Collider2D hit = Physics2D.OverlapCircle(position, radius, LayerMask.GetMask("Obstacle"));
        if (hit != null) return false;
        
        if (parentRoom != null)
        {
            Bounds bounds = GetRoomBounds();
            bounds.Expand(-1f); 
            if (!bounds.Contains(position)) return false;
        }
        
        return true;
    }

    protected void MoveSafely(Vector3 direction, float distance)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance + 0.5f, LayerMask.GetMask("Obstacle"));
        
        if (hit.collider == null)
        {
            transform.position += direction * distance;
        }
        else
        {
            float safeDistance = Mathf.Max(0, hit.distance - 0.5f);
            if (safeDistance > 0)
            {
                transform.position += direction * Mathf.Min(distance, safeDistance);
            }
        }
    }

    public virtual void DropLoot()
    {
        if (coinPrefab == null && RoomManager.Instance != null)
        {
            coinPrefab = RoomManager.Instance.coinPrefab;
        }

        if (coinPrefab == null) 
        {
             return;
        }

        float roll = Random.value;
        int coinCount = 0;

        float multiplier = FamiliarCoinDoubler.IsActive ? 2f : 1f;
        float t3 = Mathf.Min(0.05f * multiplier, 1f);
        float t2 = Mathf.Min(0.15f * multiplier, 1f);
        float t1 = Mathf.Min(0.35f * multiplier, 1f);

        if      (roll < t3) coinCount = 3;
        else if (roll < t2) coinCount = 2;
        else if (roll < t1) coinCount = 1;

        for(int i=0; i<coinCount; i++)
        {
            float mimicRoll = Random.value;
            GameObject prefabToSpawn = coinPrefab;
            
            if (mimicRoll < 0.125f && RoomManager.Instance != null && RoomManager.Instance.mimicPrefab != null)
            {
                prefabToSpawn = RoomManager.Instance.mimicPrefab;
            }
            
            Vector2 offset = Random.insideUnitCircle * 0.5f;
            Instantiate(prefabToSpawn, transform.position + (Vector3)offset, Quaternion.identity);
        }
    }

    public virtual void MarkAsDetected()
    {
        
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.ShowEnemyMarker(transform, Vector3.zero, 2.0f);
        }
    }

    public virtual void ApplyKnockback(Vector2 force, float duration)
    {
        StartCoroutine(KnockbackRoutine(force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 force, float duration)
    {
        IsKnockedBack = true;
        
        float originalDrag = 0f;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; 
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; 
            
            originalDrag = rb.linearDamping;
            rb.linearDamping = 0; 
            
            rb.linearVelocity = force; 
        }
        
        yield return new WaitForSeconds(duration);
        
        if (rb != null)
        {
             rb.linearVelocity = Vector2.zero; 
             rb.linearDamping = originalDrag;
        }
        
        IsKnockedBack = false;
        
        targetIndex = 0; 
        path = null; 
    }
}
