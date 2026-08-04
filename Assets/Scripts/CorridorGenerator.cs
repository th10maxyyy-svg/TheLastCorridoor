using System.Collections.Generic;
using UnityEngine;

public class CorridorGenerator : MonoBehaviour
{
    [Header("Prefabs & Setup")]
    [SerializeField] private GameObject[] corridorPrefabs; // Drag your corridor room prefabs here
    [SerializeField] private int initialSegments = 3;       // How many corridors to spawn at the start
    [SerializeField] private Transform playerTransform;     // Reference to the Player object
    [SerializeField] private float spawnDistanceThreshold = 15f; // Distance from player to last segment to trigger new spawn
    [SerializeField] private int maxSegmentsOnScreen = 5;    // Keep memory low by deleting old rooms

    private List<GameObject> activeSegments = new List<GameObject>();
    private Vector3 nextSpawnPosition = Vector3.zero;
    private Quaternion nextSpawnRotation = Quaternion.identity;

    void Start()
    {
        if (corridorPrefabs == null || corridorPrefabs.Length == 0)
        {
            Debug.LogError("Please assign Corridor Prefabs in the CorridorGenerator script!");
            return;
        }

        if (playerTransform == null)
        {
            // Try to find the player by tag or script if not assigned
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerTransform = player.transform;
        }

        // Spawn initial corridors
        for (int i = 0; i < initialSegments; i++)
        {
            SpawnNextSegment();
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Check distance between player and the last spawned segment's position
        if (activeSegments.Count > 0)
        {
            GameObject lastSegment = activeSegments[activeSegments.Count - 1];
            float distance = Vector3.Distance(playerTransform.position, lastSegment.transform.position);

            // If player gets close to the end of the spawned corridors, spawn a new one
            if (distance < spawnDistanceThreshold)
            {
                SpawnNextSegment();
            }
        }
    }

    private void SpawnNextSegment()
    {
        // Pick a random prefab
        int index = Random.Range(0, corridorPrefabs.Length);
        GameObject selectedPrefab = corridorPrefabs[index];

        // Instantiate
        GameObject newSegment = Instantiate(selectedPrefab, nextSpawnPosition, nextSpawnRotation);
        newSegment.transform.SetParent(this.transform);
        activeSegments.Add(newSegment);

        // Find the 'ExitPoint' child in the spawned segment to set up the NEXT spawn position and rotation
        Transform exitPoint = FindExitPoint(newSegment.transform);
        if (exitPoint != null)
        {
            nextSpawnPosition = exitPoint.position;
            nextSpawnRotation = exitPoint.rotation;
        }
        else
        {
            // Fallback: If no exit point is found, just shift forward along Z axis by a default size (e.g. 10 units)
            nextSpawnPosition += nextSpawnRotation * Vector3.forward * 10f;
            Debug.LogWarning($"No 'ExitPoint' child found in {selectedPrefab.name}. Using default forward spacing.");
        }

        // Delete old segments to optimize memory
        if (activeSegments.Count > maxSegmentsOnScreen)
        {
            GameObject oldSegment = activeSegments[0];
            activeSegments.RemoveAt(0);
            Destroy(oldSegment);
        }
    }

    private Transform FindExitPoint(Transform parent)
    {
        // Search recursively for a child named "ExitPoint"
        foreach (Transform child in parent)
        {
            if (child.name.Equals("ExitPoint", System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
            Transform found = FindExitPoint(child);
            if (found != null) return found;
        }
        return null;
    }
}
