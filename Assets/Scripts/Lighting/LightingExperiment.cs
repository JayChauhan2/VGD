using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lighting
{
    public class LightingExperiment : MonoBehaviour
    {
        [Header("Settings")]
        public bool runOnStart = true;
        
        [Header("Global Settings")]
        public Color globalLightColor = new Color(0.1f, 0.1f, 0.3f, 1f);
        public float globalLightIntensity = 0.5f;

        [Header("Player Settings")]
        public Color playerLightColor = new Color(1f, 0.9f, 0.7f, 1f);
        public float playerLightIntensity = 1.2f;
        public float playerLightRadius = 10f;

        [Header("Enemy Settings")]
        public Color enemyLightColor = new Color(1f, 0.2f, 0.2f, 1f); // Reddish for danger
        public float enemyLightIntensity = 0.8f;
        public float enemyLightRadius = 5f;

        [Header("Projectile Settings")]
        public Color projectileLightColor = new Color(1f, 0.8f, 0.2f, 1f); // Yellowish
        public float projectileLightIntensity = 0.5f;
        public float projectileLightRadius = 3f;


        [Header("References")]
        public Material litMaterial;
        public Material defaultMaterial;

        // Track added components for clean revert
        private List<Component> addedComponents = new List<Component>();
        private List<GameObject> addedObjects = new List<GameObject>();
        // Track modified renderers to revert their specific material
        private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();

        private void Start()
        {
            if (runOnStart)
            {
                SetupLighting();
            }
        }

        [ContextMenu("Setup Lighting")]
        public void SetupLighting()
        {
            // Clear lists to avoid duplicates if called multiple times without revert
            // (In a real tool we'd handle this better, but for experiment it's okay)
            
            // 1. Global Light
            GameObject globalLightObj = GameObject.Find("Global Light 2D");
            if (globalLightObj == null)
            {
                globalLightObj = new GameObject("Global Light 2D");
                Light2D light = globalLightObj.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.color = globalLightColor;
                light.intensity = globalLightIntensity;
                addedObjects.Add(globalLightObj);
            }
            else
            {
                Light2D light = globalLightObj.GetComponent<Light2D>();
                if (light != null)
                {
                    light.color = globalLightColor;
                    light.intensity = globalLightIntensity;
                }
            }

            // 2. Player Light
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                AddOrUpdateLight(player, "PlayerLight", playerLightColor, playerLightIntensity, playerLightRadius);
            }

            // 3. Enemy Lights
            // Find all objects with an EnemyAI component (or tagged "Enemy")
            // Assuming we check for EnemyAI script first as run-time approach
            MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var script in allScripts)
            {
                // Simple check for "Enemy" in script name or valid tag
                if (script.GetType().Name.Contains("Enemy") || script.gameObject.CompareTag("Enemy"))
                {
                     AddOrUpdateLight(script.gameObject, "EnemyLight", enemyLightColor, enemyLightIntensity, enemyLightRadius);
                }
            }

            // 4. Projectile Lights
            // Assuming projectiles might have "Projectile" in name or tag
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go.name.Contains("Projectile") || go.name.Contains("Bullet") || go.CompareTag("Projectile")) // Adjust tag as needed
                {
                     AddOrUpdateLight(go, "ProjectileLight", projectileLightColor, projectileLightIntensity, projectileLightRadius);
                }
            }


            // 5. Update Materials
            if (litMaterial == null)
            {
                litMaterial = GetLitMaterial();
            }

            if (litMaterial != null)
            {
                Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (Renderer r in renderers)
                {
                    // Skip if already tracked
                    if (originalMaterials.ContainsKey(r)) continue;

                    if (r is SpriteRenderer || r is ParticleSystemRenderer)
                    {
                        // Check if current material is suitable specifically for replacement
                        // Mostly "Sprites-Default"
                        if (r.sharedMaterial != null && (r.sharedMaterial.name == "Sprites-Default" || r.sharedMaterial.name == "Default-Particle"))
                        {
                            originalMaterials[r] = r.sharedMaterial;
                            r.sharedMaterial = litMaterial;
                        }
                    }
                }
            }

            // 6. Shadow Casters on Obstacles (Layer 6)
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer != -1)
            {
                foreach (GameObject go in allObjects)
                {
                    if (go.layer == obstacleLayer)
                    {
                        if (go.TryGetComponent<Collider2D>(out Collider2D col) && !go.TryGetComponent<ShadowCaster2D>(out ShadowCaster2D sc))
                        {
                            // If it has a CompositeCollider2D, we should only add ShadowCaster to that object
                            // (Usually CompositeCollider2D is on the same object as the RB2D/TilemapCollider2D)
                            
                            // Check if this object is part of a composite (if needed). 
                            // For simplicity, just add ShadowCaster2D. It usually works out.
                            ShadowCaster2D caster = go.AddComponent<ShadowCaster2D>();
                            caster.selfShadows = true;
                            addedComponents.Add(caster);
                        }
                    }
                }
            }
        }

        private void AddOrUpdateLight(GameObject target, string lightName, Color color, float intensity, float radius)
        {
            Transform existingLight = target.transform.Find(lightName);
            Light2D light;
            if (existingLight == null)
            {
                GameObject lightObj = new GameObject(lightName);
                lightObj.transform.SetParent(target.transform, false);
                lightObj.transform.localPosition = Vector3.zero;
                light = lightObj.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                    
                addedObjects.Add(lightObj);
            }
            else
            {
                light = existingLight.GetComponent<Light2D>();
            }

            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
        }

        [ContextMenu("Revert Lighting")]
        public void RevertLighting()
        {
            // Restore Materials
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.sharedMaterial = kvp.Value;
                }
            }
            originalMaterials.Clear();

            // Destroy Components
            foreach (var comp in addedComponents)
            {
                if (comp != null)
                {
                    if (Application.isPlaying) Destroy(comp);
                    else DestroyImmediate(comp);
                }
            }
            addedComponents.Clear();

            // Destroy Objects
            foreach (var obj in addedObjects)
            {
                if (obj != null)
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }
            addedObjects.Clear();
        }

        private Material GetLitMaterial()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("Sprite-Lit-Default t:Material");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
#endif
            return null;
        }
    }
}
