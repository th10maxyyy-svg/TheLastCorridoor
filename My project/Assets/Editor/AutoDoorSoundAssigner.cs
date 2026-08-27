using UnityEngine;
using UnityEditor;

public class AutoDoorSoundAssigner : EditorWindow
{
    public AudioClip woodenOpen;
    public AudioClip woodenClose;
    public AudioClip metalOpen;
    public AudioClip metalClose;

    [MenuItem("Tools/Auto Assign Door Sounds")]
    public static void ShowWindow()
    {
        GetWindow<AutoDoorSoundAssigner>("Assign Door Sounds");
    }

    void OnGUI()
    {
        GUILayout.Label("1. Put Sounds Here:", EditorStyles.boldLabel);
        woodenOpen = (AudioClip)EditorGUILayout.ObjectField("Wooden Open", woodenOpen, typeof(AudioClip), false);
        woodenClose = (AudioClip)EditorGUILayout.ObjectField("Wooden Close", woodenClose, typeof(AudioClip), false);
        
        metalOpen = (AudioClip)EditorGUILayout.ObjectField("Metal Open", metalOpen, typeof(AudioClip), false);
        metalClose = (AudioClip)EditorGUILayout.ObjectField("Metal Close", metalClose, typeof(AudioClip), false);

        GUILayout.Space(20);
        GUILayout.Label("2. Select Doors in Hierarchy, then click:", EditorStyles.boldLabel);

        if (GUILayout.Button("Assign WOODEN Sounds to SELECTED Doors", GUILayout.Height(35)))
        {
            AssignToSelected(false);
        }
        
        GUILayout.Space(5);

        if (GUILayout.Button("Assign METAL Sounds to SELECTED Doors", GUILayout.Height(35)))
        {
            AssignToSelected(true);
        }
    }

    void AssignToSelected(bool isMetal)
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int count = 0;

        foreach (GameObject obj in selectedObjects)
        {
            DoorController normalDoor = obj.GetComponentInChildren<DoorController>();
            if (normalDoor != null)
            {
                SerializedObject so = new SerializedObject(normalDoor);
                so.FindProperty("openSound").objectReferenceValue = isMetal ? metalOpen : woodenOpen;
                so.FindProperty("closeSound").objectReferenceValue = isMetal ? metalClose : woodenClose;
                so.ApplyModifiedProperties();
                count++;
            }

            SlidingDoorController slidingDoor = obj.GetComponentInChildren<SlidingDoorController>();
            if (slidingDoor != null)
            {
                SerializedObject so = new SerializedObject(slidingDoor);
                so.FindProperty("openSound").objectReferenceValue = isMetal ? metalOpen : woodenOpen;
                so.FindProperty("closeSound").objectReferenceValue = isMetal ? metalClose : woodenClose;
                so.ApplyModifiedProperties();
                count++;
            }
        }

        string type = isMetal ? "METAL" : "WOODEN";
        Debug.Log($"<color=green><b>SUCCESS:</b> Assigned {type} sounds to {count} selected doors!</color>");
    }
}
