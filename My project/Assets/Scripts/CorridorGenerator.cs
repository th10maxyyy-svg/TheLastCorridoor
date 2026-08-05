using System.Collections.Generic;
using UnityEngine;

public class CorridorGenerator : MonoBehaviour
{
    [Header("Prefabs & Setup")]
    [SerializeField] private GameObject[] corridorPrefabs;
    [SerializeField] private int initialSegments = 3;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float spawnDistanceThreshold = 15f;
    [SerializeField] private int maxSegmentsOnScreen = 5;

    private List<GameObject> activeSegments = new List<GameObject>();
    private Vector3 nextSpawnPosition = new Vector3(0, 0, 20f);
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private Unity.AI.Navigation.NavMeshSurface globalSurface;

    void Start()
    {
        globalSurface = FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();

        if (corridorPrefabs == null || corridorPrefabs.Length == 0)
        {
            Debug.LogError("Corridor Prefabs list khali hai! Inspector me prefabs assign karein.");
            return;
        }

        // Scene mein majood aakhri kamre (jaise room2) ka ExitPoint dhoondein taake naya kamra theek wahan se shuru ho
        Transform lastExit = FindExitPointInScene();
        if (lastExit != null)
        {
            nextSpawnPosition = lastExit.position;
            nextSpawnRotation = lastExit.rotation;
        }

        if (playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) playerTransform = player.transform;
        }

        for (int i = 0; i < initialSegments; i++)
        {
            SpawnNextSegment(); // Game start hotay waqt
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        if (activeSegments.Count > 0)
        {
            GameObject lastSegment = activeSegments[activeSegments.Count - 1];
            float distance = Vector3.Distance(playerTransform.position, lastSegment.transform.position);

            if (distance < spawnDistanceThreshold)
            {
                SpawnNextSegment();
            }
        }
    }

    private void SpawnNextSegment()
    {
        int index = Random.Range(0, corridorPrefabs.Length);
        GameObject selectedPrefab = corridorPrefabs[index];

        GameObject newSegment = Instantiate(selectedPrefab, nextSpawnPosition, nextSpawnRotation);
        newSegment.transform.SetParent(this.transform);
        activeSegments.Add(newSegment);

        // NOTE: Runtime NavMesh baking has been removed to fix lag.
        // NavMesh should be baked on the prefabs themselves.

        Transform exitPoint = FindExitPoint(newSegment.transform);
        if (exitPoint != null)
        {
            nextSpawnPosition = exitPoint.position;
            nextSpawnRotation = exitPoint.rotation;
        }
        else
        {
            nextSpawnPosition += nextSpawnRotation * Vector3.forward * 10f;
            Debug.LogWarning("No 'ExitPoint' found in " + selectedPrefab.name + ". Using default spacing.");
        }

        if (activeSegments.Count > maxSegmentsOnScreen)
        {
            GameObject oldSegment = activeSegments[0];
            activeSegments.RemoveAt(0);
            Destroy(oldSegment);
        }
    }

    private Transform FindExitPoint(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals("ExitPoint", System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform found = FindExitPoint(child);
            if (found != null) return found;
        }
        return null;
    }

    private void FindAllExitPoints(Transform parent, List<Transform> results)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals("ExitPoint", System.StringComparison.OrdinalIgnoreCase))
            {
                results.Add(child);
            }
            FindAllExitPoints(child, results);
        }
    }

    private Transform FindExitPointInScene()
    {
        // Sab se aakhri ExitPoint dhoondne ke liye scene ke tamaam root objects check karein
        List<Transform> allExitPoints = new List<Transform>();
        foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            FindAllExitPoints(rootObj.transform, allExitPoints);
        }

        Transform bestExit = null;
        float maxZ = float.MinValue;

        foreach (Transform exit in allExitPoints)
        {
            // Jo sab se aagay (Z axis me) ho wo current end hoga
            if (exit.position.z > maxZ)
            {
                maxZ = exit.position.z;
                bestExit = exit;
            }
        }
        
        return bestExit;
    }
}
