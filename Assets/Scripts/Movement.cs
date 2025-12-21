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

    void FixedUpdate()
    {
        // Apply velocity to the Rigidbody2D based on input and speed
        rb.linearVelocity = moveInput * moveSpeed;
    }
}