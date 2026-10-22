#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// New Mixamo animations for the joker in the ESCAPE cinematic:
///   Mutant Idle  (breathing, staring at the player)      - Whiteclown N Hallin@Mutant Breathing Idle.fbx
///   Mutant Roar  (big roar before he charges)             - Whiteclown N Hallin@Mutant Roaring.fbx
///   Mutant Run   (the charge, and the panicked flee)      - Whiteclown N Hallin@Mutant Run.fbx
///   Light Hit    (the sunlight hits him, he staggers)     - Whiteclown N Hallin@Zombie Stumble Back.fbx
///   Agony        (burning, writhing in the light)         - Whiteclown N Hallin@Zombie In Agony.fbx
/// Runs once by itself: makes the FBX files Humanoid, sets loop / in-place on the clips and adds the 5 states to
/// Assets/clown/clownanimator.controller (no transitions - EscapeCinematicManager cross-fades into them by name).
/// Can be run again from the menu: The Last Corridor / Setup Joker Escape Animations.
/// </summary>
[InitializeOnLoad]
public static class JokerEscapeAnimSetup
{
    private const string PrefKey = "TLC_JokerEscapeAnims_v1_";
    private const string Folder = "Assets/clown/";
    private const string Prefix = "Whiteclown N Hallin@";
    private const string ControllerPath = "Assets/clown/clownanimator.controller";
    private const string AvatarSourceFbx = "Assets/clown/Whiteclown N Hallin@Zombie Run.fbx";

    private struct Entry { public string file, clip, state; public bool loop; public Vector3 pos; }
    private static readonly Entry[] Entries =
    {
        new Entry { file = "Mutant Breathing Idle", clip = "MutantBreathingIdle", state = "Mutant Idle", loop = true,  pos = new Vector3(600, 300, 0) },
        new Entry { file = "Mutant Roaring",        clip = "MutantRoaring",       state = "Mutant Roar", loop = false, pos = new Vector3(600, 360, 0) },
        new Entry { file = "Mutant Run",            clip = "MutantRun",           state = "Mutant Run",  loop = true,  pos = new Vector3(600, 420, 0) },
        new Entry { file = "Zombie Stumble Back",   clip = "ZombieStumbleBack",   state = "Light Hit",   loop = false, pos = new Vector3(600, 480, 0) },
        new Entry { file = "Zombie In Agony",       clip = "ZombieInAgony",       state = "Agony",       loop = true,  pos = new Vector3(600, 540, 0) },
    };

    private static bool triedThisSession;

    static JokerEscapeAnimSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Run()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Setup Joker Escape Animations")]
    private static void Menu()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        Run();
    }

    private static bool Run()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { Debug.LogWarning("[TLC] Joker anims: " + ControllerPath + " not found."); return false; }

        int added = 0;
        foreach (Entry e in Entries)
        {
            string path = Folder + Prefix + e.file + ".fbx";
            AnimationClip clip = ImportClip(path, e);
            if (clip == null) { Debug.LogWarning("[TLC] Joker anims: no clip in " + path); continue; }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == e.state);
            if (state == null) state = sm.AddState(e.state, e.pos);
            state.motion = clip;
            state.writeDefaultValues = true;
            state.speed = 1f;
            added++;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[TLC] Joker escape animations ready: " + added + "/" + Entries.Length + " states in clownanimator.controller");
        return added == Entries.Length;
    }

    private static AnimationClip ImportClip(string path, Entry e)
    {
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) return null;

        // 1. Humanoid, own avatar (same Mixamo skeleton as the other clown clips)
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.clipAnimations = new ModelImporterClipAnimation[0];
        mi.SaveAndReimport();

        // the avatar could not be made from this file -> copy the one of the existing run clip
        Avatar av = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (av == null || !av.isValid || !av.isHuman)
        {
            Avatar src = AssetDatabase.LoadAllAssetsAtPath(AvatarSourceFbx).OfType<Avatar>().FirstOrDefault();
            if (src != null)
            {
                mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                mi.sourceAvatar = src;
                mi.SaveAndReimport();
            }
        }

        // 2. one clip, the whole take, in place
        ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return null;
        ModelImporterClipAnimation c = clips[0];
        c.name = e.clip;
        c.loopTime = e.loop;
        c.loopPose = e.loop;
        c.lockRootRotation = true;  c.keepOriginalOrientation = true;
        c.lockRootHeightY = true;   c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = false;
        mi.clipAnimations = new[] { c };
        mi.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                            .FirstOrDefault(a => !a.name.StartsWith("__preview__"));
    }
}
#endif
