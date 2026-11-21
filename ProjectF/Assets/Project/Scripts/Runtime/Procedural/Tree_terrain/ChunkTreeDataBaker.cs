using UnityEngine;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
    public GameObject[] treePrefabs;

    [Header("Spawn Settings")]
    public int treesPerChunk = 200;
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Terrain Layer Mask")]
    public LayerMask terrainLayer;

    [Header("Resources Target Folder")]
    [Tooltip("Folder path inside Assets/Resources where JSON files will be saved")]
    public string resourceFolderPath = "Assets/Resources/BakedTreeData";

    [ContextMenu("Bake Tree Coordinates to JSON (Zero RAM Leak)")]
    public void BakeTreeDataToJSON()
    {
#if UNITY_EDITOR
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("Please assign at least one tree prefab!");
            return;
        }

        if (!Directory.Exists(resourceFolderPath))
        {
            Directory.CreateDirectory(resourceFolderPath);
        }

        int totalTrees = 0;
        int totalChunks = transform.childCount;
        int currentChunkIndex = 0;
        System.Diagnostics.Stopwatch timer =
            System.Diagnostics.Stopwatch.StartNew();

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

                ChunkData chunkData = new ChunkData
                {
                    chunkName = chunk.name
                };

                Vector3 chunkPos = chunk.position;
                float halfSize = 250f;

                for (int i = 0; i < treesPerChunk; i++)
                {
                    float randomX = Random.Range(-halfSize, halfSize);
                    float randomZ = Random.Range(-halfSize, halfSize);
                    Vector3 rayOrigin = new Vector3(
                        chunkPos.x + randomX,
                        chunkPos.y + 1000f,
                        chunkPos.z + randomZ
                    );

                    if (!Physics.Raycast(
                            rayOrigin,
                            Vector3.down,
                            out RaycastHit hit,
                            2000f,
                            terrainLayer))
                    {
                        continue;
                    }

                    if (Vector3.Angle(hit.normal, Vector3.up) >= 35f)
                    {
                        continue;
                    }

                    int selectedPrefabIndex =
                        Random.Range(0, treePrefabs.Length);
                    float scaleValue = Random.Range(minScale, maxScale);
                    chunkData.trees.Add(new TreeData
                    {
                        position = hit.point,
                        rotation = Quaternion.Euler(
                            0f,
                            Random.Range(0f, 360f),
                            0f
                        ),
                        scale = new Vector3(
                            scaleValue,
                            scaleValue,
                            scaleValue
                        ),
                        prefabIndex = selectedPrefabIndex
                    });
                    totalTrees++;
                }

                string jsonString = JsonUtility.ToJson(chunkData, false);
                string filePath = Path.Combine(
                    resourceFolderPath,
                    $"{chunk.name}.json"
                );
                File.WriteAllText(filePath, jsonString);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            timer.Stop();
            Debug.Log(
                $"SUCCESS! Baked {totalTrees} trees across " +
                $"{totalChunks} chunks in {timer.ElapsedMilliseconds} ms. " +
                "RAM remains clean!"
            );
        }
#else
        Debug.LogWarning(
            "Chunk tree baking is only available in the Unity Editor.",
            this
        );
#endif
    }
}
