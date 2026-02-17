using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class LaserChargeAnimationSetup : EditorWindow
{
    [MenuItem("Tools/Setup Laser Charge Animation")]
    public static void SetupAnimation()
    {
        // Paths
        string spriteSheetPath = "Assets/Sprites/UIandMisc/laserframe.png";
        string animationPath = "Assets/Sprites/Animations/UI/LaserCharge.anim";
        string controllerPath = "Assets/Sprites/Animations/UI/LaserChargeAnimator.controller";
        
        // Ensure directories exist
        string animDir = Path.GetDirectoryName(animationPath);
        if (!Directory.Exists(animDir))
        {
            Directory.CreateDirectory(animDir);
            AssetDatabase.Refresh();
        }
        
        // Load all sprites from the sprite sheet
        Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetPath);
        Sprite[] laserSprites = new Sprite[15];
        
        // Extract sprites in order (laserframe_0 to laserframe_14)
        foreach (Object obj in sprites)
        {
            if (obj is Sprite sprite)
            {
                string spriteName = sprite.name;
                if (spriteName.StartsWith("laserframe_"))
                {
                    string indexStr = spriteName.Replace("laserframe_", "");
                    if (int.TryParse(indexStr, out int index) && index >= 0 && index < 15)
                    {
                        laserSprites[index] = sprite;
                    }
                }
            }
        }
        
        // Verify all sprites were found
        bool allFound = true;
        for (int i = 0; i < 15; i++)
        {
            if (laserSprites[i] == null)
            {
                Debug.LogError($"Missing sprite: laserframe_{i}");
                allFound = false;
            }
        }
        
        if (!allFound)
        {
            Debug.LogError("Failed to load all laser sprites!");
            return;
        }
        
        // Create Animation Clip
        AnimationClip clip = new AnimationClip();
        clip.frameRate = 15; // 15 frames per second
        
        // Create sprite keyframes for UI Image component
        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(UnityEngine.UI.Image);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";
        
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[15];
        for (int i = 0; i < 15; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe();
            keyframes[i].time = i / 15f; // Evenly distribute over 1 second
            keyframes[i].value = laserSprites[i];
        }
        
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        
        // Set animation to not loop (we'll control it manually)
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        
        // Save animation clip
        AssetDatabase.CreateAsset(clip, animationPath);
        Debug.Log($"Created animation clip at: {animationPath}");
        
        // Create Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // Add the animation to the controller
        AnimatorState state = controller.layers[0].stateMachine.AddState("LaserCharge");
        state.motion = clip;
        state.speed = 0; // Speed 0 so we can control it manually via normalized time
        
        // Set as default state
        controller.layers[0].stateMachine.defaultState = state;
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Created animator controller at: {controllerPath}");
        Debug.Log("Laser Charge Animation setup complete!");
        Debug.Log("Next steps:");
        Debug.Log("1. Add an Animator component to your LaserEnergyUI GameObject");
        Debug.Log("2. Assign the LaserChargeAnimator controller to the Animator");
        Debug.Log("3. Add a SpriteRenderer or Image component to display the sprite");
        Debug.Log("4. The LaserEnergyUI script will control the animation automatically");
    }
}
