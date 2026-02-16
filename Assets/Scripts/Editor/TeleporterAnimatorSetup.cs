using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class TeleporterAnimatorSetup
{
    [MenuItem("Tools/Setup Teleporter Animator")]
    public static void Setup()
    {
        // 1. Get Selected GameObject
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogError("Please select the Teleporter Enemy GameObject in the scene or prefab editor.");
            return;
        }

        // 2. Locate or Add Animator
        Animator animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
            Debug.Log("Added Animator component to " + go.name);
        }

        // 3. Find/Create Controller
        string controllerPath = "Assets/Sprites/Animations/Enemies/Teleporter.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            // Create if missing
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log("Created new Animator Controller at " + controllerPath);
        }
        else
        {
            Debug.Log("Found existing Animator Controller at " + controllerPath);
        }

        animator.runtimeAnimatorController = controller;

        // 4. Add Parameters
        AddParameter(controller, "Appear", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Disappear", AnimatorControllerParameterType.Trigger);

        // 5. Load Clips
        AnimationClip defaultClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Animations/Enemies/TeleporterDefault.anim");
        AnimationClip disappearClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Animations/Enemies/TeleporterDisappear.anim");

        if (disappearClip == null)
        {
            Debug.LogError("Could not find TeleporterDisappear.anim at Assets/Sprites/Animations/Enemies/TeleporterDisappear.anim");
            return;
        }

        // 6. Setup States (Layer 0)
        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine sm = layer.stateMachine;

        // Clean up old states if needed? No, let's just add/update.
        
        // Default State
        var defaultState = FindOrCreateState(sm, "Default", defaultClip);
        sm.defaultState = defaultState;

        // Disappear State
        var disappearState = FindOrCreateState(sm, "Disappear", disappearClip);
        disappearState.speed = 1.0f;

        // Appear State (Reverse Disappear)
        var appearState = FindOrCreateState(sm, "Appear", disappearClip);
        appearState.speed = -1.0f;

        // Hidden State (Empty)
        var hiddenState = FindOrCreateState(sm, "Hidden", null);

        // 7. Setup Transitions
        // Any State -> Disappear
        var trans1 = sm.AddAnyStateTransition(disappearState);
        trans1.AddCondition(AnimatorConditionMode.If, 0, "Disappear");
        trans1.duration = 0f;
        trans1.hasExitTime = false;

        // Disappear -> Hidden
        var trans2 = disappearState.AddTransition(hiddenState);
        trans2.hasExitTime = true;
        trans2.exitTime = 1.0f; // Wait for full animation
        trans2.duration = 0f;

        // Hidden -> Appear
        var trans3 = hiddenState.AddTransition(appearState);
        trans3.AddCondition(AnimatorConditionMode.If, 0, "Appear");
        trans3.duration = 0f;
        trans3.hasExitTime = false;

        // Appear -> Exit (or Default)
        // Let's go to Default
        var trans4 = appearState.AddTransition(defaultState);
        trans4.hasExitTime = true;
        trans4.exitTime = 1.0f; // Wait for full animation
        trans4.duration = 0f;

        Debug.Log("Teleporter Animator Setup Complete!");
    }

    static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }
        controller.AddParameter(name, type);
    }

    static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        foreach (var state in sm.states)
        {
            if (state.state.name == name)
            {
                state.state.motion = clip; // Update clip just in case
                return state.state;
            }
        }
        var newState = sm.AddState(name);
        newState.motion = clip;
        return newState;
    }
}
