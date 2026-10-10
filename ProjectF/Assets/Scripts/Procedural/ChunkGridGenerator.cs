using UnityEngine;
using UnityEditor;

public class IslandChunkGenerator : MonoBehaviour
{
    [Header("Island Target Object (Terrain or Mesh)")]
    public GameObject islandObject;

    [Header("Grid Settings")]
    [Tooltip("Chunk size in meters (500m recommended for a 20km map)")]
    public float chunkSize = 500f;

    [ContextMenu("Generate Chunks For Island")]
    public void GenerateChunksForIsland()
    {
        if (islandObject == null)
        {
            Debug.LogError("Please assign the island object to the Island Object field!");
            return;
        }

        // 1. Clear previous chunks
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 2. Calculate target island bounds
        Bounds islandBounds = GetTargetBounds(islandObject);

        if (islandBounds.size == Vector3.zero)
        {
            Debug.LogError("Failed to find a Renderer or TerrainCollider on the specified island object!");
            return;
        }

        Vector3 min = islandBounds.min;
        Vector3 max = islandBounds.max;

        int createdChunks = 0;

        // 3. Iterate through world coordinates across the island bounds
        for (float x = min.x; x < max.x; x += chunkSize)
        {
            for (float z = min.z; z < max.z; z += chunkSize)
            {
                // Center point of the current chunk candidate
                Vector3 chunkCenter = new Vector3(x + chunkSize / 2f, 0f, z + chunkSize / 2f);

                // Verify if land/mesh exists underneath this coordinate via Raycast
                if (IsIslandUnderPoint(chunkCenter, islandBounds))
                {
                    // Get exact terrain height for the chunk center
                    float terrainHeight = GetTerrainHeight(chunkCenter);
                    chunkCenter.y = terrainHeight;

                    // Create chunk
                    int gridX = Mathf.FloorToInt((x - min.x) / chunkSize);
                    int gridZ = Mathf.FloorToInt((z - min.z) / chunkSize);

                    GameObject chunk = new GameObject($"Chunk_{gridX}_{gridZ}");
                    chunk.transform.SetParent(this.transform);
                    chunk.transform.position = chunkCenter;

                    createdChunks++;
                }
            }
        }

        Debug.Log($"Success! Created {createdChunks} chunks ({chunkSize}x{chunkSize}m) matching the island layout.");
    }

    private Bounds GetTargetBounds(GameObject go)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        Terrain terrain = go.GetComponent<Terrain>();
        if (terrain != null)
        {
            return new Bounds(
                terrain.transform.position + terrain.terrainData.size / 2f,
                terrain.terrainData.size
            );
        }

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            return r.bounds;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer childRend in renderers)
            {
                bounds.Encapsulate(childRend.bounds);
            }
            return bounds;
        }

        return bounds;
    }

    private bool IsIslandUnderPoint(Vector3 point, Bounds islandBounds)
    {
        // Cast ray downwards from above the highest point of the bounds
        Vector3 rayOrigin = new Vector3(point.x, islandBounds.max.y + 100f, point.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, islandBounds.size.y + 200f))
        {
            // Verify if ray hit the target island mesh or its children
            if (hit.transform.IsChildOf(islandObject.transform) || hit.transform == islandObject.transform)
            {
                return true;
            }
        }
        return false;
    }

    private float GetTerrainHeight(Vector3 point)
    {
        Vector3 rayOrigin = new Vector3(point.x, 2000f, point.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4000f))
        {
            return hit.point.y;
        }
        return 0f;
    }
}