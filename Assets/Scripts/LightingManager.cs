using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance;

    [Header("Settings")]
    public Material litMaterial;
    public Color globalLightColor = new Color(0.1f, 0.1f, 0.2f, 1f); // Dark blue-ish night
    public float globalLightIntensity = 0.3f;
    
    public Color playerLightColor = new Color(1f, 0.9f, 0.7f, 1f); // Warm torch
    public float playerLightIntensity = 1.2f;
    public float playerLightRadius = 10f;

    [Header("Shadows")]
    public bool castShadows = true;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartCoroutine(SetupLightingRoutine());
    }

    IEnumerator SetupLightingRoutine()
    {
        // Wait a frame for initialization
        yield return null;

        ApplyGlobalLighting();
        ApplyPlayerLighting();
        ApplyMaterials();
    }

    [ContextMenu("Apply Lighting Settings")]
    public void ConfigureLighting()
    {
        ApplyGlobalLighting();
        ApplyPlayerLighting();
        ApplyMaterials();
    }

    void ApplyGlobalLighting()
    {
        GameObject globalObj = GameObject.Find("GlobalLight");
        Light2D globalLight;

        if (globalObj == null)
        {
            globalObj = new GameObject("GlobalLight");
            globalLight = globalObj.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
        }
        else
        {
            globalLight = globalObj.GetComponent<Light2D>();
            if (globalLight == null) globalLight = globalObj.AddComponent<Light2D>();
        }

        globalLight.color = globalLightColor;
        globalLight.intensity = globalLightIntensity;
    }

    void ApplyPlayerLighting()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Try identifying by name if tag fails
            player = GameObject.Find("Player");
        }

        if (player != null)
        {
            Transform lightTrans = player.transform.Find("PlayerLight");
            Light2D playerLight = null;

            if (lightTrans == null)
            {
                GameObject plObj = new GameObject("PlayerLight");
                plObj.transform.parent = player.transform;
                plObj.transform.localPosition = Vector3.zero;
                playerLight = plObj.AddComponent<Light2D>();
            }
            else
            {
                playerLight = lightTrans.GetComponent<Light2D>();
                if (playerLight == null) playerLight = lightTrans.gameObject.AddComponent<Light2D>();
            }

            playerLight.lightType = Light2D.LightType.Point;
            playerLight.color = playerLightColor;
            playerLight.intensity = playerLightIntensity;
            playerLight.pointLightOuterRadius = playerLightRadius;
            playerLight.falloffIntensity = 0.5f;
            playerLight.shadowsEnabled = castShadows;
        }
    }

    void ApplyMaterials()
    {
        if (litMaterial == null) return;

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in renderers)
        {
            // Simple heuristic to avoid breaking UI or particles
            if (sr.gameObject.layer == LayerMask.NameToLayer("UI")) continue;
            
            // Check if material is default or unlit
            if (sr.sharedMaterial == null || sr.sharedMaterial.name.Contains("Default") || sr.sharedMaterial.shader.name.Contains("Unlit"))
            {
                sr.sharedMaterial = litMaterial;
            }

            // Optional: Add shadows to obstacles
            // If the object has a collider and is static-ish, maybe?
            // Safer to do this manually or via specific tags.
            if (sr.gameObject.name.Contains("Wall") || sr.gameObject.name.Contains("Obstacle"))
            {
                if (sr.gameObject.GetComponent<ShadowCaster2D>() == null)
                {
                    ShadowCaster2D sc = sr.gameObject.AddComponent<ShadowCaster2D>();
                    sc.selfShadows = true;
                }
            }
        }
    }
}
