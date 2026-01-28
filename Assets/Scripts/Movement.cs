using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f; // Adjust this value for desired speed
    public Rigidbody2D rb;
    private Vector2 moveInput;

    public float smoothTime = 0.5f;
    private float angleVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
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

        // Get input for horizontal and vertical axes
        moveInput.x = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right Arrow keys
        moveInput.y = Input.GetAxisRaw("Vertical");   // W/S or Up/Down Arrow keys

    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
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
        // Apply velocity to the Rigidbody2D based on input and speed
        // Input control is handled by enabling/disabling this script or checking a flag
        // Since we disable 'this.enabled' in KnockbackRoutine, FixedUpdate won't run when knocked back.
        // However, we need to ensure rb.velocity isn't overwritten if we were using a flag. 
        // Using 'enabled = false' is the simplest way to stop Update/FixedUpdate.
        
        rb.linearVelocity = moveInput * moveSpeed;
    }
}