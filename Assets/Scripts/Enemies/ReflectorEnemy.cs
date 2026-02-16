using UnityEngine;

public class ReflectorEnemy : ShielderEnemy
{
    // Inherits all logic from ShielderEnemy
    // - OnEnemyStart (Initialize stats, shield visual)
    // - OnEnemyUpdate (RotateShieldToPlayer)
    // - TakeDamage (Shield HP logic)
    
    // We only need to override specific behaviors if they differ.
    // Currently, ReflectorEnemy is identical to ShielderEnemy EXCEPT:
    // 1. It reflects lasers (IsReflecting = true)
    
    protected override void OnEnemyStart()
    {
        base.OnEnemyStart();
        // Custom Reflector initialization if needed
        Debug.Log("ReflectorEnemy: Ready to Reflect!");
    }

    public override bool IsReflecting(Vector2 incomingDir)
    {
        // 1. Must be Shielded and have HP
        // We rely on Physics now: If Physics Hit the Shield Collider, then we reflect.
        // We just verify the STATE is correct here.
        return currentState == ShieldState.Shielded && currentShieldHealth > 0;
    }
}
