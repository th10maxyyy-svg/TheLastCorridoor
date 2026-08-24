using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class SetupPlayerAnimator : Editor
{
    [MenuItem("Tools/2-Click Setup Player Animations")]
    public static void CreateController()
    {
        AnimationClip idle = null, walk = null, sprint = null, crouch = null, interact = null;
        
        // Jo files user ne select ki hui hain unme se animations dhoondo
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lowerPath = path.ToLower();

            // Agar user ne direct Mixamo wali FBX files select ki hain
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        if (lowerPath.Contains("idle")) idle = clip;
                        else if (lowerPath.Contains("walk") && !lowerPath.Contains("crouch")) walk = clip;
                        else if (lowerPath.Contains("run") || lowerPath.Contains("sprint")) sprint = clip;
                        else if (lowerPath.Contains("crouch")) crouch = clip;
                        else if (lowerPath.Contains("interact") || lowerPath.Contains("pick")) interact = clip;
                    }
                }
            }
            else 
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    if (lowerPath.Contains("idle")) idle = clip;
                    else if (lowerPath.Contains("walk") && !lowerPath.Contains("crouch")) walk = clip;
                    else if (lowerPath.Contains("run") || lowerPath.Contains("sprint")) sprint = clip;
                    else if (lowerPath.Contains("crouch")) crouch = clip;
                    else if (lowerPath.Contains("interact") || lowerPath.Contains("pick")) interact = clip;
                }
            }
        }

        if (idle == null)
        {
            EditorUtility.DisplayDialog("Error", "Bhai pehle Project Window mein apni kam az kam 'Idle' aur 'Walk' wali animations (FBX files) par CTRL daba kar click karein taake wo select ho jayen, phir ye button dabayen!", "Theek Hai");
            return;
        }

        // Animator Controller Banayen
        string animPath = "Assets/PlayerAnimator.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(animPath);

        // Parameters add karein jo humne PlayerController mein use kiye thay
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("InteractTrigger", AnimatorControllerParameterType.Trigger);

        // States add karein
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idle;

        AnimatorState walkState = null;
        if (walk != null) { walkState = rootStateMachine.AddState("Walk"); walkState.motion = walk; }

        AnimatorState sprintState = null;
        if (sprint != null) { sprintState = rootStateMachine.AddState("Sprint"); sprintState.motion = sprint; }

        AnimatorState crouchState = null;
        if (crouch != null) { crouchState = rootStateMachine.AddState("Crouch"); crouchState.motion = crouch; }

        AnimatorState interactState = null;
        if (interact != null) { interactState = rootStateMachine.AddState("Interact"); interactState.motion = interact; }

        // Transitions lagayen taake character theek waqt par theek animation chalaye
        if (interactState != null)
        {
            AnimatorStateTransition anyToInteract = rootStateMachine.AddAnyStateTransition(interactState);
            anyToInteract.AddCondition(AnimatorConditionMode.If, 0, "InteractTrigger");
            anyToInteract.hasExitTime = false;

            AnimatorStateTransition interactToIdle = interactState.AddTransition(idleState);
            interactToIdle.hasExitTime = true;
            interactToIdle.exitTime = 0.8f;
        }

        if (walkState != null)
        {
            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToWalk.hasExitTime = false;

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            walkToIdle.hasExitTime = false;

            if (sprintState != null)
            {
                AnimatorStateTransition walkToSprint = walkState.AddTransition(sprintState);
                walkToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
                walkToSprint.hasExitTime = false;

                AnimatorStateTransition sprintToWalk = sprintState.AddTransition(walkState);
                sprintToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
                sprintToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
                sprintToWalk.hasExitTime = false;

                AnimatorStateTransition sprintToIdle = sprintState.AddTransition(idleState);
                sprintToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
                sprintToIdle.hasExitTime = false;
            }
        }

        if (crouchState != null)
        {
            AnimatorStateTransition idleToCrouch = idleState.AddTransition(crouchState);
            idleToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            idleToCrouch.hasExitTime = false;

            AnimatorStateTransition crouchToIdle = crouchState.AddTransition(idleState);
            crouchToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            crouchToIdle.hasExitTime = false;

            if (walkState != null)
            {
                AnimatorStateTransition walkToCrouch = walkState.AddTransition(crouchState);
                walkToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
                walkToCrouch.hasExitTime = false;

                AnimatorStateTransition crouchToWalk = crouchState.AddTransition(walkState);
                crouchToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
                crouchToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
                crouchToWalk.hasExitTime = false;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Magic Complete!", "Aapka 'PlayerAnimator' automatically ban gaya hai aur saari settings ho gayi hain!\n\nAb Hierarchy mein apna player (remy) select karein aur Inspector mein uske Animator ke 'Controller' dabbe mein 'Assets' folder se 'PlayerAnimator' daal dein.", "Zabardast!");
    }
}
