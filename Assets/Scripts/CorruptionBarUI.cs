using UnityEngine;

/// <summary>
/// Displays the room's pressure as a vertical liquid bar in world-space.
///
/// SETUP (one-time, in the Inspector):
/// ─────────────────────────────────────────────────────────────────────
/// 1. Create an empty GameObject in your scene (e.g. "CorruptionBar").
/// 2. Attach this script to it.
/// 3. Assign sprites in the Inspector:
///      • Frame Frames   → Drag multiple sprites here (e.g. corruptionbarframe_0, _1...)
///      • Fill Sprite    → corruptionbar_0
///      • Wave Frames    → corruptionliquid_0 … corruptionliquid_7
/// 4. Position the GameObject wherever you want the bar on screen.
/// 5. Adjust fillMinLocalY / fillMaxLocalY at runtime in Play Mode.
///
/// HOW IT WORKS:
/// ─────────────────────────────────────────────────────────────────────
/// • Three child SpriteRenderers are created automatically at runtime.
/// • Subscribes to Room.OnRoomEntered to track the active room's
///   PressureController and update the bar in real time.
/// </summary>
[ExecuteAlways]
public class CorruptionBarUI : MonoBehaviour
{
    // ─── Sprites ────────────────────────────────────────────────────────
    [Header("Sprites")]
    [Tooltip("Frame animation frames (drag multiple sprites here).")]
    public Sprite[] frameFrames;

    [Tooltip("The fill/liquid body sprite (corruptionbar_0).")]
    public Sprite fillSprite;

    [Tooltip("Wave animation frames (corruptionliquid_0 … 7).")]
    public Sprite[] waveFrames;

    // ─── Layout ─────────────────────────────────────────────────────────
    [Header("Layout")]
    [Tooltip("Sorting layer name used for all child renderers.")]
    public string sortingLayerName = "UI";

    [Tooltip("Local Y of the fill child when pressure = 0 (bottom of inner area).")]
    public float fillMinLocalY = -2.0f;

    [Tooltip("Local Y of the fill child when pressure = 100 (top of inner area).")]
    public float fillMaxLocalY = 2.0f;

    [Tooltip("Extra vertical offset for the wave sprite (constant tweak).")]
    public float waveVerticalOffset = 0f;

    [Tooltip("If the fill sprite has transparent pixels at the top, increase this value to subtract that empty space from the wave position (SCALES with bar).")]
    public float fillTopPadding = 0f;

    // ─── Animation ──────────────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("FPS for the liquid wave.")]
    public float waveAnimFPS = 8f;
    
    [Tooltip("FPS for the frame animation.")]
    public float frameAnimFPS = 8f;

    // ─── Internal ────────────────────────────────────────────────────────
    private SpriteRenderer fillRenderer;
    private SpriteRenderer waveRenderer;
    private SpriteRenderer frameRenderer;

    private float currentPressureNorm = 0f;   // 0..1
    private PressureController trackedPressure;

    private float waveTimer;
    private int waveFrame;
    
    private float frameAnimTimer;
    private int frameAnimIndex;

    // ────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        BuildChildren();
    }

    private void OnEnable()
    {
        Room.OnRoomEntered += OnRoomEntered;

        // Try to hook up immediately if a room is already active
        Room[] rooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        foreach (var r in rooms)
        {
            if (r.PlayerHasEntered)
            {
                SubscribeToPressure(r.Pressure);
                break;
            }
        }
    }

    private void OnDisable()
    {
        Room.OnRoomEntered -= OnRoomEntered;
        UnsubscribeFromPressure();
    }

    private void Update()
    {
        AnimateWave();
        AnimateFrame();
        ApplyFill();
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Child Construction

    [ContextMenu("Force Rebuild")]
    public void RebuildChildren()
    {
        // Destroy existing for clean slate
        var fillAnchor = transform.Find("FillAnchor");
        if (fillAnchor != null) DestroyImmediate(fillAnchor.gameObject);

        var existingFill = transform.Find("Fill");
        if (existingFill != null) DestroyImmediate(existingFill.gameObject);
        var existingWave = transform.Find("Wave");
        if (existingWave != null) DestroyImmediate(existingWave.gameObject);
        var existingFrame = transform.Find("Frame");
        if (existingFrame != null) DestroyImmediate(existingFrame.gameObject);
        
        BuildChildren();
    }

    private void BuildChildren()
    {
        // CLEANUP: If we have a FillAnchor (from previous failed attempt), dismantle it.
        Transform anchor = transform.Find("FillAnchor");
        if (anchor != null)
        {
            Transform anchorFill = anchor.Find("Fill");
            if (anchorFill != null)
            {
                anchorFill.SetParent(transform, false);
                anchorFill.localPosition = Vector3.zero;
            }
            if (Application.isPlaying) Destroy(anchor.gameObject);
            else DestroyImmediate(anchor.gameObject);
        }

        // 1. Frame (Order 12)
        frameRenderer = GetOrCreateChild("Frame", 12, frameFrames != null && frameFrames.Length > 0 ? frameFrames[0] : null);

        // 2. Fill (Order 10) - Direct Child
        fillRenderer = GetOrCreateChild("Fill", 10, fillSprite);

        // 3. Wave (Order 11) - Direct Child
        waveRenderer = GetOrCreateChild("Wave", 11, waveFrames != null && waveFrames.Length > 0 ? waveFrames[0] : null);
    }

    private SpriteRenderer GetOrCreateChild(string childName, int sortOrder, Sprite sprite)
    {
        Transform existing = transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = Vector3.one;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();

        sr.sprite           = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder     = sortOrder;
        sr.drawMode         = SpriteDrawMode.Simple;

        return sr;
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Pressure Subscription

    private void OnRoomEntered(Room room)
    {
        SubscribeToPressure(room?.Pressure);
        // Reset bar when entering a new room
        currentPressureNorm = 0f;
    }

    private void SubscribeToPressure(PressureController pc)
    {
        UnsubscribeFromPressure();
        trackedPressure = pc;
        if (trackedPressure != null)
            trackedPressure.OnPressureChanged += OnPressureChanged;
    }

    private void UnsubscribeFromPressure()
    {
        if (trackedPressure != null)
            trackedPressure.OnPressureChanged -= OnPressureChanged;
        trackedPressure = null;
    }

    private void OnPressureChanged(float normalizedPressure)
    {
        currentPressureNorm = Mathf.Clamp01(normalizedPressure);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Visuals

    /// <summary>
    /// Moves and scales the fill child to represent the current pressure.
    /// The fill sprite is anchored at its bottom and grows upward.
    /// </summary>
    private void ApplyFill()
    {
        if (fillRenderer == null || waveRenderer == null) return;

        // 1. Calculate target world height of the fill based on pressure
        //    fillTopY is the theoretical top of the sprite bounds
        float fillTopY = Mathf.Lerp(fillMinLocalY, fillMaxLocalY, currentPressureNorm);
        float targetHeight = Mathf.Max(fillTopY - fillMinLocalY, 0f);

        // 2. Get the sprite's native size (in world units) from its bounds
        float nativeHeight = fillSprite != null ? fillSprite.bounds.size.y : 1f;

        // 3. Calculate scale required to reach target height
        float scaleY = targetHeight > 0f ? targetHeight / nativeHeight : 0f;
        
        // 4. Position the Fill Sprite
        //    We want the BOTTOM of the sprite (visual bottom) to be at fillMinLocalY.
        //    The sprite's bottom relative to its pivot is bounds.min.y.
        float fillBottomOffset = fillSprite != null ? fillSprite.bounds.min.y : -0.5f;
        float fillPosY = fillMinLocalY - (fillBottomOffset * scaleY);

        fillRenderer.transform.localPosition = new Vector3(0f, fillPosY, 0f);
        fillRenderer.transform.localScale    = new Vector3(1f, scaleY, 1f);

        // 5. Position the Wave Sprite
        //    Target: Visual Top of the fill.
        //    Visual Top = Bounds Top - (Padding * Scale).
        //    Bounds Top = fillTopY.
        float visualFillTop = fillTopY - (fillTopPadding * scaleY);
        
        Sprite currentWave = waveRenderer.sprite;
        float waveBottomOffset = currentWave != null ? currentWave.bounds.min.y : -0.5f;
        
        // Wave Y = Visual Top + Offset - Wave Bottom Offset
        float wavePosY = (visualFillTop + waveVerticalOffset) - waveBottomOffset;

        waveRenderer.transform.localPosition = new Vector3(0f, wavePosY, 0f);

        // Hide wave when bar is essentially empty
        waveRenderer.enabled = targetHeight > 0.01f;
    }

    /// <summary>
    /// Cycles through wave frames at waveAnimFPS.
    /// </summary>
    private void AnimateWave()
    {
        if (waveRenderer == null || waveFrames == null || waveFrames.Length == 0) return;

        waveTimer += Time.deltaTime;
        float timePerFrame = 1f / Mathf.Max(waveAnimFPS, 0.1f);

        if (waveTimer >= timePerFrame)
        {
            waveTimer -= timePerFrame;
            waveFrame = (waveFrame + 1) % waveFrames.Length;
            // Update the sprite
            waveRenderer.sprite = waveFrames[waveFrame];
            // Re-apply position so the new frame's bounds are accounted for immediately
            ApplyFill();
        }
    }
    
    /// <summary>
    /// Cycles through frame animation sprites.
    /// </summary>
    private void AnimateFrame()
    {
        if (frameRenderer == null || frameFrames == null || frameFrames.Length == 0) return;

        frameAnimTimer += Time.deltaTime;
        float timePerFrame = 1f / Mathf.Max(frameAnimFPS, 0.1f);

        if (frameAnimTimer >= timePerFrame)
        {
            frameAnimTimer -= timePerFrame;
            frameAnimIndex = (frameAnimIndex + 1) % frameFrames.Length;
            frameRenderer.sprite = frameFrames[frameAnimIndex];
        }
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Editor Helpers

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Rebuild children when sprites are changed in the Inspector
        if (!Application.isPlaying)
        {
            BuildChildren();
        }

        // Sync sprite assignments live
        if (frameRenderer != null && frameFrames != null && frameFrames.Length > 0)
            frameRenderer.sprite = frameFrames[0];
            
        if (fillRenderer  != null) fillRenderer.sprite  = fillSprite;
        
        if (waveRenderer  != null && waveFrames != null && waveFrames.Length > 0)
            waveRenderer.sprite = waveFrames[0];
    }

    private void OnDrawGizmos()
    {
        // Draw the fill range so you can see it in the Scene view
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.5f);
        Vector3 worldMin = transform.TransformPoint(new Vector3(0f, fillMinLocalY, 0f));
        Vector3 worldMax = transform.TransformPoint(new Vector3(0f, fillMaxLocalY, 0f));

        Gizmos.DrawSphere(worldMin, 0.1f);
        Gizmos.DrawSphere(worldMax, 0.1f);
        Gizmos.DrawLine(worldMin, worldMax);

        // Current fill level
        float fillTopY = Mathf.Lerp(fillMinLocalY, fillMaxLocalY, currentPressureNorm);
        Vector3 worldFill = transform.TransformPoint(new Vector3(0f, fillTopY, 0f));
        Gizmos.color = new Color(0.9f, 0.3f, 0.1f, 0.8f);
        Gizmos.DrawSphere(worldFill, 0.08f);
    }
#endif

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Manually set the pressure display (0..1). Useful for testing or
    /// driving the bar from a source other than PressureController.
    /// </summary>
    public void SetPressure(float normalizedValue)
    {
        currentPressureNorm = Mathf.Clamp01(normalizedValue);
    }

    #endregion
}
