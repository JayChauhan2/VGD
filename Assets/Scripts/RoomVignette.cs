using UnityEngine;

public class RoomVignette : MonoBehaviour
{
    private static Texture2D cachedGradient; // Static cache so we don't regen 100 times

    [Header("Vignette Settings")]
    public Color overlayColor = Color.black;
    [Range(0f, 1f)] public float clearCenterSize = 0.4f;
    [Range(0.01f, 1f)] public float softness = 0.4f;
    [Range(0f, 1f)] public float edgeDarkness = 1f; // Controls max opacity
    public int resolution = 256;
    
    [Header("Sorting")]
    public string sortingLayerName = "Ground"; // Strictly Background
    public int sortingOrder = 32000; // on Top of Floor tiles, but below Objects

    private void Start()
    {
        // Create Sprite Renderer
        GameObject bgObj = new GameObject("VignetteOverlay");
        bgObj.transform.SetParent(transform);
        bgObj.transform.localPosition = Vector3.zero;
        
        SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
        
        // Generate or Get Texture
        if (cachedGradient == null)
        {
            cachedGradient = GenerateVignetteTexture();
        }

        // Create Sprite
        Rect rect = new Rect(0, 0, cachedGradient.width, cachedGradient.height);
        Sprite vignetteSprite = Sprite.Create(cachedGradient, rect, new Vector2(0.5f, 0.5f), 100f);
        sr.sprite = vignetteSprite;
        
        // Settings
        Color finalColor = overlayColor;
        finalColor.a = edgeDarkness;
        sr.color = finalColor;
        
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;

        // Force Unlit Material (Sprites-Default)
        // This ensures it stays black/transparent regardless of scene lighting
        Material unlitMat = new Material(Shader.Find("Sprites/Default"));
        sr.material = unlitMat;
        
        // Scale to Fit Room
        // Room script has roomSize? Or we find Room component.
        Room room = GetComponent<Room>();
        if (room != null)
        {
            float width = room.roomSize.x;
            float height = room.roomSize.y;
            
            // Texture is resolution x resolution (e.g. 256x256)
            // PixelsPerUnit is 100. So unscaled it is 2.56 x 2.56 units.
            // We need it to be width x height units.
            float spriteUnitSize = resolution / 100f;
            
            bgObj.transform.localScale = new Vector3(width / spriteUnitSize, height / spriteUnitSize, 1f);
        }
        else
        {
            // Fallback if no Room component found locally
             bgObj.transform.localScale = new Vector3(32f / 2.56f, 16f / 2.56f, 1f);
        }
    }

    private Texture2D GenerateVignetteTexture()
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear; // Smooth

        Color[] colors = new Color[resolution * resolution];
        float center = resolution / 2f;
        float maxDist = resolution / 2f * Mathf.Sqrt(2); 
        // Or just edge distance? Usually vignette is circular or elliptical.
        // Let's do elliptical to match rectangular room?
        // Actually, simple radial distance from center (0.5, 0.5) in UV space.

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // UV coordinates 0 to 1
                float u = (float)x / (resolution - 1);
                float v = (float)y / (resolution - 1);
                
                // Distance from center (0.5, 0.5)
                float dist = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                
                // 0 at center, 0.707 at corners.
                // We want clear until 'clearCenterSize' (radius), then fade to 1.
                
                // Remap distance
                // dist 0 -> alpha 0
                // dist 0.2 -> alpha 0 (if clearCenterSize is 0.2)
                // dist 0.5 -> alpha 1
                
                float alpha = Mathf.SmoothStep(clearCenterSize, clearCenterSize + softness, dist * 2f); 
                
                // dist * 2 goes from 0 to ~1.414
                // clearCenterSize approx 0.5 means half screen clear.
                
                colors[y * resolution + x] = new Color(1, 1, 1, alpha); // White color, Alpha controls darkness eventually
            }
        }
        
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }
}
