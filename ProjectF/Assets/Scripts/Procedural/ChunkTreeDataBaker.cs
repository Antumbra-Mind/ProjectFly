using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public struct TreeData
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public int prefabIndex;
}

[System.Serializable]
public class ChunkData
{
    public string chunkName;
    public List<TreeData> trees = new List<TreeData>();
}

public class ChunkTreeDataBaker : MonoBehaviour
{
    [Header("Tree Prefabs Reference")]
    public GameObject[] treePrefabs; // Array of combined tree prefabs with LOD Groups

    [Header("Spawn Settings")]
    public int treesPerChunk = 200; // Target tree count per chunk
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Terrain Layer Mask")]
    public LayerMask terrainLayer; // Ground / Terrain layer mask for Raycasting

    [Header("Resources Target Folder")]
    [Tooltip("Folder path inside Assets/Resources where JSON files will be saved")]
    public string resourceFolderPath = "Assets/Resources/BakedTreeData";

    [ContextMenu("Bake Tree Coordinates to JSON (Zero RAM Leak)")]
    public void BakeTreeDataToJSON()
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("Please assign at least one tree prefab!");
            return;
        }

        // Ensure Resources directory exists for runtime loading
        if (!Directory.Exists(resourceFolderPath))
        {
            Directory.CreateDirectory(resourceFolderPath);
        }

        int totalTrees = 0;
        int totalChunks = transform.childCount;
        int currentChunkIndex = 0;

        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            foreach (Transform chunk in transform)
            {
                currentChunkIndex++;

                EditorUtility.DisplayProgressBar(
                    "Baking Tree Coordinates to JSON",
                    $"Processing Chunk {currentChunkIndex}/{totalChunks} ({chunk.name})...",
                    (float)currentChunkIndex / totalChunks
                );

                ChunkData chunkData = new ChunkData();
                chunkData.chunkName = chunk.name;

                Vector3 chunkPos = chunk.position;
                float halfSize = 250f; // Half size for a 500m chunk

                for (int i = 0; i < treesPerChunk; i++)
                {
                    float randomX = Random.Range(-halfSize, halfSize);
                    float randomZ = Random.Range(-halfSize, halfSize);

                    Vector3 rayOrigin = new Vector3(chunkPos.x + randomX, chunkPos.y + 1000f, chunkPos.z + randomZ);

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, terrainLayer))
                    {
                        // Slope angle check (e.g. skip cliffs steeper than 35 degrees)
                        if (Vector3.Angle(hit.normal, Vector3.up) < 35f)
                        {
                            int selectedPrefabIndex = Random.Range(0, treePrefabs.Length);
                            float scaleValue = Random.Range(minScale, maxScale);

                            TreeData tree = new TreeData
                            {
                                position = hit.point,
                                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                                scale = new Vector3(scaleValue, scaleValue, scaleValue),
                                prefabIndex = selectedPrefabIndex
                            };

                            chunkData.trees.Add(tree);
                            totalTrees++;
                        }
                    }
                }

                // Write JSON file directly to disk without Editor AssetDatabase overhead
                string jsonString = JsonUtility.ToJson(chunkData, false);
                string filePath = Path.Combine(resourceFolderPath, $"{chunk.name}.json");
                File.WriteAllText(filePath, jsonString);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            timer.Stop();

            Debug.Log($"SUCCESS! Baked {totalTrees} trees across {totalChunks} chunks in {timer.ElapsedMilliseconds} ms. RAM remains clean!");
        }
    }
}