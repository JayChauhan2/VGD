using UnityEngine;

public class PressureController : MonoBehaviour
{
    [Header("Pressure Values")]
    public float currentPressure = 0f;
    public float maxPressure = 100f;

    [Header("Pressure Rates")]
    public float passiveIncreaseRate = 4f; // per second
    public float killReduction = 8f;
    public float damageIncrease = 12f;
    public float idleIncreaseMultiplier = 1.5f;

    [Header("State Thresholds")]
    public float lowThreshold = 30f;
    public float midThreshold = 60f;
    public float highThreshold = 85f;

    public bool roomStabilized = false;
    public bool isStressActive = false;

    private Room room;
    private PlayerMovement player;
    private float stableTimer = 0f;



    public enum PressureState { Low, Mid, High }
    public PressureState CurrentState { get; private set; } = PressureState.Low;

    // Events for other systems to listen to
    public event System.Action<float> OnPressureChanged;
    public event System.Action<PressureState> OnStateChanged;

    public void Initialize(Room room)
    {
        this.room = room;
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.GetComponent<PlayerMovement>();
        }
    }

    [Header("Debug")]
    public bool debugMode = true;
    public bool forceActivate = false;

    public void Activate()
    {
        if (debugMode) Debug.Log($"[PressureController] Activated on {gameObject.name}");
        isStressActive = true;
        currentPressure = 0f; 
        stableTimer = 0f;
        timeActive = 0f;
    }

    public void Deactivate()
    {
        if (debugMode) Debug.Log($"[PressureController] Deactivated on {gameObject.name}");
        isStressActive = false;
    }

    void Update()
    {
        if (forceActivate && !isStressActive) Activate();

        if (!isStressActive || roomStabilized) return;

        float rate = passiveIncreaseRate;

        if (PlayerIsIdle())
        {
            rate *= idleIncreaseMultiplier;
        }

        currentPressure += rate * Time.deltaTime;
        currentPressure = Mathf.Clamp(currentPressure, 0, maxPressure);

        EvaluatePressureState();
        CheckStabilization();

        OnPressureChanged?.Invoke(currentPressure / maxPressure);
        if (debugMode && Time.frameCount % 60 == 0) 
        {
             Debug.Log($"[PressureController] Pressure: {currentPressure}/{maxPressure} (State: {CurrentState})");
        }
    }

    bool PlayerIsIdle()
    {
        if (player == null) return true; // If no player, treat as idle (punish?) or pause? User said "punish passive play".
        
        // Using rb velocity magnitude from PlayerMovement
        // User suggested: player.Velocity.magnitude < 0.1f && !player.HasAttackedRecently;
        // Accessing rb directly might be needed if not exposed. PlayerMovement public rb is available.
        // Attack check: I need to check Laser.cs or similar for "HasAttackedRecently". 
        // For now, I will stick to movement.
        
        return player.rb.linearVelocity.magnitude < 0.1f;
    }

    public void OnEnemyKilled()
    {
        if (roomStabilized) return;
        currentPressure -= killReduction;
        currentPressure = Mathf.Clamp(currentPressure, 0, maxPressure);
        // Note: EvaluatePressureState will happen next Update, implies frame delay which is fine.
    }

    public void OnPlayerDamaged()
    {
        if (roomStabilized) return;
        currentPressure += damageIncrease;
        currentPressure = Mathf.Clamp(currentPressure, 0, maxPressure);
    }

    public void OnMissed(float amount)
    {
        if (roomStabilized) return;
        currentPressure += amount;
        currentPressure = Mathf.Clamp(currentPressure, 0, maxPressure);
    }

    void EvaluatePressureState()
    {
        PressureState oldState = CurrentState;

        if (currentPressure >= highThreshold)
            CurrentState = PressureState.High;
        else if (currentPressure >= midThreshold)
            CurrentState = PressureState.Mid;
        else
            CurrentState = PressureState.Low;

        if (oldState != CurrentState)
        {
            OnStateChanged?.Invoke(CurrentState);
        }
    }

    [Header("Stabilization")]
    public float stabilizationTime = 5f; // Time required to HOLD low pressure
    public float minimumSurvivalTime = 15f; // Absolute minimum time the room must be active
    
    private float timeActive = 0f;



    void CheckStabilization()
    {
        timeActive += Time.deltaTime;
    
        // Option A: Stabilization Window
        if (currentPressure < midThreshold)
        {
            stableTimer += Time.deltaTime;
        }
        else
        {
            stableTimer = 0f;
        }

        // We only stabilize if:
        // 1. We have held low pressure for 'stabilizationTime' (Performance)
        // 2. We have survived for at least 'minimumSurvivalTime' (Duration)
        if (stableTimer >= stabilizationTime && timeActive >= minimumSurvivalTime)
        {
            Stabilize();
        }
    }

    void Stabilize()
    {
        roomStabilized = true;
        isStressActive = false;
        Debug.Log("PressureSystem: Room Stabilized!");
        if (room != null)
        {
            room.StabilizeRoom();
        }
    }
}
