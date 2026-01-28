using UnityEngine;

public class Laser : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float defDistanceRay = 100;
    public float damagePerSecond = 30f;
    
    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float drainRate = 40f; // Drains in 2.5 seconds
    [SerializeField] private float rechargeRate = 20f; // Recharges in 5 seconds
    
    private float currentEnergy;

    [Header("References")]
    public Transform laserFirePoint;
    public LineRenderer m_lineRenderer;
    Transform m_transform;

    private void Awake()
    {
        m_transform = GetComponent<Transform>();
        currentEnergy = maxEnergy;
    }

    private void Update()
    {
        // Require Mouse Input AND Energy
        if (Input.GetMouseButton(0) && currentEnergy > 0)
        {
            ShootLaser();
            DrainEnergy();
        }
        else
        {
            DisableLaser();
            RechargeEnergy();
        }
    }

    public float GetEnergyPercent()
    {
        return currentEnergy / maxEnergy;
    }

    void DrainEnergy()
    {
        currentEnergy -= drainRate * Time.deltaTime;
        if (currentEnergy < 0) currentEnergy = 0;
    }

    void RechargeEnergy()
    {
        if (currentEnergy < maxEnergy)
        {
            currentEnergy += rechargeRate * Time.deltaTime;
            if (currentEnergy > maxEnergy) currentEnergy = maxEnergy;
        }
    }

    void DisableLaser()
    {
        if (m_lineRenderer != null) m_lineRenderer.enabled = false;
    }
    
    void ShootLaser()
    {
        if (m_lineRenderer != null) m_lineRenderer.enabled = true;

        RaycastHit2D _hit = Physics2D.Raycast(m_transform.position, transform.right);
        
        Vector2 endPos;

        if (_hit.collider != null) // Hit something
        {
            endPos = _hit.point;
            
            // Apply Damage
            EnemyAI enemy = _hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
        else
        {
            endPos = (Vector2)laserFirePoint.position + (Vector2)transform.right * defDistanceRay;
        }

        Draw2DRay(laserFirePoint.position, endPos);
    }

    void Draw2DRay(Vector2 startPos, Vector2 endPos)
    {
        m_lineRenderer.SetPosition(0, startPos);
        m_lineRenderer.SetPosition(1, endPos);
    }
}