using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f; // Adjust this value for desired speed
    
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;

    public Rigidbody2D rb;
    private Vector2 moveInput;

    public float smoothTime = 0.5f;
    private float angleVelocity;

    private bool isDashing = false;
    private float dashCooldownTimer = 0f;

    // Properties for UI
    public float DashCooldownProgress => 1f - Mathf.Clamp01(dashCooldownTimer / dashCooldown); // 0 = empty, 1 = full
    public bool IsDashReady => dashCooldownTimer <= 0f;
    public bool IsDashing => isDashing;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Handle Cooldown
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Standard Movement Input
        // (Only read if not currently dashing, though we disable script during dash usually, 
        // strictly checking isDashing helps if we change that approach)
        if (!isDashing) 
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 direction = mousePos - transform.position;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.z,
                angle,
                ref angleVelocity,
                smoothTime
            );

            transform.rotation = Quaternion.Euler(0, 0, smoothAngle);

            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");

            // Dash Input
            if (Input.GetKeyDown(KeyCode.Space) && IsDashReady && moveInput != Vector2.zero)
            {
                StartCoroutine(DashRoutine());
            }
        }
    }

    private System.Collections.IEnumerator DashRoutine()
    {
        isDashing = true;
        dashCooldownTimer = dashCooldown; // Reset cooldown

        // Capture current movement direction for the dash
        Vector2 dashDir = moveInput.normalized;
        
        // If for some reason input is zero (should satisfy moveInput != Vector2.zero check above), fallback to forward? 
        // But the requirement says "dash in the direction theyre moving". 
        // If moving, moveInput is non-zero.

        rb.linearVelocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero; // Optional: stop immediately after dash? Or keep momentum? 
        // Usually dash feels better if it stops or returns to normal control.
        // Let's reset to normal velocity logic handle in next frame.
        
        isDashing = false;
    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
        // Cancel dash if knocked back
        if (isDashing)
        {
            StopCoroutine("DashRoutine"); // String name safer if we haven't stored Coroutine ref
            isDashing = false;
        }
        StartCoroutine(KnockbackRoutine(force, duration));
    }

    System.Collections.IEnumerator KnockbackRoutine(Vector2 force, float duration)
    {
        bool wasKinematic = rb.isKinematic;
        rb.isKinematic = false; // Ensure physics can apply
        rb.linearVelocity = Vector2.zero; // Reset current velocity
        rb.AddForce(force, ForceMode2D.Impulse);
        
        // Disable input control
        enabled = false; 

        yield return new WaitForSeconds(duration);

        // Re-enable input control
        enabled = true;
        rb.linearVelocity = Vector2.zero; // Stop sliding after knockback
        rb.isKinematic = wasKinematic;
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            // During dash, we might want to ensure velocity stays constant or just let RB handle it.
            // If we don't set it here, friction might slow it down.
            // Let's rely on RB inertia or move it manually if needed. 
            // For simple linear dash, setting velocity in Coroutine once is often enough if drag is low.
            // But to be safe against Update overwriting:
             // Actually FixedUpdate runs constantly. If we don't return here, line 85 will overwrite dash velocity.
             return;
        }

        // Apply velocity to the Rigidbody2D based on input and speed
        // Input control is handled by enabling/disabling this script or checking a flag
        // Since we disable 'this.enabled' in KnockbackRoutine, FixedUpdate won't run when knocked back.
        // However, we need to ensure rb.velocity isn't overwritten if we were using a flag. 
        // Using 'enabled = false' is the simplest way to stop Update/FixedUpdate.
        
        rb.linearVelocity = moveInput.normalized * moveSpeed; // Normalized to prevent faster diagonal movement
    }
}