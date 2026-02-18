using UnityEngine;
using System.Collections.Generic;

public class SpriteFragmenter : MonoBehaviour
{
    /// <summary>
    /// Slices a sprite into pieces and spawns them as physics objects.
    /// </summary>
    /// <param name="sprite">The sprite to slice. Must have Read/Write Enabled.</param>
    /// <param name="position">World position to spawn debris.</param>
    /// <param name="rotation">World rotation for debris.</param>
    /// <param name="columns">Number of columns to slice.</param>
    /// <param name="rows">Number of rows to slice.</param>
    /// <param name="explosionForce">Force to apply to pieces.</param>
    /// <param name="hitDirection">Direction of the hit (to push debris away).</param>
    /// <param name="scaleMultiplier">Scale of the debris pieces.</param>
    public static void CreateDebris(Sprite sprite, Vector3 position, Quaternion rotation, int columns = 2, int rows = 2, float explosionForce = 5f, Vector2? hitDirection = null, float scaleMultiplier = 0.5f)
    {
        if (sprite == null) 
        {
            Debug.LogError("SpriteFragmenter: Source sprite is null! Cannot create debris.");
            return;
        }

        // Ensure texture is readable
        if (!sprite.texture.isReadable)
        {
            Debug.LogError($"SpriteFragmenter: Texture '{sprite.texture.name}' is not readable. Please enable 'Read/Write Enabled' in Import Settings.");
            return;
        }

        Texture2D texture = sprite.texture;
        Rect rect = sprite.rect;
        
        float width = rect.width;
        float height = rect.height;
        
        float pieceWidth = width / columns;
        float pieceHeight = height / rows;
        
        Vector2 pivot = sprite.pivot;
        
        float worldWidth = sprite.bounds.size.x;
        float worldHeight = sprite.bounds.size.y;
        
        float worldPieceWidth = worldWidth / columns;
        float worldPieceHeight = worldHeight / rows;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Rect pieceRect = new Rect(
                    rect.x + (x * pieceWidth), 
                    rect.y + (y * pieceHeight), 
                    pieceWidth, 
                    pieceHeight
                );
                
                Sprite pieceSprite = Sprite.Create(
                    texture, 
                    pieceRect, 
                    new Vector2(0.5f, 0.5f), 
                    sprite.pixelsPerUnit
                );
                
                GameObject piece = new GameObject($"Debris_{x}_{y}");
                
                float localX = (x * worldPieceWidth) + (worldPieceWidth / 2f);
                float localY = (y * worldPieceHeight) + (worldPieceHeight / 2f);
                
                float pivotOffsetX = pivot.x / sprite.pixelsPerUnit;
                float pivotOffsetY = pivot.y / sprite.pixelsPerUnit;
                
                Vector3 offset = new Vector3(
                    localX - pivotOffsetX,
                    localY - pivotOffsetY,
                    0
                );
                
                // Add some randomness to position
                Vector3 randomOffset = (Vector3)Random.insideUnitCircle * (worldPieceWidth * 0.2f);

                piece.transform.position = position + (rotation * offset) + randomOffset;
                piece.transform.rotation = rotation;
                piece.transform.localScale = Vector3.one * scaleMultiplier;

                // Add Components
                SpriteRenderer sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = pieceSprite;
                sr.sortingLayerName = "Object"; 
                sr.sortingOrder = Mathf.RoundToInt(piece.transform.position.y * -100); 

                // DEBUG: Check if layer applies
                if (sr.sortingLayerID == 0 && sr.sortingLayerName != "Default")
                {
                     // Debug.LogError($"SpriteFragmenter: 'Object' sorting layer not found!");
                     sr.sortingLayerName = "Default";
                     sr.sortingOrder = 1000; 
                }

                Rigidbody2D rb = piece.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f; 
                rb.linearDamping = 2f; 
                rb.angularDamping = 2f;
                
                BoxCollider2D col = piece.AddComponent<BoxCollider2D>();
                col.size = new Vector2(worldPieceWidth * 0.8f, worldPieceHeight * 0.8f); 

                Vector2 forceDir;
                if (hitDirection.HasValue)
                {
                    // Push generally in hit direction, with spread
                    Vector2 spread = Random.insideUnitCircle * 0.5f;
                    forceDir = (hitDirection.Value + spread).normalized;
                }
                else
                {
                    // Explode outwards
                    forceDir = (new Vector2(offset.x, offset.y)).normalized + (Random.insideUnitCircle * 0.5f);
                }
                
                rb.AddForce(forceDir.normalized * explosionForce, ForceMode2D.Impulse);
                rb.AddTorque(Random.Range(-20f, 20f), ForceMode2D.Impulse);

                // Add Fader
                piece.AddComponent<DebrisFader>();
            }
        }
        
        Debug.Log($"SpriteFragmenter: Spawned {columns*rows} pieces from {sprite.name}.");
    }

    private static string[] GetLayerNames()
    {
        List<string> layers = new List<string>();
        foreach (var layer in SortingLayer.layers)
        {
            layers.Add($"{layer.name} ({layer.value})");
        }
        return layers.ToArray();
    }
}

public class DebrisFader : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color startColor;
    private float duration = 1.0f;
    private float timer = 0f;
    private float delay = 0.5f; // Wait before fading

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) startColor = sr.color;
        Destroy(gameObject, duration + delay + 0.1f);
    }

    void Update()
    {
        if (sr == null) return;
        
        timer += Time.deltaTime;
        
        if (timer > delay)
        {
            float fadeProgress = (timer - delay) / duration;
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(startColor.a, 0f, fadeProgress);
            sr.color = newColor;
        }
    }
}
