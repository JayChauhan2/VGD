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

    private bool isOverheated = false;
    [SerializeField] private float overheatThreshold = 20f; // Must recharge to this to shoot again

    private bool isCharging = false;
    private float chargeTimer = 0f;
    [SerializeField] private float chargeDuration = 0f; // Short delay for "power up"

    private void Awake()
    {
        m_transform = GetComponent<Transform>();
        currentEnergy = maxEnergy;
    }

    private void Update()
    {
        // Require Mouse Input AND Energy AND Not Overheated
        bool tryingToShoot = Input.GetMouseButton(0);
        bool justClicked = Input.GetMouseButtonDown(0);

        if (justClicked && currentEnergy > 0 && !isOverheated)
        {
            isCharging = true;
            chargeTimer = chargeDuration;
        }

        // Handle Camera Effect - PUSH away from aim direction
        if (tryingToShoot && currentEnergy > 0 && !isOverheated)
        {
            if (CameraController.Instance != null)
            {
                // Push camera BACK (negative right)
                // Magnitude reduced to 0.125f (1/4 of previous 0.5f) as per user request
                CameraController.Instance.SetRecoilOffset(-transform.right * 0.125f);
            }
        }
        else
        {
            // Reset to center
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetRecoilOffset(Vector3.zero);
            }
        }

        if (tryingToShoot && !isOverheated && currentEnergy > 0)
        {
            if (isCharging)
            {
                // Wait for charge to finish
                chargeTimer -= Time.deltaTime;
                if (chargeTimer <= 0)
                {
                    isCharging = false;
                    ShootLaser();
                    DrainEnergy();
                }
            }
            else
            {
                // Already charged, just shoot
                ShootLaser();
                DrainEnergy();
            }
        }
        else
        {
            // Stopped shooting or ran out/overheated
            isCharging = false;
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
        if (currentEnergy <= 0) 
        {
            currentEnergy = 0;
            isOverheated = true; // Trigger cooldown
        }
    }

    void RechargeEnergy()
    {
        if (currentEnergy < maxEnergy)
        {
            currentEnergy += rechargeRate * Time.deltaTime;
            
            // Check if we recovered from overheat
            if (isOverheated && currentEnergy >= overheatThreshold)
            {
                isOverheated = false;
            }

            if (currentEnergy > maxEnergy) currentEnergy = maxEnergy;
        }
    }

    void DisableLaser()
    {
        if (m_lineRenderer != null) m_lineRenderer.enabled = false;
    }
    
    [Header("Pressure Settings")]
    public float missPressureRate = 5f; // Pressure increase per second of missing

    void ShootLaser()
    {
        if (m_lineRenderer != null) m_lineRenderer.enabled = true;

        RaycastHit2D _hit = Physics2D.Raycast(m_transform.position, transform.right);
        
        Vector2 endPos;
        bool isHitEnemy = false;

        if (_hit.collider != null) // Hit something
        {
            endPos = _hit.point;
            
            // Apply Damage
            EnemyAI enemy = _hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
                isHitEnemy = true;
            }
            // Check for Projectile
            EnemyProjectile proj = _hit.collider.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                Destroy(proj.gameObject);
                // We hit a bullet, so it's not a "Miss" (no penalty), but also didn't hit enemy.
                // Just return or let it draw the ray to the bullet impact point.
                // We'll set isHitEnemy = true purely to skip the "Miss" penalty below.
                isHitEnemy = true; 
            }
        }
        else
        {
            endPos = (Vector2)laserFirePoint.position + (Vector2)transform.right * defDistanceRay;
        }
        
        // Miss Logic
        if (!isHitEnemy)
        {
            Room room = Room.GetRoomContaining(transform.position);
            if (room != null && room.Pressure != null)
            {
                room.Pressure.OnMissed(missPressureRate * Time.deltaTime);
            }
        }

        Draw2DRay(laserFirePoint.position, endPos);
    }

    void Draw2DRay(Vector2 startPos, Vector2 endPos)
    {
        m_lineRenderer.SetPosition(0, startPos);
        m_lineRenderer.SetPosition(1, endPos);
    }
}