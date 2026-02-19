using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

[InitializeOnLoad]
public class LightingAutoSetup
{
    static LightingAutoSetup()
    {
        EditorApplication.update += Update;
    }

    static void Update()
    {
        EditorApplication.update -= Update;
        SetupURP2D();
    }

    static void SetupURP2D()
    {
        var renderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (renderPipelineAsset == null)
        {
            Debug.LogWarning("LightingAutoSetup: No URP Asset found in Graphics Settings.");
            return;
        }

        // Check if we are already using a 2D Renderer
        // We need to access the renderer data list. This is private in some versions, ensuring we get it.
        ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(renderPipelineAsset);

        if (rendererDataList == null || rendererDataList.Length == 0)
        {
             Debug.Log("LightingAutoSetup: No renderers found in URP Asset.");
        }
        else if (rendererDataList[0] is Renderer2DData)
        {
            // Already set up!
            return;
        }

        Debug.Log("LightingAutoSetup: Current default renderer is NOT 2D. Creating and switching to URP 2D Renderer...");

        // Create new 2D Renderer Data
        Renderer2DData data = ScriptableObject.CreateInstance<Renderer2DData>();
        data.name = "AutoURP2D_Renderer";
        
        // Save it
        string path = "Assets/Settings/AutoURP2D_Renderer.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }
        
        AssetDatabase.CreateAsset(data, path);
        
        // Assign it to the URP Asset (using reflection because m_RendererDataList is internal/private usually)
        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            rendererDataList = new ScriptableRendererData[1];
        }
        rendererDataList[0] = data;

        typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(renderPipelineAsset, rendererDataList);

        EditorUtility.SetDirty(renderPipelineAsset);
        AssetDatabase.SaveAssets();

        Debug.Log("LightingAutoSetup: Switched to URP 2D Renderer!");
    }
}
