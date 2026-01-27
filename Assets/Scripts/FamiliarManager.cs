using UnityEngine;
using System.Collections.Generic;

public class FamiliarManager : MonoBehaviour
{
    public static FamiliarManager Instance;

    public Transform player;
    public float radius = 2f;
    public float rotationSpeed = 30f; // degrees per second
    public float deformationStrength = 0.5f; // How much it stretches based on speed
    public float velocitySmoothing = 10f;

    private List<Familiar> familiars = new List<Familiar>();
    private float currentRotation = 0f;
    private Vector3 lastPlayerPos;
    private Vector3 smoothedVelocity;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else
            {
                var pm = Object.FindFirstObjectByType<PlayerMovement>();
                if (pm != null) player = pm.transform;
            }
        }
        
        if (player != null) lastPlayerPos = player.position;
    }

    public void RegisterFamiliar(Familiar f)
    {
        if (!familiars.Contains(f))
        {
            familiars.Add(f);
        }
    }

    public void UnregisterFamiliar(Familiar f)
    {
        if (familiars.Contains(f))
        {
            familiars.Remove(f);
        }
    }

    void Update()
    {
        if (player == null || familiars.Count == 0) return;

        // Calculate velocity manually to avoid Rigidbody dependency
        Vector3 rawVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, Time.deltaTime * velocitySmoothing);
        lastPlayerPos = player.position;

        // Update global rotation for the ring
        currentRotation += rotationSpeed * Time.deltaTime;
        currentRotation %= 360f;

        float angleStep = 360f / familiars.Count;

        for (int i = 0; i < familiars.Count; i++)
        {
            float angle = currentRotation + (i * angleStep);
            float rad = angle * Mathf.Deg2Rad;

            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);
            
            // Jelly Effect: Expand in the direction of movement
            // Dot product gives us alignment (-1 to 1) between familiar direction and movement
            float alignment = Vector3.Dot(dir, smoothedVelocity.normalized);
            // We only want to expand if alignment is positive (in front), scale by speed
            float distortion = Mathf.Max(0, alignment) * smoothedVelocity.magnitude * deformationStrength;

            Vector3 offset = dir * (radius + distortion);
            Vector3 targetPos = player.position + offset;

            familiars[i].SetTargetPosition(targetPos);
        }
    }
}
