using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class BreakableBox : MonoBehaviour
{
    [Header("Box Settings")]
    public float maxHealth = 30f;
    // [Tooltip("The sprite to use for debris generation. MUST be Read/Write Enabled.")]
    // public Sprite debrisSprite; // REMOVED: Using object's own sprite now
    public int debrisColumns = 2;
    public int debrisRows = 2;
    public float debrisScale = 0.3f;

    [Header("UI Settings")]
    public Vector3 healthBarOffset = new Vector3(0, 0.8f, 0);
    public Vector2 healthBarSize = new Vector2(0.6f, 0.03f);

    private float currentHealth;
    private bool healthBarVisible = false;
    private Vector2 lastHitDirection = Vector2.zero;


    
    // UI Components
    private GameObject healthCanvasGO;
    private Image healthFillImage;
    private Canvas healthCanvas;

    void Start()
    {
        currentHealth = maxHealth;
        SetupHealthBar();
        SetHealthBarVisible(false); // Hidden by default
    }

    void LateUpdate()
    {
        if (healthBarVisible && healthCanvasGO != null)
        {
            // Billboard effect
            healthCanvasGO.transform.rotation = Camera.main.transform.rotation;
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, Vector2? hitDirection)
    {
        if (hitDirection.HasValue) lastHitDirection = hitDirection.Value;

        // Show health bar on first hit
        if (!healthBarVisible)
        {
            SetHealthBarVisible(true);
        }

        currentHealth -= damage;
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // 1. Visual Effects (Debris)
        // 1. Visual Effects (Debris)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
             // Use stored hit direction for debris
            SpriteFragmenter.CreateDebris(sr.sprite, transform.position, transform.rotation, debrisColumns, debrisRows, 5f, lastHitDirection != Vector2.zero ? lastHitDirection : null, debrisScale);
        }
        else
        {
            Debug.LogWarning("BreakableBox: No SpriteRenderer or Sprite found for Debris!");
        }

        // 2. Tile Removal - REMOVED per user request (was deleting floor)
        // RemoveTileAtPosition();

        // 3. Drop Loot (Coin)
        DropLoot();

        // 4. Destroy this object
        Destroy(gameObject);
    }

    void RemoveTileAtPosition()
    {
        // Raycast to find the tilemap at this position
        // Or assume standard Grid layout
        Tilemap tilemap = GetComponentInParent<Tilemap>();
        
        // If not a child of a tilemap, look for one at the position
        if (tilemap == null)
        {
            Collider2D hit = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("Obstacle", "Default"));
            if (hit != null)
            {
                tilemap = hit.GetComponent<Tilemap>();
                if (tilemap == null) tilemap = hit.GetComponentInParent<Tilemap>();
            }
        }

        if (tilemap != null)
        {
            Vector3Int cellPos = tilemap.WorldToCell(transform.position);
            tilemap.SetTile(cellPos, null);
        }
        else
        {
            // Fallback: Check for colliders at position and disable them? 
            // The Tilemap logic is requested, so we focus on that.
            // If the box is placed manually without a tilemap parent, we might miss.
            // Let's try finding ANY Tilemap in the scene grid.
            Grid grid = FindFirstObjectByType<Grid>();
            if (grid != null)
            {
                Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>();
                foreach (var map in maps)
                {
                    Vector3Int cellPos = map.WorldToCell(transform.position);
                    if (map.GetTile(cellPos) != null)
                    {
                        map.SetTile(cellPos, null);
                        break; // Found and removed
                    }
                }
            }
        }
    }

    void DropLoot()
    {
        // Replicating EnemyAI Loot Table logic
        // 35% Chance total for any coin
        float roll = Random.value;
        
        // Ensure RoomManager exists
        if (RoomManager.Instance == null || RoomManager.Instance.coinPrefab == null) return;

        int coinCount = 0;
        
        if (roll < 0.05f) coinCount = 3;
        else if (roll < 0.15f) coinCount = 2;
        else if (roll < 0.35f) coinCount = 1;

        for (int i = 0; i < coinCount; i++)
        {
            GameObject prefab = RoomManager.Instance.coinPrefab;
            
            // Mimic Check (12.5%)
            if (Random.value < 0.125f && RoomManager.Instance.mimicPrefab != null)
            {
                prefab = RoomManager.Instance.mimicPrefab;
            }

            Vector2 offset = Random.insideUnitCircle * 0.3f;
            Instantiate(prefab, transform.position + (Vector3)offset, Quaternion.identity);
        }
    }

    // --- Health Bar Setup (Similar to EnemyHealthBar) ---
    void SetupHealthBar()
    {
        healthCanvasGO = new GameObject("HealthBarCanvas");
        healthCanvasGO.transform.SetParent(transform);
        healthCanvasGO.transform.localPosition = healthBarOffset;
        
        healthCanvas = healthCanvasGO.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        healthCanvas.sortingLayerName = "Object"; // Ensure visible
        healthCanvas.sortingOrder = 6;
        
        CanvasScaler scaler = healthCanvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;
        
        // Scale Canvas
        RectTransform canvasRect = healthCanvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = healthBarSize;
        // Make it very small in world space so 1 unit = 1 pixel roughly if not handled, 
        // actually for WorldSpace, sizeDelta is in World Units if scale is 1
        healthCanvasGO.transform.localScale = Vector3.one; 

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(healthCanvasGO.transform, false);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = Color.gray;
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(healthCanvasGO.transform, false);
        healthFillImage = fillGO.AddComponent<Image>();
        healthFillImage.color = Color.yellow; // Yellow bar
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        fillRect.pivot = new Vector2(0, 0.5f); // Pivot Left
    }

    void UpdateHealthBar()
    {
        if (healthFillImage != null)
        {
            float pct = Mathf.Clamp01(currentHealth / maxHealth);
            healthFillImage.rectTransform.localScale = new Vector3(pct, 1, 1);
        }
    }

    void SetHealthBarVisible(bool visible)
    {
        healthBarVisible = visible;
        if (healthCanvasGO != null)
        {
            healthCanvasGO.SetActive(visible);
        }
    }
}
