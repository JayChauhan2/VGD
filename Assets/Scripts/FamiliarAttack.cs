using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class FamiliarAttack : MonoBehaviour
{
    [Header("Settings")]
    public float damagePerSecond = 7.5f; // Approx 1/4 of player's 30
    public float attackRange = 2f;
    public LayerMask enemyLayer; // Set this if known, otherwise we'll search all
    
    private LineRenderer lr;
    private Transform firePoint; // Defaults to transform

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        firePoint = transform;

        // Visual setup - Default values just in case
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.enabled = false;
        
        // Try to copy from Player's Laser if possible for consistency
        Laser playerLaser = FindFirstObjectByType<Laser>();
        if (playerLaser != null && playerLaser.m_lineRenderer != null)
        {
            lr.material = playerLaser.m_lineRenderer.sharedMaterial;
            lr.startColor = playerLaser.m_lineRenderer.startColor;
            lr.endColor = playerLaser.m_lineRenderer.endColor;
            // Optionally make it thinner
            lr.startWidth = playerLaser.m_lineRenderer.startWidth * 0.5f;
            lr.endWidth = playerLaser.m_lineRenderer.endWidth * 0.5f;
            // Ensure sorting layer is correct
            lr.sortingLayerName = "Object";
            lr.sortingOrder = 10; // Optional: Ensure it renders on top of floor/walls
        }
        else
        {
            // Fallback if no player laser found
             lr.sortingLayerName = "Object";
             lr.sortingOrder = 10;
        }
    }

    void Update()
    {
        Transform target = GetClosestEnemy();

        if (target != null)
        {
            ShootAt(target);
        }
        else
        {
            lr.enabled = false;
        }
    }

    Transform GetClosestEnemy()
    {
        // Use OverlapCircle to efficiently verify range
        // Note: Ideally use a specific layer mask for performance
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // Check for EnemyAI component
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = hit.transform;
                }
            }
        }

        return closest;
    }

    void ShootAt(Transform target)
    {
        lr.enabled = true;
        lr.SetPosition(0, firePoint.position);

        Vector2 direction = (target.position - firePoint.position).normalized;
        
        // Perform Raycast to stop at walls if needed, similar to Player Laser
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, direction, attackRange);
        
        Vector2 endPos = target.position;

        if (hit.collider != null)
        {
            endPos = hit.point;
            
            // Deal Damage if it's an enemy
            EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
        
        lr.SetPosition(1, endPos);
    }
}
