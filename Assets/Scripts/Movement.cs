using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f; // Adjust this value for desired speed
    
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;
    public LayerMask wallLayer; // Assign in Inspector to detect walls/obstacles

    public Rigidbody2D rb;
    private Vector2 moveInput;

    public float smoothTime = 0.5f;
    private float angleVelocity;

    private bool isDashing = false;
    private float dashCooldownTimer = 0f;
    private System.Collections.IEnumerator currentDashCoroutine;

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
                currentDashCoroutine = DashRoutine();
                StartCoroutine(currentDashCoroutine);
            }
        }
    }

    private System.Collections.IEnumerator DashRoutine()
    {
        isDashing = true;
        dashCooldownTimer = dashCooldown; // Reset cooldown

        // Capture current movement direction for the dash
        Vector2 dashDir = moveInput.normalized;
        
        // Calculate maximum dash distance
        float maxDashDistance = dashSpeed * dashDuration;
        
        // Raycast to check for walls in dash direction
        float actualDashDistance = maxDashDistance;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dashDir, maxDashDistance, wallLayer);
        
        if (hit.collider != null)
        {
            // Wall detected - clamp dash distance to stop just before the wall
            actualDashDistance = Mathf.Max(0.1f, hit.distance - 0.3f); // Leave small buffer (0.3 units before wall)
        }
        
        // Calculate adjusted dash speed to cover the actual distance in the same duration
        float adjustedDashSpeed = actualDashDistance / dashDuration;
        
        rb.linearVelocity = dashDir * adjustedDashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero;
        
        isDashing = false;
        currentDashCoroutine = null;
    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
        // Cancel dash if knocked back
        if (isDashing && currentDashCoroutine != null)
        {
            StopCoroutine(currentDashCoroutine);
            isDashing = false;
            currentDashCoroutine = null;
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

    void OnCollisionEnter2D(Collision2D collision)
    {
        // If we hit a wall while dashing, stop the dash immediately to prevent physics issues
        if (isDashing && ((1 << collision.gameObject.layer) & wallLayer) != 0)
        {
            if (currentDashCoroutine != null)
            {
                StopCoroutine(currentDashCoroutine);
                currentDashCoroutine = null;
            }
            isDashing = false;
            rb.linearVelocity = Vector2.zero;
        }
    }
}