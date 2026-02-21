using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Configuration")]
    [Tooltip("The enemy prefabs to spawn. If empty, spawns a default Walker.")]
    public List<GameObject> enemyPrefabs; // Changed text to list
    
    [Tooltip("Time in seconds between spawns")]
    public float spawnInterval = 3f;
    
    [Header("Pressure Integration")]
    [Tooltip("Fastest spawn rate at Max Pressure")]
    public float minSpawnInterval = 1f;
    private float maxSpawnInterval; // Will capture initial spawnInterval as max

    [Tooltip("Maximum number of active enemies from this spawner")]
    public int maxActiveEnemies = 5;
    
    [Tooltip("Radius around the spawner to place enemies")]
    public float spawnRadius = 2f;
    
    [Tooltip("Number of enemies to spawn per interval")]
    public int enemiesPerSpawn = 3;

    private float timer;
    private List<EnemyAI> activeSpawns = new List<EnemyAI>();
    private Room currentRoom;
    private static Sprite cachedWalkerSprite;
    private static Sprite cachedSpitterSprite;

    private void Awake()
    {
        timer = spawnInterval;
        maxSpawnInterval = spawnInterval; // Default to current setting
        
        // Prioritize hierarchy to find the correct room reliably
        currentRoom = GetComponentInParent<Room>();
        if (currentRoom == null)
        {
            // Fallback for manually placed spawners not in a room prefab
            currentRoom = Room.GetRoomContaining(transform.position);
        }
        
        if (currentRoom != null)
        {
             currentRoom.RegisterSpawner(this);
        }
        
        Debug.Log($"EnemySpawner: Initialized in Room {(currentRoom != null ? currentRoom.name : "None")}");
    }

    public void AdjustSpawnRate(float pressurePercent)
    {
        // pressurePercent 0 -> maxInterval (slow), 1 -> minInterval (fast)
        spawnInterval = Mathf.Lerp(maxSpawnInterval, minSpawnInterval, pressurePercent);
    }

    private GameObject assignedPrefab;

    private void Start()
    {
        // 1. Assign the Single Random Enemy Type for this Spawner
        if (enemyPrefabs != null && enemyPrefabs.Count > 0)
        {
            int randomIndex = Random.Range(0, enemyPrefabs.Count);
            assignedPrefab = enemyPrefabs[randomIndex];
        }
        else if (RoomManager.Instance != null && RoomManager.Instance.globalEnemyPrefabs != null && RoomManager.Instance.globalEnemyPrefabs.Count > 0)
        {
             int globalIndex = Random.Range(0, RoomManager.Instance.globalEnemyPrefabs.Count);
             assignedPrefab = RoomManager.Instance.globalEnemyPrefabs[globalIndex];
        }
        
        if (assignedPrefab == null)
        {
             Debug.LogError($"EnemySpawner {name}: Could not assign ANY enemy prefab (Local or Global empty).");
        }
        else
        {
             Debug.Log($"EnemySpawner {name}: Assigned Type -> {assignedPrefab.name}");
        }
    }

    private void Update()
    {
        // Cleanup nulls
        activeSpawns.RemoveAll(x => x == null);
        
        // Debugging
        if (currentRoom == null) return;
        if (currentRoom.IsCleared) return; 
        if (!currentRoom.PlayerHasEntered) return;
        
        // Continuous Spawning Logic (Reverted)
        if (activeSpawns.Count < maxActiveEnemies)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                Spawn();
                timer = spawnInterval;
            }
        }
    }

    // How many attempts to find a valid non-overlapping spawn position
    private const int SpawnPositionAttempts = 15;

    /// <summary>
    /// Finds a random walkable point within spawnRadius of the spawner.
    /// Returns the spawner position as a fallback if no clear spot is found.
    /// </summary>
    private Vector3 FindSpawnPosition()
    {
        for (int attempt = 0; attempt < SpawnPositionAttempts; attempt++)
        {
            // Pick a random direction and a random distance within the radius
            // insideUnitCircle gives [0..1] magnitude, multiply by radius
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            // Ensure we are at least half the radius away so we never land ON the spawner
            if (offset.magnitude < spawnRadius * 0.4f)
                offset = offset.normalized * spawnRadius * 0.4f;

            Vector3 candidate = transform.position + new Vector3(offset.x, offset.y, 0f);

            // Check no obstacle overlaps at this candidate (enemy radius ~ 0.3)
            Collider2D hit = Physics2D.OverlapCircle(candidate, 0.4f, LayerMask.GetMask("Obstacle"));
            if (hit == null)
                return candidate; // Valid clear spot found
        }

        // Couldn't find a clear spot; fallback to spawner position
        // (better than silently doing nothing — at least the enemy exists)
        Debug.LogWarning($"EnemySpawner {name}: Could not find clear spawn position after {SpawnPositionAttempts} attempts. Spawning at spawner origin.");
        return transform.position;
    }

    public void Spawn()
    {
        if (assignedPrefab == null) return;

        // Spawn enemies based on configuration
        for (int i = 0; i < enemiesPerSpawn; i++)
        {
            // Check limit again inside loop
            if (activeSpawns.Count >= maxActiveEnemies) return;

            // *** FIX: Use spawnRadius to find a valid nearby position ***
            // Previously this was always transform.position, causing enemies to
            // stack on top of each other and the spawner object itself.
            Vector3 spawnPos = FindSpawnPosition();
            
            GameObject newEnemyObj = Instantiate(assignedPrefab, spawnPos, Quaternion.identity);
            newEnemyObj.transform.localScale = Vector3.one * 0.5f; 
            EnemyAI enemyScript = newEnemyObj.GetComponent<EnemyAI>();
            
            if (enemyScript != null)
            {
                activeSpawns.Add(enemyScript);
                
                // Add Spawn Protection (Forcefield) if not a Mimic
                if (newEnemyObj.GetComponent<MimicEnemy>() == null)
                {
                    SpawnProtection protection = newEnemyObj.AddComponent<SpawnProtection>();
                    protection.duration = 3.0f; // 3 seconds invincibility
                }
                
                // Register with Room
                if (currentRoom != null)
                {
                    enemyScript.AssignRoom(currentRoom);
                    
                    if (currentRoom.PlayerHasEntered && !currentRoom.IsCleared)
                    {
                        enemyScript.SetActive(true);
                    }
                }
                else
                {
                    enemyScript.SetActive(true);
                }
            }
        }
    }



    // fallback procedural generation for "Walker"
    private GameObject CreateProceduralWalker(Vector3 pos)
    {
        GameObject obj = new GameObject("SpawnedWalker");
        obj.transform.position = pos;
        
        // Sprite
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if (cachedWalkerSprite == null) cachedWalkerSprite = CreateCircleSprite();
        sr.sprite = cachedWalkerSprite;
        sr.color = new Color(0.9f, 0.9f, 0.2f); // Bright Yellow
        obj.transform.localScale = Vector3.one * 0.5f;



        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        // Logic
        WandererEnemy wanderer = obj.AddComponent<WandererEnemy>();
        // Ensure stats are set if they aren't in Awake/Start vs OnEnemyStart
        // EnemyAI calls OnEnemyStart in Start(), so this should work fine.
        
        return obj;
    }

    // fallback procedural generation for "Spitter"
    private GameObject CreateProceduralSpitter(Vector3 pos)
    {
        GameObject obj = new GameObject("SpawnedSpitter");
        obj.transform.position = pos;
        
        // Sprite
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if (cachedSpitterSprite == null) cachedSpitterSprite = CreateCircleSprite();
        sr.sprite = cachedSpitterSprite;
        sr.color = Color.green; // Green for Spitter
        obj.transform.localScale = Vector3.one * 0.5f;



        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        // Logic
        SpitterEnemy spitter = obj.AddComponent<SpitterEnemy>();
        
        return obj;
    }

    private Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size*size];
        Vector2 center = new Vector2(size/2f, size/2f);
        float radius = size/2f;

        for(int y=0; y<size; y++)
        {
            for(int x=0; x<size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x,y), center);
                if(dist <= radius) colors[y*size+x] = Color.white;
                else colors[y*size+x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f, 0.5f));
    }
}
