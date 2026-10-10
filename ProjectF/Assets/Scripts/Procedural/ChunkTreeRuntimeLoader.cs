using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChunkTreeRuntimeLoader : MonoBehaviour
{
    [Header("Tree Prefabs")]
    public GameObject[] treePrefabs;

    [Header("Player / Camera Target")]
    public Transform playerTransform;

    [Header("Streaming Settings")]
    [Tooltip("Legacy near-field distance. Used when renderDistance is zero.")]
    public float loadDistance = 1000f;
    [Min(0f)]
    [Tooltip("Maximum distance at which instanced trees are submitted. Zero uses loadDistance.")]
    public float renderDistance = 5000f;
    public string jsonResourcesPath = "BakedTreeData";
    [Min(0.02f)]
    public float streamingUpdateInterval = 0.15f;
    [Min(0f)]
    [Tooltip("Seconds of forward flight used to predict upcoming chunks.")]
    public float preloadLookAheadSeconds = 2f;
    [Min(0f)]
    public float preloadSafetyDistance = 500f;
    [Min(0f)]
    public float maxPreloadDistance = 2500f;
    [Min(1)]
    public int maxConcurrentChunkLoads = 1;

    [Header("Rendering")]
    [Tooltip("Use prefab instances only inside nearFieldDistance. Disabled by default because trees have no gameplay dependency.")]
    public bool useIndividualNearField = false;
    [Min(0f)]
    public float nearFieldDistance = 300f;
    [Min(0)]
    public int maxInstantiatesPerFrame = 8;
    [Min(0)]
    public int maxDestroysPerFrame = 16;
    [Range(0, 1)]
    [Tooltip("LOD used by instanced trees. LOD 1 is the existing low-detail mesh.")]
    public int instancedLodIndex = 1;
    public bool instancedCastShadows = false;
    public bool instancedReceiveShadows = true;
    [Min(1)]
    public int chunkSize = 500;

    [Header("Memory")]
    [Tooltip("Approximate managed memory budget for parsed JSON data.")]
    [Min(0)]
    public int maxCachedDataBytes = 4 * 1024 * 1024;

    [Header("Diagnostics")]
    public bool enableDiagnostics = false;
    [Min(0.5f)]
    public float diagnosticsLogInterval = 5f;

    [Header("Editor Preview")]
    public bool showFullMeshInViewport = true;
    [Min(0)]
    public int editorPreviewMaxTrees = 12000;
    [Min(0)]
    public int editorPreviewMaxChunks = 64;
    [Min(0f)]
    public float editorPreviewDistance = 5000f;

    private const int MaxInstancesPerDraw = 1023;

    private sealed class RenderBatch
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        public Matrix4x4[] matrices;
        public Bounds bounds;
    }

    private sealed class RenderPart
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        public Matrix4x4 localMatrix;
    }

    private sealed class PrefabRenderData
    {
        public RenderPart[] parts;
    }

    private sealed class ChunkRuntimeState
    {
        public Transform chunk;
        public Bounds bounds;
        public ChunkData data;
        public int nextTreeIndex;
        public bool wantsToLoad;
        public bool wantsToRender;
        public bool wantsIndividual;
        public bool individualMode;
        public bool loading;
        public bool loaded;
        public bool unloading;
        public bool rebuildRequested;
        public float priority;
        public readonly List<RenderBatch> batches = new List<RenderBatch>();
        public readonly List<GameObject> individualTrees = new List<GameObject>();
    }

    private sealed class CachedChunkData
    {
        public ChunkData data;
        public int estimatedBytes;
        public LinkedListNode<string> node;
    }

#if UNITY_EDITOR
    private sealed class EditorCachedChunkData
    {
        public ChunkData data;
        public int estimatedBytes;
        public LinkedListNode<string> node;
    }
#endif

    private sealed class ChunkPriorityComparer : IComparer<ChunkRuntimeState>
    {
        public int Compare(ChunkRuntimeState a, ChunkRuntimeState b)
        {
            int result = a.priority.CompareTo(b.priority);
            return result != 0 ? result : string.CompareOrdinal(a.chunk.name, b.chunk.name);
        }
    }

    private float streamingTimer;
    private float diagnosticsTimer;
    private float loadDistSq;
    private float currentSpeed;
    private Vector3 lastTargetPosition;
    private bool hasLastTargetPosition;
    private Coroutine streamingRoutine;
    private Camera renderCamera;
    private Rigidbody targetRigidbody;

    private readonly Dictionary<Transform, ChunkRuntimeState> chunkStates =
        new Dictionary<Transform, ChunkRuntimeState>();
    private readonly Dictionary<string, CachedChunkData> parsedDataCache =
        new Dictionary<string, CachedChunkData>();
    private readonly LinkedList<string> parsedDataLru = new LinkedList<string>();
    private readonly List<ChunkRuntimeState> pendingLoads = new List<ChunkRuntimeState>();
    private readonly List<ChunkRuntimeState> activeLoads = new List<ChunkRuntimeState>();
    private readonly List<ChunkRuntimeState> pendingUnloads = new List<ChunkRuntimeState>();
    private readonly List<ChunkRuntimeState> stateBuffer = new List<ChunkRuntimeState>();
    private readonly Plane[] frustumPlanes = new Plane[6];
    private PrefabRenderData[] preparedPrefabData;
    private readonly ChunkPriorityComparer priorityComparer = new ChunkPriorityComparer();
    private int parsedDataBytes;
    private int frameSubmittedInstances;
    private int frameActiveRenderChunks;
    private int frameActiveIndividualTrees;
    private int preparedChunks;
    private int loadedChunks;
    private float lastChunkReadyTime;
    private float lastChunkRequestTime;

#if UNITY_EDITOR
    private readonly Dictionary<string, EditorCachedChunkData> editorDataCache =
        new Dictionary<string, EditorCachedChunkData>();
    private readonly LinkedList<string> editorDataLru = new LinkedList<string>();
    private readonly Matrix4x4[] editorMatrixBuffer = new Matrix4x4[MaxInstancesPerDraw];
    private readonly Dictionary<int, List<Matrix4x4>> editorMatricesByPrefab =
        new Dictionary<int, List<Matrix4x4>>();
    private int editorDataBytes;
    private const int MaxEditorCacheBytes = 8 * 1024 * 1024;
#endif

    private void Awake()
    {
        loadDistSq = GetEffectiveRenderDistance() * GetEffectiveRenderDistance();
        preparedPrefabData = BuildPrefabRenderData();
    }

    private void Start()
    {
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        if (playerTransform != null)
        {
            targetRigidbody = playerTransform.GetComponentInParent<Rigidbody>();
            lastTargetPosition = playerTransform.position;
            hasLastTargetPosition = true;
        }

        foreach (Transform chunk in transform)
        {
            chunkStates[chunk] = new ChunkRuntimeState
            {
                chunk = chunk,
                bounds = new Bounds(
                    chunk.position,
                    new Vector3(chunkSize, chunkSize, chunkSize)
                )
            };
        }

        UpdateChunkRequests(true);
        streamingRoutine = StartCoroutine(ProcessStreaming());
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            return;
        }

        UpdateTargetSpeed();
        streamingTimer -= Time.unscaledDeltaTime;
        if (streamingTimer <= 0f)
        {
            streamingTimer = Mathf.Max(0.02f, streamingUpdateInterval);
            UpdateChunkRequests(false);
        }

        if (enableDiagnostics)
        {
            diagnosticsTimer -= Time.unscaledDeltaTime;
            if (diagnosticsTimer <= 0f)
            {
                diagnosticsTimer = Mathf.Max(0.5f, diagnosticsLogInterval);
                LogDiagnostics();
            }
        }
    }

    private void LateUpdate()
    {
        if (renderCamera == null)
        {
            renderCamera = Camera.main;
        }

        if (renderCamera == null)
        {
            return;
        }

        frameSubmittedInstances = 0;
        frameActiveRenderChunks = 0;
        frameActiveIndividualTrees = 0;
        GeometryUtility.CalculateFrustumPlanes(renderCamera, frustumPlanes);

        foreach (ChunkRuntimeState state in chunkStates.Values)
        {
            if (!state.loaded || !state.wantsToRender)
            {
                continue;
            }

            if (state.individualMode)
            {
                frameActiveIndividualTrees += state.individualTrees.Count;
                continue;
            }

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, state.bounds))
            {
                continue;
            }

            frameActiveRenderChunks++;
            SubmitInstancedBatches(state);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying && chunkStates.Count > 0 && streamingRoutine == null)
        {
            streamingTimer = 0f;
            UpdateChunkRequests(true);
            streamingRoutine = StartCoroutine(ProcessStreaming());
        }
    }

    private float GetEffectiveRenderDistance()
    {
        return renderDistance > 0f ? renderDistance : Mathf.Max(0f, loadDistance);
    }

    private void UpdateTargetSpeed()
    {
        if (targetRigidbody != null)
        {
            currentSpeed = targetRigidbody.linearVelocity.magnitude;
        }
        else if (hasLastTargetPosition && Time.deltaTime > 0f)
        {
            currentSpeed = (playerTransform.position - lastTargetPosition).magnitude /
                           Time.deltaTime;
        }

        lastTargetPosition = playerTransform.position;
        hasLastTargetPosition = true;
    }

    private void UpdateChunkRequests(bool force)
    {
        if (playerTransform == null && !force)
        {
            return;
        }

        Vector3 targetPosition = playerTransform.position;
        Vector3 predictedPosition = targetPosition;
        if (targetRigidbody != null)
        {
            predictedPosition += targetRigidbody.linearVelocity * preloadLookAheadSeconds;
        }
        else if (playerTransform.forward.sqrMagnitude > 0.01f)
        {
            predictedPosition += playerTransform.forward *
                                 currentSpeed * preloadLookAheadSeconds;
        }

        float preloadDistance = Mathf.Max(
            preloadSafetyDistance,
            currentSpeed * Mathf.Max(0f, preloadLookAheadSeconds)
        );
        if (maxPreloadDistance > 0f)
        {
            preloadDistance = Mathf.Min(preloadDistance, maxPreloadDistance);
        }

        float effectiveRenderDistance = GetEffectiveRenderDistance();
        loadDistSq = effectiveRenderDistance * effectiveRenderDistance;
        float loadDistance = effectiveRenderDistance + preloadDistance;
        float loadDistanceSq = loadDistance * loadDistance;
        float nearDistanceSq = nearFieldDistance * nearFieldDistance;
        float speedForPriority = Mathf.Max(currentSpeed, 1f);

        pendingLoads.Clear();
        stateBuffer.Clear();

        foreach (ChunkRuntimeState state in chunkStates.Values)
        {
            float renderSqrDistance = state.bounds.SqrDistance(targetPosition);
            float predictedSqrDistance = state.bounds.SqrDistance(predictedPosition);
            state.wantsToRender = renderSqrDistance <= loadDistSq;
            state.wantsToLoad = predictedSqrDistance <= loadDistanceSq;
            state.wantsIndividual = useIndividualNearField &&
                                    renderSqrDistance <= nearDistanceSq;
            state.priority = Mathf.Sqrt(predictedSqrDistance) / speedForPriority;

            if (state.loaded && state.individualMode != state.wantsIndividual)
            {
                state.rebuildRequested = true;
                QueueUnload(state);
            }

            if (!state.wantsToLoad && (state.loaded || state.loading))
            {
                QueueUnload(state);
            }

            if (state.wantsToLoad && !state.loaded && !state.loading &&
                !state.unloading)
            {
                pendingLoads.Add(state);
            }
        }

        pendingLoads.Sort(priorityComparer);
    }

    private IEnumerator ProcessStreaming()
    {
        while (true)
        {
            int destroyBudget = Mathf.Max(0, maxDestroysPerFrame);
            ProcessUnloads(ref destroyBudget);

            StartPendingLoads();

            int instantiateBudget = Mathf.Max(0, maxInstantiatesPerFrame);
            ProcessActiveLoads(ref instantiateBudget);

            yield return null;
        }
    }

    private void StartPendingLoads()
    {
        int slots = Mathf.Max(1, maxConcurrentChunkLoads);
        for (int i = 0; i < pendingLoads.Count && activeLoads.Count < slots; i++)
        {
            ChunkRuntimeState state = pendingLoads[i];
            if (!state.wantsToLoad || state.loading || state.loaded || state.unloading)
            {
                continue;
            }

            state.loading = true;
            state.nextTreeIndex = 0;
            state.data = GetChunkData(state.chunk.name);
            lastChunkRequestTime = Time.realtimeSinceStartup;

            if (state.data == null)
            {
                state.loading = false;
                state.loaded = true;
                state.wantsToRender = false;
                continue;
            }

            activeLoads.Add(state);
        }
    }

    private void ProcessActiveLoads(ref int instantiateBudget)
    {
        for (int i = 0; i < activeLoads.Count;)
        {
            ChunkRuntimeState state = activeLoads[i];
            if (!state.wantsToLoad)
            {
                CancelLoad(state);
                activeLoads.RemoveAt(i);
                continue;
            }

            bool complete;
            if (state.wantsIndividual)
            {
                complete = BuildIndividualChunk(state, ref instantiateBudget);
            }
            else
            {
                complete = BuildInstancedChunk(state);
            }

            if (!complete)
            {
                i++;
                continue;
            }

            state.loading = false;
            state.loaded = true;
            state.individualMode = state.wantsIndividual;
            preparedChunks++;
            loadedChunks++;
            lastChunkReadyTime = Time.realtimeSinceStartup;
            activeLoads.RemoveAt(i);
        }
    }

    private bool BuildIndividualChunk(ChunkRuntimeState state, ref int instantiateBudget)
    {
        if (state.data.trees == null)
        {
            state.data.trees = new List<TreeData>();
        }

        while (state.nextTreeIndex < state.data.trees.Count && instantiateBudget > 0)
        {
            TreeData treeData = state.data.trees[state.nextTreeIndex++];
            if (treeData.prefabIndex < 0 || treeData.prefabIndex >= treePrefabs.Length ||
                treePrefabs[treeData.prefabIndex] == null)
            {
                continue;
            }

            GameObject treeInstance = Instantiate(
                treePrefabs[treeData.prefabIndex],
                treeData.position,
                treeData.rotation,
                state.chunk
            );
            treeInstance.transform.localScale = treeData.scale;
            state.individualTrees.Add(treeInstance);
            instantiateBudget--;
        }

        return state.nextTreeIndex >= state.data.trees.Count;
    }

    private bool BuildInstancedChunk(ChunkRuntimeState state)
    {
        Profiler.BeginSample("ChunkTreeRuntimeLoader.BuildInstancedChunk");
        ClearBatches(state);

        if (state.data.trees == null || state.data.trees.Count == 0)
        {
            Profiler.EndSample();
            return true;
        }

        int lodIndex = Mathf.Max(0, instancedLodIndex);
        Dictionary<int, List<Matrix4x4>> matricesByPrefab =
            new Dictionary<int, List<Matrix4x4>>();

        foreach (TreeData treeData in state.data.trees)
        {
            if (treeData.prefabIndex < 0 || treeData.prefabIndex >= preparedPrefabData.Length ||
                preparedPrefabData[treeData.prefabIndex] == null)
            {
                continue;
            }

            if (!matricesByPrefab.TryGetValue(treeData.prefabIndex,
                    out List<Matrix4x4> matrices))
            {
                matrices = new List<Matrix4x4>(state.data.trees.Count);
                matricesByPrefab.Add(treeData.prefabIndex, matrices);
            }

            PrefabRenderData prefabData = preparedPrefabData[treeData.prefabIndex];
            Matrix4x4 rootMatrix = Matrix4x4.TRS(
                treeData.position,
                treeData.rotation,
                treeData.scale
            );

            for (int i = 0; i < prefabData.parts.Length; i++)
            {
                if (i == 0)
                {
                    matrices.Add(rootMatrix);
                }
            }
        }

        foreach (KeyValuePair<int, List<Matrix4x4>> entry in matricesByPrefab)
        {
            int prefabIndex = entry.Key;
            List<Matrix4x4> rootMatrices = entry.Value;
            PrefabRenderData prefabData = preparedPrefabData[prefabIndex];
            for (int partIndex = 0; partIndex < prefabData.parts.Length; partIndex++)
            {
                RenderPart part = prefabData.parts[partIndex];
                Matrix4x4[] matrices = new Matrix4x4[rootMatrices.Count];
                for (int i = 0; i < rootMatrices.Count; i++)
                {
                    matrices[i] = rootMatrices[i] * part.localMatrix;
                }

                for (int start = 0; start < matrices.Length; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, matrices.Length - start);
                    Matrix4x4[] drawMatrices = new Matrix4x4[count];
                    Array.Copy(matrices, start, drawMatrices, 0, count);
                    state.batches.Add(new RenderBatch
                    {
                        mesh = part.mesh,
                        material = part.material,
                        submeshIndex = part.submeshIndex,
                        matrices = drawMatrices,
                        bounds = state.bounds
                    });
                }
            }
        }

        Profiler.EndSample();
        return true;
    }

    private void SubmitInstancedBatches(ChunkRuntimeState state)
    {
        for (int i = 0; i < state.batches.Count; i++)
        {
            RenderBatch batch = state.batches[i];
            if (batch.mesh == null || batch.material == null || batch.matrices.Length == 0)
            {
                continue;
            }

            RenderParams renderParams = new RenderParams(batch.material)
            {
                worldBounds = batch.bounds,
                shadowCastingMode = instancedCastShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off,
                receiveShadows = instancedReceiveShadows,
                layer = gameObject.layer,
                camera = null
            };

            Graphics.RenderMeshInstanced(
                in renderParams,
                batch.mesh,
                batch.submeshIndex,
                batch.matrices,
                batch.matrices.Length
            );
            frameSubmittedInstances += batch.matrices.Length;
        }
    }

    private void ProcessUnloads(ref int destroyBudget)
    {
        for (int i = pendingUnloads.Count - 1; i >= 0; i--)
        {
            ChunkRuntimeState state = pendingUnloads[i];
            if (state.loading)
            {
                CancelLoad(state);
                state.loading = false;
                activeLoads.Remove(state);
            }

            while (state.individualTrees.Count > 0 && destroyBudget > 0)
            {
                int last = state.individualTrees.Count - 1;
                GameObject tree = state.individualTrees[last];
                state.individualTrees.RemoveAt(last);
                if (tree != null)
                {
                    Destroy(tree);
                    destroyBudget--;
                }
            }

            if (state.individualTrees.Count > 0)
            {
                continue;
            }

            ClearBatches(state);
            state.loaded = false;
            state.unloading = false;
            state.individualMode = false;
            state.nextTreeIndex = 0;
            state.data = null;
            state.rebuildRequested = false;
            pendingUnloads.RemoveAt(i);

            if (state.wantsToLoad && !state.loading)
            {
                pendingLoads.Add(state);
                pendingLoads.Sort(priorityComparer);
            }
        }
    }

    private void QueueUnload(ChunkRuntimeState state)
    {
        if (state.unloading)
        {
            return;
        }

        state.unloading = true;
        pendingUnloads.Add(state);
    }

    private void CancelLoad(ChunkRuntimeState state)
    {
        ClearBatches(state);
        state.data = null;
        state.nextTreeIndex = 0;
        QueueUnload(state);
    }

    private void ClearBatches(ChunkRuntimeState state)
    {
        state.batches.Clear();
    }

    private ChunkData GetChunkData(string chunkName)
    {
        if (parsedDataCache.TryGetValue(chunkName, out CachedChunkData cached))
        {
            TouchCacheEntry(chunkName, cached);
            return cached.data;
        }

        TextAsset jsonAsset = Resources.Load<TextAsset>($"{jsonResourcesPath}/{chunkName}");
        if (jsonAsset == null)
        {
            Debug.LogWarning($"Chunk tree data not found for '{chunkName}'.", this);
            return null;
        }

        ChunkData data = null;
        try
        {
            Profiler.BeginSample("ChunkTreeRuntimeLoader.JsonParse");
            data = JsonUtility.FromJson<ChunkData>(jsonAsset.text);
            Profiler.EndSample();
        }
        catch (ArgumentException exception)
        {
            Debug.LogError($"Invalid tree data in '{chunkName}': {exception.Message}", this);
        }
        finally
        {
            Resources.UnloadAsset(jsonAsset);
        }

        if (data == null)
        {
            return null;
        }

        int estimatedBytes = EstimateChunkDataBytes(data);
        if (maxCachedDataBytes > 0 && estimatedBytes <= maxCachedDataBytes)
        {
            while (parsedDataBytes + estimatedBytes > maxCachedDataBytes &&
                   parsedDataLru.First != null)
            {
                string oldestName = parsedDataLru.First.Value;
                parsedDataLru.RemoveFirst();
                if (parsedDataCache.TryGetValue(oldestName, out CachedChunkData oldest))
                {
                    parsedDataBytes -= oldest.estimatedBytes;
                    parsedDataCache.Remove(oldestName);
                }
            }

            CachedChunkData entry = new CachedChunkData
            {
                data = data,
                estimatedBytes = estimatedBytes
            };
            entry.node = parsedDataLru.AddLast(chunkName);
            parsedDataCache[chunkName] = entry;
            parsedDataBytes += estimatedBytes;
        }

        return data;
    }

    private static int EstimateChunkDataBytes(ChunkData data)
    {
        int treeCount = data == null || data.trees == null ? 0 : data.trees.Count;
        return 128 + treeCount * 128;
    }

    private void TouchCacheEntry(string chunkName, CachedChunkData entry)
    {
        parsedDataLru.Remove(entry.node);
        entry.node = parsedDataLru.AddLast(chunkName);
    }

    private PrefabRenderData[] BuildPrefabRenderData()
    {
        if (treePrefabs == null)
        {
            return Array.Empty<PrefabRenderData>();
        }

        PrefabRenderData[] result = new PrefabRenderData[treePrefabs.Length];
        int lodIndex = Mathf.Max(0, instancedLodIndex);

        for (int prefabIndex = 0; prefabIndex < treePrefabs.Length; prefabIndex++)
        {
            GameObject prefab = treePrefabs[prefabIndex];
            if (prefab == null)
            {
                continue;
            }

            LODGroup lodGroup = prefab.GetComponent<LODGroup>();
            Renderer[] renderers;
            if (lodGroup != null)
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length == 0)
                {
                    continue;
                }

                renderers = lods[Mathf.Min(lodIndex, lods.Length - 1)].renderers;
            }
            else
            {
                renderers = prefab.GetComponentsInChildren<Renderer>(true);
            }

            List<RenderPart> parts = new List<RenderPart>();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                Matrix4x4 localMatrix = prefab.transform.worldToLocalMatrix *
                                         renderer.transform.localToWorldMatrix;
                for (int submeshIndex = 0; submeshIndex < materials.Length; submeshIndex++)
                {
                    Material material = materials[submeshIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    material.enableInstancing = true;
                    parts.Add(new RenderPart
                    {
                        mesh = meshFilter.sharedMesh,
                        material = material,
                        submeshIndex = submeshIndex,
                        localMatrix = localMatrix
                    });
                }
            }

            result[prefabIndex] = new PrefabRenderData
            {
                parts = parts.ToArray()
            };
        }

        return result;
    }

    private void LogDiagnostics()
    {
        Debug.Log(
            $"Tree streaming: loadedChunks={loadedChunks}, preparedChunks={preparedChunks}, " +
            $"submittedInstances={frameSubmittedInstances}, " +
            $"visibleInstancedChunks={frameActiveRenderChunks}, " +
            $"activeIndividualTrees={frameActiveIndividualTrees}, " +
            $"parsedCacheBytes={parsedDataBytes}/{maxCachedDataBytes}, " +
            $"lastRequestToReady={(lastChunkReadyTime - lastChunkRequestTime):F3}s",
            this
        );
    }

    private void OnDisable()
    {
        if (streamingRoutine != null)
        {
            StopCoroutine(streamingRoutine);
            streamingRoutine = null;
        }

        if (Application.isPlaying)
        {
            ReleaseRuntimeResources();
        }
    }

    private void OnDestroy()
    {
        ReleaseRuntimeResources();

        parsedDataCache.Clear();
        parsedDataLru.Clear();
        parsedDataBytes = 0;

#if UNITY_EDITOR
        editorDataCache.Clear();
        editorDataLru.Clear();
        editorDataBytes = 0;
#endif
    }

    private void ReleaseRuntimeResources()
    {
        pendingLoads.Clear();
        activeLoads.Clear();
        pendingUnloads.Clear();

        foreach (ChunkRuntimeState state in chunkStates.Values)
        {
            for (int i = 0; i < state.individualTrees.Count; i++)
            {
                if (state.individualTrees[i] != null)
                {
                    state.individualTrees[i].SetActive(false);
                    Destroy(state.individualTrees[i]);
                }
            }

            state.individualTrees.Clear();
            state.batches.Clear();
            state.data = null;
            state.nextTreeIndex = 0;
            state.loading = false;
            state.loaded = false;
            state.unloading = false;
            state.individualMode = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showFullMeshInViewport || Application.isPlaying || treePrefabs == null ||
            treePrefabs.Length == 0 || editorPreviewMaxTrees <= 0)
        {
            return;
        }

        Camera sceneCamera = SceneView.lastActiveSceneView == null
            ? null
            : SceneView.lastActiveSceneView.camera;
        Vector3 previewCenter = sceneCamera == null ? transform.position : sceneCamera.transform.position;
        float previewDistanceSq = editorPreviewDistance * editorPreviewDistance;
        int drawnTrees = 0;
        int drawnChunks = 0;
        editorMatricesByPrefab.Clear();

        foreach (Transform chunk in transform)
        {
            if (drawnChunks >= editorPreviewMaxChunks ||
                (chunk.position - previewCenter).sqrMagnitude > previewDistanceSq)
            {
                continue;
            }

            ChunkData data = GetEditorChunkData(chunk.name);
            if (data == null || data.trees == null)
            {
                continue;
            }

            drawnChunks++;
            for (int treeIndex = 0;
                 treeIndex < data.trees.Count && drawnTrees < editorPreviewMaxTrees;
                 treeIndex++)
            {
                TreeData treeData = data.trees[treeIndex];
                if (treeData.prefabIndex < 0 || treeData.prefabIndex >= treePrefabs.Length)
                {
                    continue;
                }

                if (!editorMatricesByPrefab.TryGetValue(
                        treeData.prefabIndex,
                        out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>(MaxInstancesPerDraw);
                    editorMatricesByPrefab.Add(treeData.prefabIndex, matrices);
                }

                matrices.Add(Matrix4x4.TRS(
                    treeData.position,
                    treeData.rotation,
                    treeData.scale
                ));
                drawnTrees++;
            }
        }

        if (preparedPrefabData == null)
        {
            preparedPrefabData = BuildPrefabRenderData();
        }

        foreach (KeyValuePair<int, List<Matrix4x4>> entry in editorMatricesByPrefab)
        {
            if (entry.Key < 0 || entry.Key >= preparedPrefabData.Length ||
                preparedPrefabData[entry.Key] == null)
            {
                continue;
            }

            PrefabRenderData prefabData = preparedPrefabData[entry.Key];
            List<Matrix4x4> rootMatrices = entry.Value;
            for (int partIndex = 0; partIndex < prefabData.parts.Length; partIndex++)
            {
                RenderPart part = prefabData.parts[partIndex];
                if (part.mesh == null || part.material == null)
                {
                    continue;
                }

                part.material.enableInstancing = true;
                for (int start = 0; start < rootMatrices.Count; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, rootMatrices.Count - start);
                    for (int i = 0; i < count; i++)
                    {
                        editorMatrixBuffer[i] =
                            rootMatrices[start + i] * part.localMatrix;
                    }

                    Graphics.DrawMeshInstanced(
                        part.mesh,
                        part.submeshIndex,
                        part.material,
                        editorMatrixBuffer,
                        count
                    );
                }
            }
        }
    }

    private ChunkData GetEditorChunkData(string chunkName)
    {
        if (editorDataCache.TryGetValue(chunkName, out EditorCachedChunkData cached))
        {
            editorDataLru.Remove(cached.node);
            cached.node = editorDataLru.AddLast(chunkName);
            return cached.data;
        }

        TextAsset asset = Resources.Load<TextAsset>($"{jsonResourcesPath}/{chunkName}");
        if (asset == null)
        {
            return null;
        }

        ChunkData data = JsonUtility.FromJson<ChunkData>(asset.text);
        Resources.UnloadAsset(asset);
        if (data == null)
        {
            return null;
        }

        int estimatedBytes = EstimateChunkDataBytes(data);
        while (editorDataBytes + estimatedBytes > MaxEditorCacheBytes &&
               editorDataLru.First != null)
        {
            string oldestName = editorDataLru.First.Value;
            editorDataLru.RemoveFirst();
            if (editorDataCache.TryGetValue(oldestName, out EditorCachedChunkData oldest))
            {
                editorDataBytes -= oldest.estimatedBytes;
                editorDataCache.Remove(oldestName);
            }
        }

        EditorCachedChunkData entry = new EditorCachedChunkData
        {
            data = data,
            estimatedBytes = estimatedBytes
        };
        entry.node = editorDataLru.AddLast(chunkName);
        editorDataCache[chunkName] = entry;
        editorDataBytes += estimatedBytes;
        return data;
    }
#endif
}
