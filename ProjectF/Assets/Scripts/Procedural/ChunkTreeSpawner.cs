#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.IO;

public class ChunkTreeSpawner : MonoBehaviour
{
    [Header("Tree Prefabs")]
    public GameObject[] treePrefabs;

    [Header("Spawn Settings")]
    public int treesPerChunk = 100;
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Terrain Layer Mask")]
    public LayerMask terrainLayer;

    [Header("Folder to Save Chunk Prefabs")]
    public string saveFolderPath = "Assets/GeneratedChunks";

    [ContextMenu("Spawn and Bake Chunks (Zero Memory Leak)")]
    public void SpawnAndBakeChunks()
    {
#if UNITY_EDITOR
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("Please assign at least one tree prefab!");
            return;
        }

        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
            AssetDatabase.Refresh();
        }

        int totalTrees = 0;
        int totalChunks = transform.childCount;
        int currentChunkIndex = 0;

        try
        {
            foreach (Transform chunk in transform)
            {
                currentChunkIndex++;
                EditorUtility.DisplayProgressBar(
                    "Spawning and Baking Chunks",
                    $"Baking Chunk {currentChunkIndex}/{totalChunks} ({chunk.name})...",
                    (float)currentChunkIndex / totalChunks
                );

                for (int i = chunk.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(chunk.GetChild(i).gameObject);
                }

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

                    GameObject randomTreePrefab =
                        treePrefabs[Random.Range(0, treePrefabs.Length)];
                    GameObject spawnedTree = Instantiate(
                        randomTreePrefab,
                        hit.point,
                        Quaternion.identity,
                        chunk
                    );
                    spawnedTree.transform.rotation = Quaternion.Euler(
                        0f,
                        Random.Range(0f, 360f),
                        0f
                    );
                    float scale = Random.Range(minScale, maxScale);
                    spawnedTree.transform.localScale =
                        new Vector3(scale, scale, scale);
                    totalTrees++;
                }

                string prefabPath =
                    $"{saveFolderPath}/{chunk.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(
                    chunk.gameObject,
                    prefabPath
                );

                for (int i = chunk.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(chunk.GetChild(i).gameObject);
                }

                Undo.ClearAll();
                EditorUtility.UnloadUnusedAssetsImmediate();
                System.GC.Collect();
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Successfully baked {totalChunks} chunks ({totalTrees} trees) " +
                $"to '{saveFolderPath}'. Scene memory cleared!"
            );
        }
#else
        Debug.LogWarning(
            "Chunk spawning and baking is only available in the Unity Editor.",
            this
        );
#endif
    }
}
