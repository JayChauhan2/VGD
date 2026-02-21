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

    [Header("Multi-Ray Experiment")]
    public int beamCount = 10;
    public float beamWidth = 0.25f;
    private LineRenderer[] beamRenderers;

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

        if (m_lineRenderer != null)
        {
            beamRenderers = new LineRenderer[beamCount];
            beamRenderers[0] = m_lineRenderer;

            // Make the segments slightly thicker than the mathematical slice so they overlap and form a solid block
            float segmentWidth = beamCount > 0 ? (beamWidth / beamCount) * 1.5f : m_lineRenderer.startWidth;
            m_lineRenderer.startWidth = segmentWidth;
            m_lineRenderer.endWidth = segmentWidth;

            for (int i = 1; i < beamCount; i++)
            {
                GameObject beamObj = new GameObject("LaserBeam_" + i);
                beamObj.transform.SetParent(m_lineRenderer.transform.parent, false);
                LineRenderer lr = beamObj.AddComponent<LineRenderer>();

                lr.sharedMaterial = m_lineRenderer.sharedMaterial;
                lr.colorGradient = m_lineRenderer.colorGradient;
                lr.startWidth = segmentWidth;
                lr.endWidth = segmentWidth;
                lr.sortingLayerID = m_lineRenderer.sortingLayerID;
                lr.sortingLayerName = m_lineRenderer.sortingLayerName;
                lr.sortingOrder = m_lineRenderer.sortingOrder;
                lr.textureMode = m_lineRenderer.textureMode;
                lr.numCapVertices = m_lineRenderer.numCapVertices;
                lr.numCornerVertices = m_lineRenderer.numCornerVertices;
                lr.alignment = m_lineRenderer.alignment;

                beamRenderers[i] = lr;
            }
        }
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
        if (beamRenderers != null)
        {
            foreach (var lr in beamRenderers)
            {
                if (lr != null) lr.enabled = false;
            }
        }
        else if (m_lineRenderer != null) m_lineRenderer.enabled = false;

        if (spriteRenderer != null && flashlightOffSprite != null)
        {
            spriteRenderer.sprite = flashlightOffSprite;
        }
    }
    
    [Header("Pressure Settings")]
    public float missPressureRate = 5f; // Pressure increase per second of missing

    void ShootLaser()
    {
        if (beamRenderers != null)
        {
            foreach (var lr in beamRenderers)
            {
                if (lr != null) lr.enabled = true;
            }
        }
        else if (m_lineRenderer != null) m_lineRenderer.enabled = true;

        if (spriteRenderer != null && flashlightOnSprite != null)
        {
            spriteRenderer.sprite = flashlightOnSprite;
        }

        bool isHitEnemy = false;
        Vector2 right = transform.right;
        Vector2 up = transform.up;
        System.Collections.Generic.HashSet<Collider2D> damagedEnemiesThisFrame = new System.Collections.Generic.HashSet<Collider2D>();
        System.Collections.Generic.HashSet<Collider2D> damagedShieldsThisFrame = new System.Collections.Generic.HashSet<Collider2D>();
        System.Collections.Generic.HashSet<BreakableBox> damagedBoxesThisFrame = new System.Collections.Generic.HashSet<BreakableBox>();
        System.Collections.Generic.HashSet<Collider2D> damagedPlayerThisFrame = new System.Collections.Generic.HashSet<Collider2D>();

        for (int i = 0; i < beamCount; i++)
        {
            float t = (beamCount <= 1) ? 0.5f : (float)i / (beamCount - 1);
            float offset = Mathf.Lerp(-beamWidth / 2f, beamWidth / 2f, t);
            
            // Offset start position perpendicular to the laser direction
            Vector2 startPos = (Vector2)laserFirePoint.position + up * offset;
            
            // Raycast starting from the offset position
            RaycastHit2D _hit = Physics2D.Raycast(startPos, right, defDistanceRay);
            Vector2 firstEndPos = _hit.collider != null ? _hit.point : startPos + right * defDistanceRay;
            
            LineRenderer currentLR = (beamRenderers != null && i < beamRenderers.Length) ? beamRenderers[i] : m_lineRenderer;
            
            if (currentLR != null)
            {
                currentLR.positionCount = 2;
                currentLR.SetPosition(0, startPos);
                currentLR.SetPosition(1, firstEndPos);
            }

            if (_hit.collider != null)
            {
                ReflectorEnemy reflector = _hit.collider.GetComponentInParent<ReflectorEnemy>();
                bool hitShieldObject = false;
                if (reflector != null)
                {
                    GameObject shieldObj = reflector.GetShieldGameObject();
                    if (shieldObj != null && _hit.collider.gameObject == shieldObj) hitShieldObject = true;
                }

                if (reflector != null && hitShieldObject && reflector.IsReflecting(right))
                {
                    Vector2 incomingDir = right;
                    Vector2 normal = _hit.normal;
                    Vector2 reflectDir = Vector2.Reflect(incomingDir, normal).normalized;
                    
                    RaycastHit2D _hit2 = Physics2D.Raycast(_hit.point + reflectDir * 0.1f, reflectDir, defDistanceRay);
                    Vector2 secondEndPos = _hit2.collider != null ? _hit2.point : _hit.point + reflectDir * defDistanceRay;
                    
                    if (currentLR != null)
                    {
                        currentLR.positionCount = 3;
                        currentLR.SetPosition(2, secondEndPos);
                    }
                    
                    if (_hit2.collider != null)
                    {
                        EnemyAI enemy2 = _hit2.collider.GetComponent<EnemyAI>();
                        if (enemy2 != null && !damagedEnemiesThisFrame.Contains(_hit2.collider))
                        {
                            enemy2.TakeDamage(damagePerSecond * Time.deltaTime);
                            damagedEnemiesThisFrame.Add(_hit2.collider);
                        }
                        else if (_hit2.collider.CompareTag("Player") && !damagedPlayerThisFrame.Contains(_hit2.collider))
                        {
                             PlayerHealth ph = _hit2.collider.GetComponent<PlayerHealth>();
                             if (ph != null) ph.TakeDamage(10f * Time.deltaTime, reflectDir);
                             damagedPlayerThisFrame.Add(_hit2.collider);
                        }
                    }
                    
                    if (!damagedShieldsThisFrame.Contains(_hit.collider))
                    {
                        reflector.TakeDamage(damagePerSecond * Time.deltaTime);
                        damagedShieldsThisFrame.Add(_hit.collider);
                    }
                    isHitEnemy = true; 
                    continue; // Done with this ray
                }
                 
                EnemyAI enemy = _hit.collider.GetComponent<EnemyAI>();
                if (enemy == null) enemy = _hit.collider.GetComponentInParent<EnemyAI>();

                if (enemy != null)
                {
                    if (!damagedEnemiesThisFrame.Contains(_hit.collider))
                    {
                        enemy.TakeDamage(damagePerSecond * Time.deltaTime);
                        damagedEnemiesThisFrame.Add(_hit.collider);
                    }
                    isHitEnemy = true;
                }
                else
                {
                    BreakableBox box = _hit.collider.GetComponent<BreakableBox>();
                    if (box != null)
                    {
                        if (!damagedBoxesThisFrame.Contains(box))
                        {
                            box.TakeDamage(damagePerSecond * Time.deltaTime, right);
                            damagedBoxesThisFrame.Add(box);
                        }
                        isHitEnemy = true;
                    }
                }
                
                EnemyProjectile proj = _hit.collider.GetComponent<EnemyProjectile>();
                if (proj != null)
                {
                    Destroy(proj.gameObject);
                    isHitEnemy = true; 
                }
            }
        }
        
        if (!isHitEnemy)
        {
            Room room = Room.GetRoomContaining(transform.position);
            if (room != null && room.Pressure != null)
            {
                room.Pressure.OnMissed(missPressureRate * Time.deltaTime);
            }
        }
    }
}
