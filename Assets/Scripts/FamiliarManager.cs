using UnityEngine;
using System.Collections.Generic;

public class FamiliarManager : MonoBehaviour
{
    public static FamiliarManager Instance;

    public Transform player;
    public float radius = 2f;
    public float rotationSpeed = 30f; // degrees per second

    private List<Familiar> familiars = new List<Familiar>();
    private float currentRotation = 0f;

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

        // Update global rotation for the ring
        currentRotation += rotationSpeed * Time.deltaTime;
        currentRotation %= 360f;

        float angleStep = 360f / familiars.Count;

        for (int i = 0; i < familiars.Count; i++)
        {
            float angle = currentRotation + (i * angleStep);
            float rad = angle * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
            Vector3 targetPos = player.position + offset;

            familiars[i].SetTargetPosition(targetPos);
        }
    }
}
