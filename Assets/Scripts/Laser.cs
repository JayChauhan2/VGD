using UnityEngine;

public class Laser : MonoBehaviour
{
    [SerializeField] private float defDistanceRay = 100;
    public Transform laserFirePoint;
    public LineRenderer m_lineRenderer;
    Transform m_transform;

    private void Awake()
    {
        m_transform = GetComponent<Transform>();
    }

    private void Update()
    {
        ShootLaser();
    }
    public float damagePerSecond = 30f;
    
    void ShootLaser()
    {
        RaycastHit2D _hit = Physics2D.Raycast(m_transform.position, transform.right);
        
        if (_hit.collider != null) // Hit something
        {
            Draw2DRay(laserFirePoint.position, _hit.point);
            
            // Apply Damage
            EnemyAI enemy = _hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                // Applying damage over time
                enemy.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
        else
        {
            Draw2DRay(laserFirePoint.position, laserFirePoint.position + transform.right * defDistanceRay);
        }
    }
    void Draw2DRay(Vector2 startPos, Vector2 endPos)
    {
        m_lineRenderer.SetPosition(0, startPos);
        m_lineRenderer.SetPosition(1, endPos);
    }
}