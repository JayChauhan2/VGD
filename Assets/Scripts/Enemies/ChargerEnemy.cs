using UnityEngine;

public class ChargerEnemy : EnemyAI
{
    [Header("Charger Settings")]
    public float detectionRange = 6f;
    public float chargeSpeed = 15f;
    public float chargeWindupTime = 0.3f;
    public float chargeCooldown = 1.5f;
    public float stunDuration = 0.5f;
    
    private enum ChargeState { Idle, WindingUp, Charging, Stunned }
    private ChargeState currentState = ChargeState.Idle;
    
    private Vector2 chargeDirection;
    private float stateTimer;
    private Vector3 normalSpeed;

    protected override void OnEnemyStart()
    {
        maxHealth = 70f;
        currentHealth = maxHealth;
        speed = 3f; // Slow when not charging
        normalSpeed = Vector3.one * speed;
        
        Debug.Log("ChargerEnemy: Initialized");
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        
        switch (currentState)
        {
            case ChargeState.Idle:
                // Check if player is in range to start charge
                if (distanceToPlayer <= detectionRange)
                {
                    StartWindup();
                }
                break;
                
            case ChargeState.WindingUp:
                // Wait for windup to complete
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    StartCharge();
                }
                break;
                
            case ChargeState.Charging:
                // Move in charge direction at high speed
                transform.position += (Vector3)chargeDirection * chargeSpeed * Time.deltaTime;
                
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    // Charge ended, enter stun
                    EnterStun();
                }
                break;
                
            case ChargeState.Stunned:
                // Wait for stun to end
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    ExitStun();
                }
                break;
        }
    }

    void StartWindup()
    {
        currentState = ChargeState.WindingUp;
        stateTimer = chargeWindupTime;
        
        // Calculate charge direction toward player's CURRENT position
        chargeDirection = (target.position - transform.position).normalized;
        
        // Stop pathfinding movement during windup
        path = null;
        
        Debug.Log("ChargerEnemy: Winding up charge!");
    }

    void StartCharge()
    {
        currentState = ChargeState.Charging;
        stateTimer = chargeCooldown;
        
        Debug.Log("ChargerEnemy: CHARGING!");
    }

    void EnterStun()
    {
        currentState = ChargeState.Stunned;
        stateTimer = stunDuration;
        
        Debug.Log("ChargerEnemy: Stunned after charge");
    }

    void ExitStun()
    {
        currentState = ChargeState.Idle;
        Debug.Log("ChargerEnemy: Recovered from stun");
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // If charging and hit a wall, immediately stun
        if (currentState == ChargeState.Charging)
        {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
            {
                EnterStun();
            }
        }
    }
}
