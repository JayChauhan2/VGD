using UnityEngine;
using System.Collections;

/// <summary>
/// Plays a "drop from the sky" animation, then spawns a wild Familiar at the landing spot.
/// Instantiate this GO at the intended landing position; the animation will lift it up and
/// bring it back down, spinning, before landing cleanly.
/// </summary>
public class FamiliarDropper : MonoBehaviour
{
    [Header("Drop Settings")]
    public GameObject familiarPrefab;       // Familiar prefab to spawn on landing
    public LayerMask obstacleLayer;         // Same mask as RoomManager.obstacleLayer
    public float clearanceRadius = 0.3f;    // Overlap check radius

    [Header("Fall Animation")]
    public float fallDuration   = 0.55f;    // Time the object takes to fall from height to ground
    public float startHeight    = 4f;       // How far above the landing spot it begins
    public float spinSpeed      = 900f;     // Degrees per second

    [Header("Bounce Settle")]
    public float bounceAmount   = 0.25f;    // Extra height of first bounce (world units)
    public float bounceDuration = 0.18f;
    public int   bounceCount    = 2;

    private SpriteRenderer sr;
    private Vector3 landingPos;

    void Start()
    {
        // Give us a simple visual: a star-shaped / bright orb placeholder
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Object";
        sr.sortingOrder = 1000;  // Draw above everything during the drop

        landingPos = transform.position;
        StartCoroutine(DropSequence());
    }

    IEnumerator DropSequence()
    {
        // 1. Start high above the landing spot
        Vector3 startPos = landingPos + Vector3.up * startHeight;
        transform.position = startPos;
        Vector3 baseScale  = transform.localScale;

        // Optional: small scale-up as it "comes into view"
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed    += Time.deltaTime;
            float t     = elapsed / fallDuration;

            // Position: linear fall
            transform.position = Vector3.Lerp(startPos, landingPos, t);

            // Spin
            transform.rotation = Quaternion.Euler(0, 0, t * spinSpeed);

            // Scale: start slightly small, arrive at full size
            float scaleFactor  = Mathf.Lerp(0.5f, 1f, t);
            transform.localScale = baseScale * scaleFactor;

            yield return null;
        }

        // Snap to exact landing
        transform.position   = landingPos;
        transform.rotation   = Quaternion.identity;
        transform.localScale = baseScale;

        // 2. Bounce settle
        float currentBounce   = bounceAmount;
        float currentDuration = bounceDuration;

        for (int i = 0; i < bounceCount; i++)
        {
            elapsed = 0f;
            while (elapsed < currentDuration)
            {
                elapsed += Time.deltaTime;
                float t  = elapsed / currentDuration;
                // Parabola arc 0 → peak → 0
                float arc = 4f * (t - t * t);
                transform.position = landingPos + Vector3.up * (arc * currentBounce);
                yield return null;
            }

            // Each successive bounce is smaller
            currentBounce   /= 2f;
            currentDuration /= 1.5f;
        }

        // Settle exactly at landing position (reset from bounce)
        transform.position   = landingPos;
        transform.localScale = baseScale;

        // Restore sorting order so it renders normally in the world
        if (sr != null) sr.sortingOrder = 5;

        // 3. Spawn the actual Familiar
        if (familiarPrefab != null)
        {
            GameObject familiarGO = Instantiate(familiarPrefab, landingPos, Quaternion.identity);
            // Ensure it starts wild (player must walk up to it)
            Familiar familiar = familiarGO.GetComponent<Familiar>();
            if (familiar != null) familiar.isWild = true;
        }

        // 4. Remove the dropper shell
        Destroy(gameObject);
    }

    /// <summary>
    /// Find a clear spot inside the room for the familiar to land, avoiding obstacles.
    /// Returns the room centre as fallback.
    /// </summary>
    public static Vector3 FindClearSpot(Room room, LayerMask obstacleLayer, float clearanceRadius = 0.3f, int attempts = 30)
    {
        Vector3 center  = room.transform.position;
        float   halfW   = room.roomSize.x / 2f - 2f;
        float   halfH   = room.roomSize.y / 2f - 2f;

        for (int i = 0; i < attempts; i++)
        {
            float   rx  = Random.Range(-halfW, halfW);
            float   ry  = Random.Range(-halfH, halfH);
            Vector3 pos = center + new Vector3(rx, ry, 0);

            if (Physics2D.OverlapCircle(pos, clearanceRadius, obstacleLayer) == null)
                return pos;
        }
        return center; // Fallback
    }
}
