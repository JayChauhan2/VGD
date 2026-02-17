using UnityEngine;
using System.Collections;

public class SpawnProtection : MonoBehaviour
{
    public float duration = 3.0f;
    public float visualScale = 0.8f; // Manually settable scale for the visual
    public bool IsActive { get; private set; } = true;
    
    private GameObject forcefield;
    private Renderer[] forceFieldRenderers;
    private SpriteRenderer ownerSprite;

    void Start()
    {
        ownerSprite = GetComponentInChildren<SpriteRenderer>();
        CreateForcefieldVisual();
        StartCoroutine(ProtectionRoutine());
    }

    private void CreateForcefieldVisual()
    {
        // Option 1: Use Global Prefab if assigned in RoomManager
        if (RoomManager.Instance != null && RoomManager.Instance.enemyForcefieldPrefab != null)
        {
            // Change 1: Instantiate as child directly
            forcefield = Instantiate(RoomManager.Instance.enemyForcefieldPrefab, transform);
            // Change 2: Ensure local position is zero
            forcefield.transform.localPosition = Vector3.zero;
            // Force scale to visualScale (0.8f default)
            forcefield.transform.localScale = Vector3.one * visualScale;
            
            // Adjust scale if needed? Or assume user prefab is correct size.
            // Let's ensure it's on the correct layer at least.
            forceFieldRenderers = forcefield.GetComponentsInChildren<Renderer>();
            foreach(var r in forceFieldRenderers)
            {
                r.sortingLayerName = "Object";
                r.sortingOrder = 10;
            }
            // Disable looping animation to prevent annoyance
            Animator anim = forcefield.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    float clipLength = clips[0].length;
                     
                     // Sync protection duration to animation length
                     duration = clipLength;
                }
            }
            return;
        }

        // Option 2: Fallback to procedural LineRenderer
        forcefield = new GameObject("Forcefield_Generated");
        forcefield.transform.SetParent(transform);
        forcefield.transform.localPosition = Vector3.zero;

        LineRenderer lr = forcefield.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 50;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0, 1, 1, 0.5f); // Cyan transparent
        lr.endColor = new Color(0, 0, 1, 0.5f);   // Blue transparent
        lr.sortingLayerName = "Object";
        lr.sortingOrder = 10;
        
        forceFieldRenderers = new Renderer[] { lr };

        float radius = 0.8f; // Adjust based on enemy size?
        // Maybe dynamic based on collider size?
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) + 0.2f;
        }

        for (int i = 0; i < 50; i++)
        {
            float angle = i * Mathf.PI * 2f / 49;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
        }
    }

    IEnumerator ProtectionRoutine()
    {
        IsActive = true;
        yield return new WaitForSeconds(duration);
        IsActive = false;
        
        if (forcefield != null) Destroy(forcefield);
        Destroy(this); // Remove component when done
    }
    


    void LateUpdate()
    {
        // Maintain sorting order on top of enemy
        if (forcefield != null && ownerSprite != null && forceFieldRenderers != null)
        {
            int targetOrder = ownerSprite.sortingOrder + 10;
            foreach (var r in forceFieldRenderers)
            {
                if (r != null) r.sortingOrder = targetOrder;
            }
        }
    }

    void OnDestroy()
    {
        if (forcefield != null) Destroy(forcefield);
    }
}
