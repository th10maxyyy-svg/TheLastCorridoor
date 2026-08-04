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

    void Start()
    {
        if (corridorPrefabs == null || corridorPrefabs.Length == 0)
        {
            Debug.LogError("Corridor Prefabs list khali hai! Inspector me prefabs assign karein.");
            return;
        }

        if (playerTransform == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) playerTransform = player.transform;
        }

        for (int i = 0; i < initialSegments; i++)
        {
            SpawnNextSegment();
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
}
