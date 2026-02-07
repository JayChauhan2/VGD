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
    
    private float timer;
    private List<EnemyAI> activeSpawns = new List<EnemyAI>();
    private Room currentRoom;
    private static Sprite cachedWalkerSprite;
    private static Sprite cachedSpitterSprite;

    private void Start()
    {
        timer = spawnInterval;
        maxSpawnInterval = spawnInterval; // Default to current setting
        
        // Try to find the room this spawner is in
        currentRoom = Room.GetRoomContaining(transform.position);
        if (currentRoom == null)
        {
            currentRoom = GetComponentInParent<Room>();
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

    private void Update()
    {
        // Cleanup nulls
        activeSpawns.RemoveAll(x => x == null);
        
        // Stop spawning if the room is cleared OR player hasn't entered
        if (currentRoom != null && (currentRoom.IsCleared || !currentRoom.PlayerHasEntered)) return;
        
        // Stop spawning if active enemies limit reached
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

    public void Spawn()
    {
        // Check if we have room for 3 more enemies before spawning
        if (activeSpawns.Count >= maxActiveEnemies) return;
        
        // Spawn 3 enemies at once
        for (int i = 0; i < 3; i++)
        {
            Vector3 spawnPos = transform.position;
            
            GameObject newEnemyObj = null;
            EnemyAI enemyScript = null;

            if (enemyPrefabs != null && enemyPrefabs.Count > 0)
            {
                // Pick random prefab
                int randomIndex = Random.Range(0, enemyPrefabs.Count);
                GameObject selectedPrefab = enemyPrefabs[randomIndex];

                if (selectedPrefab != null)
                {
                    newEnemyObj = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
                    // Also reduce scale of prefabs if needed? User asked: "size is smaller".
                    // Prefer procedural scale for now, assuming user relies on procedural given the context.
                    // But let's safely downscale the prefab instances too.
                    newEnemyObj.transform.localScale = Vector3.one * 0.5f; 
                    enemyScript = newEnemyObj.GetComponent<EnemyAI>();
                }
            }
            
            // Fallback if list is empty or selected prefab was null
            if (newEnemyObj == null)
            {
                // Procedurally generate generic Spitter
                newEnemyObj = CreateProceduralSpitter(spawnPos);
                // Size handled in creation method
                enemyScript = newEnemyObj.GetComponent<EnemyAI>();
            }

            if (enemyScript != null)
            {
                activeSpawns.Add(enemyScript);
                
                // Register with Room if possible so it counts towards clearing the room
                if (currentRoom != null)
                {
                    currentRoom.RegisterEnemy(enemyScript);
                    
                    // Explicitly tell the enemy about the room so it knows who to notify when it dies
                    enemyScript.AssignRoom(currentRoom);
                    
                    // If the room is already active (player is there), ensure enemy wakes up
                    if (currentRoom.PlayerHasEntered && !currentRoom.IsCleared)
                    {
                        enemyScript.SetActive(true);
                    }
                }
                else
                {
                    // If no room, just activate immediately
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
