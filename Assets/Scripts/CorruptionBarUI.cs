using UnityEngine;

/// <summary>
/// Displays the room's pressure as a vertical liquid bar in world-space.
///
/// SETUP (one-time, in the Inspector):
/// ─────────────────────────────────────────────────────────────────────
/// 1. Create an empty GameObject in your scene (e.g. "CorruptionBar").
/// 2. Attach this script to it.
/// 3. Assign sprites in the Inspector:
///      • Frame Sprite   → corruptionbarframe_0
///      • Fill Sprite    → corruptionbar_0
///      • Wave Frames    → corruptionliquid_0 … corruptionliquid_7  (8 sprites)
/// 4. Position the GameObject wherever you want the bar on screen.
/// 5. Adjust fillMinLocalY / fillMaxLocalY at runtime in Play Mode to
///    align the fill with the inside of the frame sprite.
///
/// HOW IT WORKS:
/// ─────────────────────────────────────────────────────────────────────
/// • Three child SpriteRenderers are created automatically at runtime:
///     Fill  (sorting order 10) – scales vertically with pressure
///     Wave  (sorting order 11) – animated wave sitting on top of fill
///     Frame (sorting order 12) – always on top, decorative border
/// • Subscribes to Room.OnRoomEntered to track the active room's
///   PressureController and update the bar in real time.
/// </summary>
[ExecuteAlways]
public class CorruptionBarUI : MonoBehaviour
{
    // ─── Sprites ────────────────────────────────────────────────────────
    [Header("Sprites")]
    [Tooltip("The outer frame sprite (corruptionbarframe_0).")]
    public Sprite frameSprite;

    [Tooltip("The fill/liquid body sprite (corruptionbar_0).")]
    public Sprite fillSprite;

    [Tooltip("Wave animation frames in order (corruptionliquid_0 … 7).")]
    public Sprite[] waveFrames;

    // ─── Layout ─────────────────────────────────────────────────────────
    [Header("Layout")]
    [Tooltip("Sorting layer name used for all child renderers.")]
    public string sortingLayerName = "UI";

    [Tooltip("Local Y of the fill child when pressure = 0 (bottom of inner area).")]
    public float fillMinLocalY = -2.0f;

    [Tooltip("Local Y of the fill child when pressure = 100 (top of inner area).")]
    public float fillMaxLocalY = 2.0f;

    [Tooltip("Pixels-per-unit scale applied to all children (matches your sprite PPU).")]
    public float pixelsPerUnit = 16f;

    // ─── Animation ──────────────────────────────────────────────────────
    [Header("Wave Animation")]
    [Tooltip("How many wave frames to show per second.")]
    public float waveAnimFPS = 8f;

    // ─── Internal ────────────────────────────────────────────────────────
    private SpriteRenderer fillRenderer;
    private SpriteRenderer waveRenderer;
    private SpriteRenderer frameRenderer;

    private float currentPressureNorm = 0f;   // 0..1
    private PressureController trackedPressure;

    private float waveTimer;
    private int waveFrame;

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
        ApplyFill();
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────
    #region Child Construction

    /// <summary>
    /// Creates (or finds) the three child SpriteRenderers.
    /// Safe to call multiple times — won't duplicate children.
    /// </summary>
    private void BuildChildren()
    {
        fillRenderer  = GetOrCreateChild("Fill",  10, fillSprite);
        waveRenderer  = GetOrCreateChild("Wave",  11, waveFrames != null && waveFrames.Length > 0 ? waveFrames[0] : null);
        frameRenderer = GetOrCreateChild("Frame", 12, frameSprite);
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

        // Y position of the top of the fill (lerped between min and max)
        float fillTopY = Mathf.Lerp(fillMinLocalY, fillMaxLocalY, currentPressureNorm);

        // Height of the fill in world units
        float fillHeight = fillTopY - fillMinLocalY;
        fillHeight = Mathf.Max(fillHeight, 0f);

        // Scale the fill sprite vertically.
        // The sprite is 6px tall at 16 PPU = 0.375 world units native.
        // We scale it to match the desired height.
        float nativeHeight = fillSprite != null
            ? fillSprite.rect.height / pixelsPerUnit
            : 1f;

        float scaleY = fillHeight > 0f ? fillHeight / nativeHeight : 0f;

        // Position fill so its bottom sits at fillMinLocalY
        float fillCenterY = fillMinLocalY + fillHeight * 0.5f;

        fillRenderer.transform.localPosition = new Vector3(0f, fillCenterY, 0f);
        fillRenderer.transform.localScale    = new Vector3(1f, scaleY, 1f);

        // Position wave at the top of the fill
        waveRenderer.transform.localPosition = new Vector3(0f, fillTopY, 0f);

        // Hide wave when bar is empty
        waveRenderer.enabled = fillHeight > 0.01f;
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
            waveRenderer.sprite = waveFrames[waveFrame];
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
        if (frameRenderer != null) frameRenderer.sprite = frameSprite;
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
