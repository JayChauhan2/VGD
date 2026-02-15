using UnityEngine;

public class Laser : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float defDistanceRay = 100;
    public float damagePerSecond = 30f;
    public float orbitRadius = 0.5f;
    
    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float drainRate = 40f; // Drains in 2.5 seconds
    [SerializeField] private float rechargeRate = 20f; // Recharges in 5 seconds
    
    private float currentEnergy;

    [Header("References")]
    public Transform laserFirePoint;
    public LineRenderer m_lineRenderer;
    Transform m_transform;

    [Header("Visuals")]
    public Sprite flashlightOnSprite;
    public Sprite flashlightOffSprite;
    private SpriteRenderer spriteRenderer;

    private bool isOverheated = false;
    [SerializeField] private float overheatThreshold = 20f; // Must recharge to this to shoot again

    private bool isCharging = false;
    private float chargeTimer = 0f;
    [SerializeField] private float chargeDuration = 0f; // Short delay for "power up"

    private void Awake()
    {
        m_transform = GetComponent<Transform>();
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        // Orphaning Fix: Continue Update logic here
        
        // Handle Camera Effect - PUSH away from aim direction
        if (tryingToShoot && currentEnergy > 0 && !isOverheated)
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetRecoilOffset(-transform.right * 0.125f);
            }
        }
        else
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetRecoilOffset(Vector3.zero);
            }
        }

        if (tryingToShoot && !isOverheated && currentEnergy > 0)
        {
            if (isCharging)
            {
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
                ShootLaser();
                DrainEnergy();
            }
        }
        else
        {
            isCharging = false;
            DisableLaser();
            RechargeEnergy();
        }
    }

    private void LateUpdate()
    {
        if (Camera.main == null) return;

        // Look At Mouse & Orbit (Moved to LateUpdate to override potential Animation control)
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 centerPos = transform.parent != null ? transform.parent.position : transform.position;
        Vector2 direction = (Vector2)mousePos - (Vector2)centerPos;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Flip gun when facing left
        float scaleY = Mathf.Abs(transform.localScale.y);
        if (Mathf.Abs(angle) > 90)
        {
            transform.localScale = new Vector3(transform.localScale.x, -scaleY, transform.localScale.z);
        }
        else
        {
            transform.localScale = new Vector3(transform.localScale.x, scaleY, transform.localScale.z);
        }
        
        // Apply Orbit Position
        float radians = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(radians) * orbitRadius, Mathf.Sin(radians) * orbitRadius, 0);

        if (transform.parent != null)
        {
            // Use World Space to avoid Parent Rotation/Scale issues
            transform.position = centerPos + offset;
        }
        else
        {
            transform.localPosition = offset;
        }

        // Debug output to verify values
        if (Time.frameCount % 60 == 0)
        {
             Debug.Log($"[LaserDebug] Radius: {orbitRadius}, Parent: {transform.parent?.name}, Pos: {transform.position}, Offset: {offset}");
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
        if (spriteRenderer != null && flashlightOffSprite != null)
        {
            spriteRenderer.sprite = flashlightOffSprite;
        }
    }
    
    [Header("Pressure Settings")]
    public float missPressureRate = 5f; // Pressure increase per second of missing

    void ShootLaser()
    {
        if (m_lineRenderer != null) m_lineRenderer.enabled = true;
        if (spriteRenderer != null && flashlightOnSprite != null)
        {
            spriteRenderer.sprite = flashlightOnSprite;
        }

        // Raycast 1
        RaycastHit2D _hit = Physics2D.Raycast(m_transform.position, transform.right, defDistanceRay);
        
        Vector2 firstEndPos = _hit.collider != null ? _hit.point : (Vector2)laserFirePoint.position + (Vector2)transform.right * defDistanceRay;
        bool isHitEnemy = false;
        
        // Handle First Hit
        if (_hit.collider != null)
        {
            // Check for Reflector
            ReflectorEnemy reflector = _hit.collider.GetComponent<ReflectorEnemy>();
            if (reflector != null && reflector.IsReflecting())
            {
                // REFLECTION LOGIC
                // 1. Calculate Reflection Vector
                Vector2 incomingDir = transform.right;
                Vector2 normal = _hit.normal;
                Vector2 reflectDir = Vector2.Reflect(incomingDir, normal).normalized;
                
                // 2. Raycast 2 (Bounce)
                RaycastHit2D _hit2 = Physics2D.Raycast(_hit.point + reflectDir * 0.1f, reflectDir, defDistanceRay);
                Vector2 secondEndPos = _hit2.collider != null ? _hit2.point : _hit.point + reflectDir * defDistanceRay;
                
                // Draw 3 points: Start -> Hit1 -> Hit2
                m_lineRenderer.positionCount = 3;
                m_lineRenderer.SetPosition(0, laserFirePoint.position);
                m_lineRenderer.SetPosition(1, firstEndPos);
                m_lineRenderer.SetPosition(2, secondEndPos);
                
                // Handle Damage for Second Hit
                if (_hit2.collider != null)
                {
                    EnemyAI enemy2 = _hit2.collider.GetComponent<EnemyAI>();
                    if (enemy2 != null)
                    {
                        // Friendly Fire? Or does reflected laser hurt enemies? 
                        // Typically lasers hurt enemies. Let's allow multi-hit.
                        enemy2.TakeDamage(damagePerSecond * Time.deltaTime);
                    }
                    else if (_hit2.collider.CompareTag("Player"))
                    {
                         // Reflected laser hurts player!
                         PlayerHealth ph = _hit2.collider.GetComponent<PlayerHealth>();
                         if (ph != null) ph.TakeDamage(10f * Time.deltaTime, reflectDir); // Constant small damage or burst? using time delta
                    }
                }
                
                // Apply damage to the Reflector Shield itself (it degrades)
                reflector.TakeDamage(damagePerSecond * Time.deltaTime);
                
                isHitEnemy = true; // We hit a reflector, so no miss penalty
                return; // Exit here, handled reflection drawing
            }
            
            // Standard Enemy Hit
            EnemyAI enemy = _hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
                isHitEnemy = true;
            }
            
            // Projectile Hit
            EnemyProjectile proj = _hit.collider.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                Destroy(proj.gameObject);
                isHitEnemy = true; 
            }
        }
        
        // No Reflection / Standard Draw
        m_lineRenderer.positionCount = 2;
        Draw2DRay(laserFirePoint.position, firstEndPos);
        
        // Miss Logic
        if (!isHitEnemy)
        {
            Room room = Room.GetRoomContaining(transform.position);
            if (room != null && room.Pressure != null)
            {
                room.Pressure.OnMissed(missPressureRate * Time.deltaTime);
            }
        }
    }

    void Draw2DRay(Vector2 startPos, Vector2 endPos)
    {
        m_lineRenderer.SetPosition(0, startPos);
        m_lineRenderer.SetPosition(1, endPos);
    }
}
