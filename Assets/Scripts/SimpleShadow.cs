using UnityEngine;

public class SimpleShadow : MonoBehaviour
{
    private static Texture2D cachedShadowTexture;

    [Header("Shadow Settings")]
    public Vector3 offset = new Vector3(0, -0.46f, 0); // User tuned value
    public Vector3 scale = new Vector3(0.92f, 0.5133f, 1.4667f); // User tuned value
    [Range(0f, 1f)] public float alpha = 103f / 255f; // User tuned value (~0.404)
    public string sortingLayerName = "Object"; // Back to Object
    public int sortingOrder = -1; // Default fallback

    [Header("Debug")]
    public bool debugMode = false;

    private GameObject shadowObj;
    private SpriteRenderer sr;

    void Start()
    {
        // Don't create if already exists
        Transform existing = transform.Find("BlobShadow");
        if (existing != null)
        {
            shadowObj = existing.gameObject;
            sr = shadowObj.GetComponent<SpriteRenderer>();
        }
        else
        {
            shadowObj = new GameObject("BlobShadow");
            shadowObj.transform.SetParent(transform);
            // Ensure Z is slightly forward (closer to camera) to avoid floor z-fighting
            shadowObj.transform.localPosition = new Vector3(offset.x, offset.y, -0.05f);
            shadowObj.transform.localScale = scale;
            
            sr = shadowObj.AddComponent<SpriteRenderer>();
            
            if (cachedShadowTexture == null || debugMode)
            {
                cachedShadowTexture = GenerateShadowTexture();
            }

            Rect rect = new Rect(0, 0, cachedShadowTexture.width, cachedShadowTexture.height);
            Sprite shadowSprite = Sprite.Create(cachedShadowTexture, rect, new Vector2(0.5f, 0.5f), 100f);
            sr.sprite = shadowSprite;
        }

        // Apply settings
        sr.color = debugMode ? Color.red : new Color(0, 0, 0, alpha);
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        
        // Force Unlit material to stay dark
        // Use URP Unlit if available, else standard default
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        
        sr.material = new Material(shader);
        
        if (debugMode) Debug.Log($"SimpleShadow: Created on {name} | Layer: {sortingLayerName} | Order: {sortingOrder} | Shader: {shader.name}");
    }

    private Texture2D GenerateShadowTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (debugMode)
                {
                    colors[y * size + x] = Color.white; // Solid block for debug
                    continue;
                }

                float u = (float)x / (size - 1);
                float v = (float)y / (size - 1);
                
                float dist = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                
                // Soft circle from center
                // Alpha 1 at center, 0 at edge (0.5 dist)
                
                // Hard Circle Logic for "Solid Black Oval"
                // Distance from center (0.5, 0.5)
                // If inside circle (dist <= 0.5), opaque black. Else transparent.
                
                if (dist <= 0.48f) // Slightly less than 0.5 to leave 1px buffer if needed, but 0.5 is fine
                {
                    colors[y * size + x] = new Color(0, 0, 0, 1f);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }
        
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
        void LateUpdate()
    {
        if (sr == null) return;

        // Find parent renderer if not cached
        SpriteRenderer parentSr = transform.parent?.GetComponent<SpriteRenderer>();
        // For Player, the sprite might be on a child (Model) or parent itself.
        // If SimpleShadow is on the root, and Sprite is on root -> simple.
        // If SimpleShadow is on root, and Sprite is on child -> trickier.
        
        // Let's assume standard setup: Shadow is child of the "Visual" object usually.
        // But we added it to "Player" root. Player sprite is in "Player/Visuals" or just "Player"?
        // PlayerHealth says: playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (parentSr == null)
        {
             // Try to find ANY sibling/parent renderer to sync with
             if (transform.parent != null) parentSr = transform.parent.GetComponentInChildren<SpriteRenderer>();
        }

        if (parentSr != null)
        {
            sr.sortingLayerID = parentSr.sortingLayerID;
            sr.sortingOrder = parentSr.sortingOrder - 1;
        }
    }
}
}
