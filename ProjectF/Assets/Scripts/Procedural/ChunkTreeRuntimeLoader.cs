using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChunkTreeRuntimeLoader : MonoBehaviour
{
    [Header("Tree Prefabs")]
    public GameObject[] treePrefabs; // Combined tree prefabs with LOD Groups

    [Header("Player / Camera Target")]
    public Transform playerTransform;

    [Header("Streaming Settings")]
    public float loadDistance = 1000f;
    public string jsonResourcesPath = "BakedTreeData";

    [Header("Performance Tuning (Anti-Lag)")]
    [Tooltip("Maximum trees to instantiate per frame to ensure smooth FPS")]
    public int maxInstantiatesPerFrame = 15;

    [Header("Editor Preview Settings")]
    public bool showFullMeshInViewport = true;

    private float loadDistSq;
    private HashSet<Transform> loadingChunks = new HashSet<Transform>();

    // Cache baked JSON data in Editor for instant preview rendering
    private Dictionary<string, ChunkData> editorDataCache = new Dictionary<string, ChunkData>();

    void Start()
    {
        loadDistSq = loadDistance * loadDistance;

        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        InvokeRepeating(nameof(UpdateChunkStreaming), 0f, 0.3f);
    }

    void UpdateChunkStreaming()
    {
        if (playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;

        foreach (Transform chunk in transform)
        {
            float sqrDist = (chunk.position - playerPos).sqrMagnitude;
            bool shouldBeLoaded = sqrDist <= loadDistSq;

            if (shouldBeLoaded && chunk.childCount == 0 && !loadingChunks.Contains(chunk))
            {
                StartCoroutine(LoadChunkTreesAsync(chunk));
            }
            else if (!shouldBeLoaded && chunk.childCount > 0)
            {
                UnloadChunkTrees(chunk);
            }
        }
    }

    // Coroutine: Smooth time-sliced instantiation across multiple frames
    IEnumerator LoadChunkTreesAsync(Transform chunk)
    {
        loadingChunks.Add(chunk);

        TextAsset jsonAsset = Resources.Load<TextAsset>($"{jsonResourcesPath}/{chunk.name}");
        if (jsonAsset == null)
        {
            loadingChunks.Remove(chunk);
            yield break;
        }

        ChunkData chunkData = JsonUtility.FromJson<ChunkData>(jsonAsset.text);
        int spawnedInCurrentFrame = 0;

        foreach (var treeData in chunkData.trees)
        {
            if (treeData.prefabIndex < 0 || treeData.prefabIndex >= treePrefabs.Length) continue;

            GameObject treeInstance = Instantiate(
                treePrefabs[treeData.prefabIndex],
                treeData.position,
                treeData.rotation,
                chunk
            );

            treeInstance.transform.localScale = treeData.scale;
            spawnedInCurrentFrame++;

            // Distribute instantiation load across frames
            if (spawnedInCurrentFrame >= maxInstantiatesPerFrame)
            {
                spawnedInCurrentFrame = 0;
                yield return null; // Wait for the next frame
            }
        }

        Resources.UnloadAsset(jsonAsset);
        loadingChunks.Remove(chunk);
    }

    void UnloadChunkTrees(Transform chunk)
    {
        for (int i = chunk.childCount - 1; i >= 0; i--)
        {
            Destroy(chunk.GetChild(i).gameObject);
        }
    }

#if UNITY_EDITOR
    // Render full 3D meshes in Scene Viewport without generating GameObjects
    private void OnDrawGizmos()
    {
        if (!showFullMeshInViewport || Application.isPlaying || treePrefabs == null || treePrefabs.Length == 0) return;

        foreach (Transform chunk in transform)
        {
            string chunkName = chunk.name;

            if (!editorDataCache.ContainsKey(chunkName))
            {
                TextAsset jsonAsset = Resources.Load<TextAsset>($"{jsonResourcesPath}/{chunkName}");
                if (jsonAsset != null)
                {
                    editorDataCache[chunkName] = JsonUtility.FromJson<ChunkData>(jsonAsset.text);
                }
            }

            if (editorDataCache.TryGetValue(chunkName, out ChunkData data))
            {
                foreach (var treeData in data.trees)
                {
                    if (treeData.prefabIndex >= 0 && treeData.prefabIndex < treePrefabs.Length)
                    {
                        GameObject targetPrefab = treePrefabs[treeData.prefabIndex];
                        MeshFilter mf = targetPrefab.GetComponentInChildren<MeshFilter>();
                        MeshRenderer mr = targetPrefab.GetComponentInChildren<MeshRenderer>();

                        if (mf != null && mf.sharedMesh != null && mr != null && mr.sharedMaterial != null)
                        {
                            Graphics.DrawMesh(
                                mf.sharedMesh,
                                Matrix4x4.TRS(treeData.position, treeData.rotation, treeData.scale),
                                mr.sharedMaterial,
                                0
                            );
                        }
                    }
                }
            }
        }
    }
#endif
}