using UnityEngine;
using UnityEngine.UI;

public class CorruptionUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The sprite for the purple bar. The Image component will be created automatically.")]
    public Sprite corruptionBarSprite;

    [Header("Liquid Animation")]
    [Tooltip("Drag the individual frames of your liquid animation here.")]
    public Sprite[] liquidSprites;

    [Tooltip("How fast the liquid plays (Frames per Second).")]
    public float animationSpeed = 12f;
    
    [Tooltip("Scale of the liquid effect.")]
    public Vector3 liquidScale = new Vector3(4f, 4f, 1f);

    [Tooltip("Offset for the liquid effect from the top of the fill.")]
    public float liquidOffsetY = 0f;
    
    [Tooltip("Horizontal offset for the liquid effect.")]
    public float liquidOffsetX = 0f;

    [Tooltip("Minimum Y position for the liquid (local space). Useful if it drops too low.")]
    public float liquidMinY = -1000f;
    
    [Header("Configuration")]
    [Tooltip("Padding for the fill image inside the frame. X = Left/Right, Y = Top/Bottom.")]
    public Vector2 fillPadding = Vector2.zero;

    [Tooltip("If true, the liquid renders ON TOP of the frame. Use checks occlusion.")]
    public bool renderOnTop = false;

    // Internal References
    private Room currentRoom;
    private Image corruptionFillImage;
    private Image liquidImage;
    private RectTransform liquidRect;
    
    // Animation State
    private float animTimer;
    private int currentFrame;

    [Header("Debug")]
    public bool debugMode = false;
    [Range(0f, 1f)]
    public float debugFill = 0.5f;

    private void Start()
    {
        SetupFrame();
        SetupCorruptionImage();
        SetupLiquidImage(); // New simple setup
        SortHierarchy();

        Room.OnRoomEntered += OnRoomEntered;
    }

    private void Update()
    {
        // 1. Animate Liquid
        if (liquidImage != null && liquidSprites != null && liquidSprites.Length > 0)
        {
            animTimer += Time.deltaTime;
            if (animTimer >= 1f / animationSpeed)
            {
                animTimer -= 1f / animationSpeed;
                currentFrame = (currentFrame + 1) % liquidSprites.Length;
                liquidImage.sprite = liquidSprites[currentFrame];
                liquidImage.SetNativeSize(); // Ensure sprite aspect ratio is kept
                
                // Re-apply scale after native size reset (optional, but good for consistency)
                // Actually SetNativeSize might make it too big/small, so we rely on RectTransform scale
            }
            
            // Apply customizable scale every frame to be safe
            if (liquidRect != null) liquidRect.localScale = liquidScale;
        }

        // 2. Debug Loop
        if (debugMode)
        {
            UpdateUI(debugFill);
            SortHierarchy();
        }
    }
    
    private void OnValidate()
    {
        if (liquidRect != null) liquidRect.localScale = liquidScale;
    }

    private void SetupLiquidImage()
    {
        // Check if we already have the child
        Transform child = transform.Find("LiquidEffect");
        if (child != null)
        {
            liquidRect = child.GetComponent<RectTransform>();
            liquidImage = child.GetComponent<Image>();
        }
        else
        {
            GameObject obj = new GameObject("LiquidEffect");
            obj.transform.SetParent(transform, false);
            liquidRect = obj.AddComponent<RectTransform>();
            liquidImage = obj.AddComponent<Image>();
        }

        // Default settings
        liquidImage.raycastTarget = false;
        
        // Initial sprite
        if (liquidSprites != null && liquidSprites.Length > 0)
        {
            liquidImage.sprite = liquidSprites[0];
            liquidImage.SetNativeSize();
        }

        // Pivot/Anchors centering
        liquidRect.anchorMin = new Vector2(0.5f, 0.5f);
        liquidRect.anchorMax = new Vector2(0.5f, 0.5f);
        liquidRect.pivot = new Vector2(0.5f, 0.5f);
        liquidRect.anchoredPosition = Vector2.zero;
        liquidRect.localScale = liquidScale;
    }

    private void UpdateUI(float percent)
    {
        if (corruptionFillImage == null) return;
        
        corruptionFillImage.fillAmount = percent;

        if (liquidRect != null)
        {
            // Position Logic
            RectTransform fillRect = corruptionFillImage.rectTransform;
            float currentFillHeight = fillRect.rect.height;
            float fillBottomLocalY = fillRect.localPosition.y - (currentFillHeight * fillRect.pivot.y);
            float targetY = fillBottomLocalY + (currentFillHeight * percent);
            
            // Add Y offset
            targetY += liquidOffsetY;
            
            // Clamp to minimum Y
            if (targetY < liquidMinY) targetY = liquidMinY;

            // Apply final position
            liquidRect.anchoredPosition = new Vector2(liquidOffsetX, targetY);
        }
    }

    // --- Standard Boilerplate Below (Sort, Frame, Events) ---

    private void SortHierarchy()
    {
        if (corruptionFillImage != null) corruptionFillImage.transform.SetSiblingIndex(0);
        
        Transform frameOverlay = transform.Find("FrameOverlay");
        if (renderOnTop)
        {
            if (frameOverlay != null) frameOverlay.SetSiblingIndex(1);
            if (liquidRect != null) liquidRect.SetAsLastSibling();
        }
        else
        {
            if (liquidRect != null) liquidRect.SetSiblingIndex(1);
            if (frameOverlay != null) frameOverlay.SetAsLastSibling();
        }
    }

    private void SetupFrame()
    {
        Image rootImage = GetComponent<Image>();
        if (rootImage != null && rootImage.enabled)
        {
            Transform existing = transform.Find("FrameOverlay");
            if (existing != null) return;

            GameObject overlayObj = new GameObject("FrameOverlay");
            overlayObj.transform.SetParent(transform, false);
            Image overlayImg = overlayObj.AddComponent<Image>();
            
            overlayImg.sprite = rootImage.sprite;
            overlayImg.color = rootImage.color;
            overlayImg.material = rootImage.material;
            overlayImg.type = rootImage.type;
            overlayImg.fillMethod = rootImage.fillMethod;
            overlayImg.preserveAspect = rootImage.preserveAspect; // Important for frames
            
            // Should match parent exactly
            RectTransform overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            
            rootImage.enabled = false;
        }
    }

    private void SetupCorruptionImage()
    {
        Transform t = transform.Find("CorruptionFill");
        if (t != null)
        {
            corruptionFillImage = t.GetComponent<Image>();
        }
        else
        {
            GameObject fillObj = new GameObject("CorruptionFill");
            fillObj.transform.SetParent(transform, false);
            corruptionFillImage = fillObj.AddComponent<Image>();
        }

        corruptionFillImage.transform.SetAsFirstSibling();
        
        RectTransform rt = corruptionFillImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(fillPadding.x, fillPadding.y);
        rt.offsetMax = new Vector2(-fillPadding.x, -fillPadding.y);

        if (corruptionBarSprite != null) corruptionFillImage.sprite = corruptionBarSprite;
        
        corruptionFillImage.type = Image.Type.Filled;
        corruptionFillImage.fillMethod = Image.FillMethod.Vertical;
        corruptionFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
    }

    private void OnDestroy()
    {
        Room.OnRoomEntered -= OnRoomEntered;
        if (currentRoom != null && currentRoom.Pressure != null)
        {
            currentRoom.Pressure.OnPressureChanged -= UpdateUI;
        }
    }

    private void OnRoomEntered(Room room)
    {
        if (currentRoom != null && currentRoom.Pressure != null)
        {
            currentRoom.Pressure.OnPressureChanged -= UpdateUI;
        }

        currentRoom = room;

        if (currentRoom != null && currentRoom.Pressure != null)
        {
            currentRoom.Pressure.OnPressureChanged += UpdateUI;
            UpdateUI(currentRoom.Pressure.currentPressure / currentRoom.Pressure.maxPressure);
        }
        else
        {
            UpdateUI(0f);
        }
    }
}
