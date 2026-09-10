using UnityEngine;
using UnityEditor;

public class CreateClassroomIllusionTool : EditorWindow
{
    [MenuItem("Tools/Create Classroom Illusion Setup")]
    public static void CreateSetup()
    {
        // 1. Main Parent
        GameObject mainParent = new GameObject("Classroom_Illusion_Setup");
        ClassroomIllusionManager manager = mainParent.AddComponent<ClassroomIllusionManager>();

        // 2. Entrance Trap Trigger
        GameObject trapTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trapTrigger.name = "Entrance_Trap_Trigger";
        trapTrigger.transform.SetParent(mainParent.transform);
        trapTrigger.transform.localPosition = new Vector3(0, 1f, -2f);
        trapTrigger.transform.localScale = new Vector3(3f, 2f, 1f);
        
        Collider trapCol = trapTrigger.GetComponent<Collider>();
        trapCol.isTrigger = true;
        trapTrigger.GetComponent<MeshRenderer>().enabled = false; // Hide it
        
        IllusionEnterTrigger trapScript = trapTrigger.AddComponent<IllusionEnterTrigger>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(trapScript.onEnter, manager.TrapPlayer);

        // 3. Fake Brick Wall (The one that replaces the door)
        GameObject fakeWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fakeWall.name = "Fake_Wall_Doorway";
        fakeWall.transform.SetParent(mainParent.transform);
        fakeWall.transform.localPosition = new Vector3(0, 1.5f, -3f);
        fakeWall.transform.localScale = new Vector3(1.5f, 3f, 0.2f);
        fakeWall.SetActive(false); // Hidden initially
        manager.fakeWall = fakeWall;

        // 4. Normal Environment Parent
        GameObject normalEnv = new GameObject("Normal_Environment");
        normalEnv.transform.SetParent(mainParent.transform);
        manager.normalEnvironment = normalEnv;

        // 5. Nightmare Environment Parent
        GameObject nightmareEnv = new GameObject("Nightmare_Environment");
        nightmareEnv.transform.SetParent(mainParent.transform);
        nightmareEnv.SetActive(false); // Hidden initially
        manager.nightmareEnvironment = nightmareEnv;

        // 6. Trigger Object (E.g. Radio/Apple)
        GameObject triggerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        triggerObj.name = "Trigger_Object_Radio";
        triggerObj.transform.SetParent(normalEnv.transform);
        triggerObj.transform.localPosition = new Vector3(0, 1f, 0);
        triggerObj.transform.localScale = new Vector3(0.4f, 0.2f, 0.2f);
        
        IllusionTriggerObject triggerScript = triggerObj.AddComponent<IllusionTriggerObject>();
        triggerScript.interactPrompt = "Press E to Inspect Object";
        UnityEditor.Events.UnityEventTools.AddPersistentListener(triggerScript.onInteract, manager.StartNightmare);

        // 7. Scare Statue (The Ghost holding key)
        GameObject scareStatue = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        scareStatue.name = "Scare_Statue_Ghost";
        scareStatue.transform.SetParent(mainParent.transform);
        scareStatue.transform.localPosition = new Vector3(0, 1f, -2.5f); // Near the door
        scareStatue.SetActive(false);
        manager.scareStatue = scareStatue;

        // Auto-select
        Selection.activeGameObject = mainParent;
        Debug.Log("AAA Classroom Illusion Setup created automatically!");
    }
}
