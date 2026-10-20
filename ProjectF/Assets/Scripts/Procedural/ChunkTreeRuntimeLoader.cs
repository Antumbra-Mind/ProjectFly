using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

public readonly struct RawTreeRecord
{
    public readonly float px, py, pz;
    public readonly float qx, qy, qz, qw;
    public readonly float sx, sy, sz;
    public readonly int prefabIndex;

    public RawTreeRecord(
        float px,
        float py,
        float pz,
        float qx,
        float qy,
        float qz,
        float qw,
        float sx,
        float sy,
        float sz,
        int prefabIndex)
    {
        this.px = px;
        this.py = py;
        this.pz = pz;
        this.qx = qx;
        this.qy = qy;
        this.qz = qz;
        this.qw = qw;
        this.sx = sx;
        this.sy = sy;
        this.sz = sz;
        this.prefabIndex = prefabIndex;
    }
}

[BurstCompile]
public struct BuildRootMatricesJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<RawTreeRecord> trees;

    [WriteOnly]
    public NativeArray<float4x4> rootMatrices;

    public void Execute(int index)
    {
        RawTreeRecord tree = trees[index];
        rootMatrices[index] = float4x4.TRS(
            new float3(tree.px, tree.py, tree.pz),
            new quaternion(tree.qx, tree.qy, tree.qz, tree.qw),
            new float3(tree.sx, tree.sy, tree.sz)
        );
    }
}

[BurstCompile]
public struct BuildPartMatricesJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<float4x4> rootMatrices;

    [WriteOnly]
    public NativeArray<float4x4> outputMatrices;

    public float4x4 localPartMatrix;

    public void Execute(int index)
    {
        outputMatrices[index] = math.mul(
            rootMatrices[index],
            localPartMatrix
        );
    }
}

internal static class RawChunkJsonParser
{
    public static RawTreeRecord[] Parse(
        string json,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new FormatException("Tree chunk JSON is empty.");
        }

        int treesProperty = json.IndexOf(
            "\"trees\"",
            StringComparison.Ordinal
        );
        if (treesProperty < 0)
        {
            return Array.Empty<RawTreeRecord>();
        }

        int arrayStart = json.IndexOf('[', treesProperty);
        if (arrayStart < 0)
        {
            throw new FormatException("Tree array is missing.");
        }

        List<RawTreeRecord> records = new List<RawTreeRecord>();
        int cursor = arrayStart + 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkipWhitespaceAndCommas(json, ref cursor);

            if (cursor >= json.Length)
            {
                throw new FormatException("Tree array is not closed.");
            }

            if (json[cursor] == ']')
            {
                break;
            }

            if (json[cursor] != '{')
            {
                throw new FormatException("Tree record must be a JSON object.");
            }

            int objectEnd = FindObjectEnd(json, cursor);
            int positionStart = FindObjectPropertyStart(
                json,
                cursor,
                objectEnd,
                "position"
            );
            int rotationStart = FindObjectPropertyStart(
                json,
                cursor,
                objectEnd,
                "rotation"
            );
            int scaleStart = FindObjectPropertyStart(
                json,
                cursor,
                objectEnd,
                "scale"
            );

            RawTreeRecord record = new RawTreeRecord(
                ReadFloatProperty(json, positionStart, "x"),
                ReadFloatProperty(json, positionStart, "y"),
                ReadFloatProperty(json, positionStart, "z"),
                ReadFloatProperty(json, rotationStart, "x"),
                ReadFloatProperty(json, rotationStart, "y"),
                ReadFloatProperty(json, rotationStart, "z"),
                ReadFloatProperty(json, rotationStart, "w"),
                ReadFloatProperty(json, scaleStart, "x"),
                ReadFloatProperty(json, scaleStart, "y"),
                ReadFloatProperty(json, scaleStart, "z"),
                ReadIntProperty(json, cursor, objectEnd, "prefabIndex")
            );

            records.Add(record);
            cursor = objectEnd + 1;
        }

        return records.ToArray();
    }

    private static void SkipWhitespaceAndCommas(string json, ref int cursor)
    {
        while (cursor < json.Length &&
               (char.IsWhiteSpace(json[cursor]) || json[cursor] == ','))
        {
            cursor++;
        }
    }

    private static int FindObjectEnd(string json, int objectStart)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = objectStart; i < json.Length; i++)
        {
            char character = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
            }
            else if (character == '{')
            {
                depth++;
            }
            else if (character == '}' && --depth == 0)
            {
                return i;
            }
        }

        throw new FormatException("Tree object is not closed.");
    }

    private static int FindObjectPropertyStart(
        string json,
        int objectStart,
        int objectEnd,
        string propertyName)
    {
        int property = json.IndexOf(
            $"\"{propertyName}\"",
            objectStart,
            objectEnd - objectStart,
            StringComparison.Ordinal
        );
        if (property < 0)
        {
            throw new FormatException(
                $"Tree property '{propertyName}' is missing."
            );
        }

        int valueStart = json.IndexOf(':', property);
        if (valueStart < 0 || valueStart >= objectEnd)
        {
            throw new FormatException(
                $"Tree property '{propertyName}' has no value."
            );
        }

        valueStart++;
        while (valueStart < objectEnd &&
               char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= objectEnd || json[valueStart] != '{')
        {
            throw new FormatException(
                $"Tree property '{propertyName}' is not an object."
            );
        }

        return valueStart;
    }

    private static float ReadFloatProperty(
        string json,
        int objectStart,
        string propertyName)
    {
        int valueStart = FindPropertyValueStart(
            json,
            objectStart,
            FindObjectEnd(json, objectStart),
            propertyName
        );
        int valueEnd = FindNumberEnd(json, valueStart);
        return float.Parse(
            json.Substring(valueStart, valueEnd - valueStart),
            NumberStyles.Float,
            CultureInfo.InvariantCulture
        );
    }

    private static int ReadIntProperty(
        string json,
        int objectStart,
        int objectEnd,
        string propertyName)
    {
        int valueStart = FindPropertyValueStart(
            json,
            objectStart,
            objectEnd,
            propertyName
        );
        int valueEnd = FindNumberEnd(json, valueStart);
        return int.Parse(
            json.Substring(valueStart, valueEnd - valueStart),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture
        );
    }

    private static int FindPropertyValueStart(
        string json,
        int objectStart,
        int objectEnd,
        string propertyName)
    {
        int property = json.IndexOf(
            $"\"{propertyName}\"",
            objectStart,
            objectEnd - objectStart,
            StringComparison.Ordinal
        );
        if (property < 0)
        {
            throw new FormatException(
                $"Tree property '{propertyName}' is missing."
            );
        }

        int valueStart = json.IndexOf(':', property);
        if (valueStart < 0 || valueStart >= objectEnd)
        {
            throw new FormatException(
                $"Tree property '{propertyName}' has no value."
            );
        }

        valueStart++;
        while (valueStart < objectEnd &&
               char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        return valueStart;
    }

    private static int FindNumberEnd(string json, int numberStart)
    {
        int cursor = numberStart;
        while (cursor < json.Length)
        {
            char character = json[cursor];
            if (!char.IsDigit(character) &&
                character != '-' &&
                character != '+' &&
                character != '.' &&
                character != 'e' &&
                character != 'E')
            {
                break;
            }

            cursor++;
        }

        if (cursor == numberStart)
        {
            throw new FormatException("Expected a numeric tree value.");
        }

        return cursor;
    }
}

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

    [Header("Stage 3 Evaluation")]
    public bool useSpatialSubChunks = false;

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
    private const int SubChunkGridSize = 4;
    private const int SubChunkCellCount =
        SubChunkGridSize * SubChunkGridSize;

    private static class Stage3Profiler
    {
        public static readonly ProfilerMarker ApplyCompletedMatrices =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.ApplyCompletedMatrixBuilds"
            );
        public static readonly ProfilerMarker ChunkVisibility =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.ChunkVisibility"
            );
        public static readonly ProfilerMarker ChunkVisibilityTest =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.ChunkVisibility.TestPlanesAABB"
            );
        public static readonly ProfilerMarker SubChunkVisibility =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.SubChunkVisibility"
            );
        public static readonly ProfilerMarker SubChunkVisibilityTest =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.SubChunkVisibility.TestPlanesAABB"
            );
        public static readonly ProfilerMarker SubmitBatches =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.SubmitInstancedBatches"
            );
        public static readonly ProfilerMarker SubmitSubChunkBatches =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.SubmitSubChunkBatches"
            );
        public static readonly ProfilerMarker RenderMeshInstanced =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.Graphics.RenderMeshInstanced"
            );
        public static readonly ProfilerMarker BuildSubChunkBatches =
            new ProfilerMarker(
                "ChunkTreeRuntimeLoader.BuildSubChunkBatches"
            );
    }

    private sealed class RenderBatch
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        public Matrix4x4[] matrices;
        public int instanceCount;
        public Bounds bounds;
    }

    private sealed class SubChunkCell
    {
        public Bounds bounds;
        public readonly List<RenderBatch> batches =
            new List<RenderBatch>();
        public int activeBatchCount;
        public int instanceCount;
        public bool hasInstances;
    }

    private sealed class ChunkSubChunkData
    {
        public readonly SubChunkCell[] cells =
            new SubChunkCell[SubChunkCellCount];
        public int activeCellCount;
        public int totalInstanceCount;
        public int totalBatchCount;

        public ChunkSubChunkData()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new SubChunkCell();
            }
        }
    }

    private sealed class RenderPart
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        public Matrix4x4 localMatrix;
        public float4x4 burstLocalMatrix;
    }

    private sealed class PrefabRenderData
    {
        public RenderPart[] parts;
    }

    private sealed class PendingPartMatrixBuild
    {
        public int prefabIndex;
        public RenderPart part;
        public NativeArray<float4x4> outputMatrices;
    }

    private sealed class PendingMatrixBuild
    {
        public JobHandle handle;
        public NativeArray<RawTreeRecord> trees;
        public NativeArray<float4x4> rootMatrices;
        public readonly List<PendingPartMatrixBuild> parts =
            new List<PendingPartMatrixBuild>();
        public int requestVersion;
        public int scheduledFrame;
        public string chunkName;
    }

    private sealed class ChunkRuntimeState
    {
        public Transform chunk;
        public Bounds bounds;
        public ChunkData data;
        public RawTreeRecord[] rawRecords;
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
        public int activeBatchCount;
        public int requestVersion;
        public int parseVersion;
        public Task<RawTreeRecord[]> parseTask;
        public CancellationTokenSource parseCancellation;
        public PendingMatrixBuild pendingMatrixBuild;
        public readonly ChunkSubChunkData subChunks =
            new ChunkSubChunkData();
        public readonly List<RenderBatch> batches = new List<RenderBatch>();
        public readonly List<GameObject> individualTrees = new List<GameObject>();
    }

    private sealed class CachedChunkData
    {
        public ChunkData data;
        public RawTreeRecord[] rawRecords;
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
    private List<Matrix4x4>[] instancedRootMatricesByPrefab;
    private readonly List<int> activeInstancedPrefabIndices = new List<int>();
    private readonly ChunkPriorityComparer priorityComparer = new ChunkPriorityComparer();
    private int parsedDataBytes;
    private int frameSubmittedInstances;
    private int frameSubmittedDrawCalls;
    private int frameActiveRenderChunks;
    private int frameActiveSubChunkCells;
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
        instancedRootMatricesByPrefab = new List<Matrix4x4>[
            preparedPrefabData == null ? 0 : preparedPrefabData.Length
        ];
    }

    private void Start()
    {
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        if (renderCamera == null)
        {
            renderCamera = Camera.main;
        }

        if (renderCamera == null && playerTransform != null)
        {
            renderCamera = playerTransform.GetComponentInChildren<Camera>(true);
        }

        if (playerTransform != null)
        {
            targetRigidbody = playerTransform.GetComponentInParent<Rigidbody>();
            lastTargetPosition = playerTransform.position;
            hasLastTargetPosition = true;
        }

        foreach (Transform chunk in transform)
        {
            Bounds chunkBounds = new Bounds(
                chunk.position,
                new Vector3(chunkSize, chunkSize, chunkSize)
            );
            ChunkRuntimeState state = new ChunkRuntimeState
            {
                chunk = chunk,
                bounds = chunkBounds
            };
            InitializeSubChunkBounds(state.subChunks, chunkBounds);
            chunkStates[chunk] = state;
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

    public void RefreshRuntimeSettings(bool rebuildInstancedBatches)
    {
        loadDistSq = GetEffectiveRenderDistance() *
                     GetEffectiveRenderDistance();
        UpdateChunkRequests(true);

        if (rebuildInstancedBatches)
        {
            preparedPrefabData = BuildPrefabRenderData();
            instancedRootMatricesByPrefab = new List<Matrix4x4>[
                preparedPrefabData == null ? 0 : preparedPrefabData.Length
            ];

            for (int i = 0; i < activeLoads.Count; i++)
            {
                ChunkRuntimeState activeState = activeLoads[i];
                if (activeState.pendingMatrixBuild == null)
                {
                    continue;
                }

                DisposePendingMatrixBuild(activeState);
                activeState.requestVersion++;
                if (!activeState.wantsIndividual &&
                    activeState.rawRecords != null)
                {
                    ScheduleMatrixBuild(activeState);
                }
            }

            foreach (ChunkRuntimeState state in chunkStates.Values)
            {
                if (!state.loaded || state.individualMode ||
                    state.unloading || state.rawRecords == null ||
                    !state.wantsToLoad)
                {
                    continue;
                }

                ClearBatches(state);
                ClearSubChunkBatches(state.subChunks);
                state.requestVersion++;
                state.loading = true;
                ScheduleMatrixBuild(state);
                activeLoads.Add(state);
            }
        }
    }

    private void LateUpdate()
    {
        if (renderCamera == null)
        {
            renderCamera = Camera.main;
        }

        if (renderCamera == null && playerTransform != null)
        {
            renderCamera = playerTransform.GetComponentInChildren<Camera>(true);
        }

        if (renderCamera == null)
        {
            return;
        }

        frameSubmittedInstances = 0;
        frameSubmittedDrawCalls = 0;
        frameActiveRenderChunks = 0;
        frameActiveSubChunkCells = 0;
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

            bool chunkVisible;
            using (Stage3Profiler.ChunkVisibility.Auto())
            {
                using (Stage3Profiler.ChunkVisibilityTest.Auto())
                {
                    chunkVisible = GeometryUtility.TestPlanesAABB(
                        frustumPlanes,
                        state.bounds
                    );
                }
            }

            if (!chunkVisible)
            {
                continue;
            }

            frameActiveRenderChunks++;
            if (useSpatialSubChunks)
            {
                SubmitSubChunkBatches(state);
            }
            else
            {
                SubmitInstancedBatches(state);
            }
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
        Profiler.BeginSample("ChunkTreeRuntimeLoader.UpdateChunkRequests");
        try
        {
            UpdateChunkRequestsInternal(force);
        }
        finally
        {
            Profiler.EndSample();
        }
    }

    private void UpdateChunkRequestsInternal(bool force)
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
            Profiler.BeginSample("ChunkTreeRuntimeLoader.ProcessUnloads");
            try
            {
                ProcessUnloads(ref destroyBudget);
            }
            finally
            {
                Profiler.EndSample();
            }

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
            state.data = null;
            lastChunkRequestTime = Time.realtimeSinceStartup;

            if (TryGetCachedChunkData(state.chunk.name, out ChunkData cachedData))
            {
                state.data = cachedData;
                state.rawRecords = parsedDataCache[state.chunk.name].rawRecords;
                if (!state.wantsIndividual)
                {
                    ScheduleMatrixBuild(state);
                }
            }
            else if (!BeginAsyncChunkParse(state))
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

            if (state.parseTask != null)
            {
                if (!state.parseTask.IsCompleted)
                {
                    i++;
                    continue;
                }

                if (!CompleteAsyncChunkParse(state))
                {
                    state.loading = false;
                    state.loaded = true;
                    state.wantsToRender = false;
                    activeLoads.RemoveAt(i);
                    continue;
                }
            }

            if (state.wantsIndividual)
            {
                DisposePendingMatrixBuild(state);
                if (!BuildIndividualChunk(state, ref instantiateBudget))
                {
                    i++;
                    continue;
                }
            }
            else
            {
                if (state.pendingMatrixBuild == null)
                {
                    ScheduleMatrixBuild(state);
                    i++;
                    continue;
                }

                if (state.pendingMatrixBuild.scheduledFrame >= Time.frameCount)
                {
                    i++;
                    continue;
                }

                if (!state.pendingMatrixBuild.handle.IsCompleted)
                {
                    i++;
                    continue;
                }

                PendingMatrixBuild pending = state.pendingMatrixBuild;
                if (pending.requestVersion != state.requestVersion ||
                    !state.wantsToLoad)
                {
                    DisposePendingMatrixBuild(state);
                    state.loading = false;
                    state.loaded = false;
                    activeLoads.RemoveAt(i);
                    continue;
                }

                if (!ApplyCompletedMatrixBuilds(state))
                {
                    state.loading = false;
                    state.loaded = true;
                    state.wantsToRender = false;
                    activeLoads.RemoveAt(i);
                    continue;
                }
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

    private void ScheduleMatrixBuild(ChunkRuntimeState state)
    {
        if (state.pendingMatrixBuild != null)
        {
            return;
        }

        RawTreeRecord[] records = state.rawRecords;
        if (records == null)
        {
            records = ConvertTreeDataToRawRecords(state.data);
            state.rawRecords = records;
        }

        int treeCount = records == null ? 0 : records.Length;
        PendingMatrixBuild pending = new PendingMatrixBuild
        {
            requestVersion = state.requestVersion,
            scheduledFrame = Time.frameCount,
            chunkName = state.chunk.name
        };
        try
        {
            pending.trees = new NativeArray<RawTreeRecord>(
                treeCount,
                Allocator.Persistent
            );
            pending.rootMatrices = new NativeArray<float4x4>(
                treeCount,
                Allocator.Persistent
            );
            for (int i = 0; i < treeCount; i++)
            {
                pending.trees[i] = records[i];
            }

            BuildRootMatricesJob rootJob = new BuildRootMatricesJob
            {
                trees = pending.trees,
                rootMatrices = pending.rootMatrices
            };
            JobHandle rootHandle = default;
            JobHandle combinedHandle = default;
            if (treeCount > 0)
            {
                rootHandle = rootJob.Schedule(treeCount, 64);
                combinedHandle = rootHandle;
            }

            if (treeCount > 0 && preparedPrefabData != null)
            {
                for (int prefabIndex = 0;
                     prefabIndex < preparedPrefabData.Length;
                     prefabIndex++)
                {
                    PrefabRenderData prefabData = preparedPrefabData[prefabIndex];
                    if (prefabData == null || prefabData.parts == null ||
                        !ContainsPrefab(records, prefabIndex))
                    {
                        continue;
                    }

                    for (int partIndex = 0;
                         partIndex < prefabData.parts.Length;
                         partIndex++)
                    {
                        PendingPartMatrixBuild partBuild =
                            new PendingPartMatrixBuild
                            {
                                prefabIndex = prefabIndex,
                                part = prefabData.parts[partIndex],
                                outputMatrices = new NativeArray<float4x4>(
                                    treeCount,
                                    Allocator.Persistent
                                )
                            };
                        pending.parts.Add(partBuild);

                        BuildPartMatricesJob partJob =
                            new BuildPartMatricesJob
                            {
                                rootMatrices = pending.rootMatrices,
                                outputMatrices = partBuild.outputMatrices,
                                localPartMatrix =
                                    partBuild.part.burstLocalMatrix
                            };
                        JobHandle partHandle = partJob.Schedule(
                            treeCount,
                            64,
                            rootHandle
                        );
                        combinedHandle = JobHandle.CombineDependencies(
                            combinedHandle,
                            partHandle
                        );
                    }
                }
            }

            pending.handle = combinedHandle;
            state.pendingMatrixBuild = pending;
        }
        catch
        {
            pending.handle.Complete();
            DisposePendingMatrixBuild(pending);
            throw;
        }
    }

    private bool ApplyCompletedMatrixBuilds(ChunkRuntimeState state)
    {
        PendingMatrixBuild pending = state.pendingMatrixBuild;
        state.pendingMatrixBuild = null;

        try
        {
            pending.handle.Complete();
            if (pending.requestVersion != state.requestVersion ||
                !state.wantsToLoad ||
                !string.Equals(
                    pending.chunkName,
                    state.chunk.name,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            using (Stage3Profiler.ApplyCompletedMatrices.Auto())
            {
                BuildManagedInstancedBatches(state, pending);
                if (useSpatialSubChunks)
                {
                    using (Stage3Profiler.BuildSubChunkBatches.Auto())
                    {
                        BuildSubChunkBatches(state, pending);
                    }
                }
                else
                {
                    ClearSubChunkBatches(state.subChunks);
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to apply matrix build for '{state.chunk.name}': " +
                $"{exception.Message}",
                this
            );
            return false;
        }
        finally
        {
            DisposePendingMatrixBuild(pending);
        }
    }

    private void BuildManagedInstancedBatches(
        ChunkRuntimeState state,
        PendingMatrixBuild pending)
    {
        ClearBatches(state);
        for (int i = 0; i < instancedRootMatricesByPrefab.Length; i++)
        {
            if (instancedRootMatricesByPrefab[i] != null)
            {
                instancedRootMatricesByPrefab[i].Clear();
            }
        }
        activeInstancedPrefabIndices.Clear();

        for (int i = 0; i < pending.trees.Length; i++)
        {
            int prefabIndex = pending.trees[i].prefabIndex;
            if (prefabIndex < 0 ||
                prefabIndex >= preparedPrefabData.Length ||
                preparedPrefabData[prefabIndex] == null)
            {
                continue;
            }

            List<Matrix4x4> rootMatrices =
                instancedRootMatricesByPrefab[prefabIndex];
            if (rootMatrices == null)
            {
                rootMatrices = new List<Matrix4x4>(
                    Mathf.Min(pending.trees.Length, MaxInstancesPerDraw)
                );
                instancedRootMatricesByPrefab[prefabIndex] = rootMatrices;
            }

            if (rootMatrices.Count == 0)
            {
                activeInstancedPrefabIndices.Add(prefabIndex);
            }

            rootMatrices.Add(ToUnityMatrix(pending.rootMatrices[i]));
        }

        int batchIndex = 0;
        for (int partIndex = 0; partIndex < pending.parts.Count; partIndex++)
        {
            PendingPartMatrixBuild partBuild = pending.parts[partIndex];
            int prefabIndex = partBuild.prefabIndex;
            int matchingCount = CountPrefab(pending.trees, prefabIndex);
            if (matchingCount == 0)
            {
                continue;
            }

            int written = 0;
            int start = 0;
            while (written < matchingCount)
            {
                int count = Mathf.Min(
                    MaxInstancesPerDraw,
                    matchingCount - written
                );
                RenderBatch batch;
                if (batchIndex < state.batches.Count)
                {
                    batch = state.batches[batchIndex];
                }
                else
                {
                    batch = new RenderBatch();
                    state.batches.Add(batch);
                }

                if (batch.matrices == null || batch.matrices.Length < count)
                {
                    batch.matrices = new Matrix4x4[count];
                }

                int batchMatrixIndex = 0;
                for (; start < pending.trees.Length &&
                       batchMatrixIndex < count;
                     start++)
                {
                    if (pending.trees[start].prefabIndex != prefabIndex)
                    {
                        continue;
                    }

                    batch.matrices[batchMatrixIndex++] =
                        ToUnityMatrix(partBuild.outputMatrices[start]);
                }

                batch.mesh = partBuild.part.mesh;
                batch.material = partBuild.part.material;
                batch.submeshIndex = partBuild.part.submeshIndex;
                batch.instanceCount = batchMatrixIndex;
                batch.bounds = state.bounds;
                batchIndex++;
                written += batchMatrixIndex;
            }
        }

        state.activeBatchCount = batchIndex;
    }

    private void BuildSubChunkBatches(
        ChunkRuntimeState state,
        PendingMatrixBuild pending)
    {
        ChunkSubChunkData subChunks = state.subChunks;
        ClearSubChunkBatches(subChunks);

        if (pending.trees.Length == 0)
        {
            return;
        }

        float cellSize = state.bounds.size.x / SubChunkGridSize;
        for (int cellIndex = 0;
             cellIndex < subChunks.cells.Length;
             cellIndex++)
        {
            SubChunkCell cell = subChunks.cells[cellIndex];
            for (int partIndex = 0;
                 partIndex < pending.parts.Count;
                 partIndex++)
            {
                PendingPartMatrixBuild partBuild = pending.parts[partIndex];
                int matchingCount = CountCellPrefab(
                    pending.trees,
                    state.bounds,
                    cellSize,
                    cellIndex,
                    partBuild.prefabIndex
                );
                int written = 0;

                while (written < matchingCount)
                {
                    int count = Mathf.Min(
                        MaxInstancesPerDraw,
                        matchingCount - written
                    );
                    RenderBatch batch = GetReusableBatch(
                        cell.batches,
                        cell.activeBatchCount
                    );
                    if (batch.matrices == null ||
                        batch.matrices.Length < count)
                    {
                        batch.matrices = new Matrix4x4[count];
                    }

                    int matchedBeforeBatch = written;
                    int matrixIndex = 0;
                    for (int treeIndex = 0;
                         treeIndex < pending.trees.Length &&
                         matrixIndex < count;
                         treeIndex++)
                    {
                        RawTreeRecord tree = pending.trees[treeIndex];
                        if (tree.prefabIndex != partBuild.prefabIndex ||
                            GetSubChunkCellIndex(
                                new Vector3(tree.px, tree.py, tree.pz),
                                state.bounds,
                                cellSize
                            ) != cellIndex)
                        {
                            continue;
                        }

                        if (matchedBeforeBatch > 0)
                        {
                            matchedBeforeBatch--;
                            continue;
                        }

                        batch.matrices[matrixIndex++] =
                            ToUnityMatrix(
                                partBuild.outputMatrices[treeIndex]
                            );
                    }

                    batch.mesh = partBuild.part.mesh;
                    batch.material = partBuild.part.material;
                    batch.submeshIndex = partBuild.part.submeshIndex;
                    batch.instanceCount = matrixIndex;
                    batch.bounds = cell.bounds;
                    cell.activeBatchCount++;
                    cell.instanceCount += matrixIndex;
                    subChunks.totalBatchCount++;
                    subChunks.totalInstanceCount += matrixIndex;
                    written += matrixIndex;
                }
            }

            cell.hasInstances = cell.activeBatchCount > 0;
            if (cell.hasInstances)
            {
                subChunks.activeCellCount++;
            }
        }
    }

    private static RenderBatch GetReusableBatch(
        List<RenderBatch> batches,
        int batchIndex)
    {
        if (batchIndex < batches.Count)
        {
            return batches[batchIndex];
        }

        RenderBatch batch = new RenderBatch();
        batches.Add(batch);
        return batch;
    }

    private static int CountCellPrefab(
        NativeArray<RawTreeRecord> trees,
        Bounds chunkBounds,
        float cellSize,
        int cellIndex,
        int prefabIndex)
    {
        int count = 0;
        for (int i = 0; i < trees.Length; i++)
        {
            RawTreeRecord tree = trees[i];
            if (tree.prefabIndex == prefabIndex &&
                GetSubChunkCellIndex(
                    new Vector3(tree.px, tree.py, tree.pz),
                    chunkBounds,
                    cellSize
                ) == cellIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetSubChunkCellIndex(
        Vector3 worldPosition,
        Bounds chunkBounds,
        float cellSize)
    {
        Vector3 local = worldPosition - chunkBounds.min;
        int x = Mathf.Clamp(
            Mathf.FloorToInt(local.x / cellSize),
            0,
            SubChunkGridSize - 1
        );
        int z = Mathf.Clamp(
            Mathf.FloorToInt(local.z / cellSize),
            0,
            SubChunkGridSize - 1
        );
        return z * SubChunkGridSize + x;
    }

    private static void InitializeSubChunkBounds(
        ChunkSubChunkData subChunks,
        Bounds chunkBounds)
    {
        Vector3 cellSize = new Vector3(
            chunkBounds.size.x / SubChunkGridSize,
            chunkBounds.size.y,
            chunkBounds.size.z / SubChunkGridSize
        );

        for (int z = 0; z < SubChunkGridSize; z++)
        {
            for (int x = 0; x < SubChunkGridSize; x++)
            {
                int cellIndex = z * SubChunkGridSize + x;
                Vector3 center = chunkBounds.min + new Vector3(
                    (x + 0.5f) * cellSize.x,
                    chunkBounds.size.y * 0.5f,
                    (z + 0.5f) * cellSize.z
                );
                subChunks.cells[cellIndex].bounds =
                    new Bounds(center, cellSize);
            }
        }
    }

    private static void ClearSubChunkBatches(
        ChunkSubChunkData subChunks)
    {
        subChunks.activeCellCount = 0;
        subChunks.totalInstanceCount = 0;
        subChunks.totalBatchCount = 0;
        for (int i = 0; i < subChunks.cells.Length; i++)
        {
            SubChunkCell cell = subChunks.cells[i];
            cell.activeBatchCount = 0;
            cell.instanceCount = 0;
            cell.hasInstances = false;
        }
    }

    private static bool ContainsPrefab(
        RawTreeRecord[] records,
        int prefabIndex)
    {
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].prefabIndex == prefabIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountPrefab(
        NativeArray<RawTreeRecord> records,
        int prefabIndex)
    {
        int count = 0;
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].prefabIndex == prefabIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static RawTreeRecord[] ConvertTreeDataToRawRecords(
        ChunkData data)
    {
        if (data == null || data.trees == null)
        {
            return Array.Empty<RawTreeRecord>();
        }

        RawTreeRecord[] records = new RawTreeRecord[data.trees.Count];
        for (int i = 0; i < data.trees.Count; i++)
        {
            TreeData tree = data.trees[i];
            records[i] = new RawTreeRecord(
                tree.position.x,
                tree.position.y,
                tree.position.z,
                tree.rotation.x,
                tree.rotation.y,
                tree.rotation.z,
                tree.rotation.w,
                tree.scale.x,
                tree.scale.y,
                tree.scale.z,
                tree.prefabIndex
            );
        }

        return records;
    }

    private static Matrix4x4 ToUnityMatrix(float4x4 matrix)
    {
        return new Matrix4x4(
            new Vector4(matrix.c0.x, matrix.c0.y, matrix.c0.z, matrix.c0.w),
            new Vector4(matrix.c1.x, matrix.c1.y, matrix.c1.z, matrix.c1.w),
            new Vector4(matrix.c2.x, matrix.c2.y, matrix.c2.z, matrix.c2.w),
            new Vector4(matrix.c3.x, matrix.c3.y, matrix.c3.z, matrix.c3.w)
        );
    }

    private void SubmitInstancedBatches(ChunkRuntimeState state)
    {
        SubmitBatches(state.batches, state.activeBatchCount);
    }

    private void SubmitSubChunkBatches(ChunkRuntimeState state)
    {
        using (Stage3Profiler.SubmitSubChunkBatches.Auto())
        {
            ChunkSubChunkData subChunks = state.subChunks;
            for (int cellIndex = 0;
                 cellIndex < subChunks.cells.Length;
                 cellIndex++)
            {
                SubChunkCell cell = subChunks.cells[cellIndex];
                if (!cell.hasInstances)
                {
                    continue;
                }

                using (Stage3Profiler.SubChunkVisibility.Auto())
                {
                    bool visible;
                    using (Stage3Profiler.SubChunkVisibilityTest.Auto())
                    {
                        visible = GeometryUtility.TestPlanesAABB(
                            frustumPlanes,
                            cell.bounds
                        );
                    }

                    if (!visible)
                    {
                        continue;
                    }
                }

                frameActiveSubChunkCells++;
                SubmitBatches(cell.batches, cell.activeBatchCount);
            }
        }
    }

    private void SubmitBatches(
        List<RenderBatch> batches,
        int activeBatchCount)
    {
        using (Stage3Profiler.SubmitBatches.Auto())
        {
            for (int i = 0; i < activeBatchCount; i++)
            {
                RenderBatch batch = batches[i];
                if (batch.mesh == null || batch.material == null ||
                    batch.matrices == null || batch.instanceCount == 0)
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
                    camera = renderCamera
                };

                using (Stage3Profiler.RenderMeshInstanced.Auto())
                {
                    Graphics.RenderMeshInstanced(
                        in renderParams,
                        batch.mesh,
                        batch.submeshIndex,
                        batch.matrices,
                        batch.instanceCount
                    );
                }

                frameSubmittedInstances += batch.instanceCount;
                frameSubmittedDrawCalls++;
            }
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
            ClearSubChunkBatches(state.subChunks);
            state.loaded = false;
            state.unloading = false;
            state.individualMode = false;
            state.nextTreeIndex = 0;
            state.data = null;
            state.rawRecords = null;
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
        CancelAsyncChunkParse(state);
        DisposePendingMatrixBuild(state);
        state.requestVersion++;
        ClearBatches(state);
        ClearSubChunkBatches(state.subChunks);
        state.data = null;
        state.rawRecords = null;
        state.nextTreeIndex = 0;
        QueueUnload(state);
    }

    private void ClearBatches(ChunkRuntimeState state)
    {
        state.activeBatchCount = 0;
    }

    private bool TryGetCachedChunkData(
        string chunkName,
        out ChunkData data)
    {
        if (parsedDataCache.TryGetValue(chunkName, out CachedChunkData cached))
        {
            TouchCacheEntry(chunkName, cached);
            data = cached.data;
            return true;
        }

        data = null;
        return false;
    }

    private bool BeginAsyncChunkParse(ChunkRuntimeState state)
    {
        string chunkName = state.chunk.name;
        Profiler.BeginSample("ChunkTreeRuntimeLoader.ResourcesLoad");
        TextAsset jsonAsset;
        try
        {
            jsonAsset = Resources.Load<TextAsset>(
                $"{jsonResourcesPath}/{chunkName}"
            );
        }
        finally
        {
            Profiler.EndSample();
        }

        if (jsonAsset == null)
        {
            Debug.LogWarning($"Chunk tree data not found for '{chunkName}'.", this);
            return false;
        }

        string jsonText = jsonAsset.text;
        Resources.UnloadAsset(jsonAsset);

        state.requestVersion++;
        state.parseVersion = state.requestVersion;
        state.parseCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = state.parseCancellation.Token;
        state.parseTask = Task.Run(
            () => RawChunkJsonParser.Parse(jsonText, cancellationToken),
            cancellationToken
        );
        return true;
    }

    private bool CompleteAsyncChunkParse(ChunkRuntimeState state)
    {
        Task<RawTreeRecord[]> parseTask = state.parseTask;
        state.parseTask = null;

        CancellationTokenSource cancellation = state.parseCancellation;
        state.parseCancellation = null;

        try
        {
            if (state.parseVersion != state.requestVersion ||
                !state.wantsToLoad ||
                parseTask == null ||
                parseTask.IsCanceled)
            {
                return false;
            }

            RawTreeRecord[] records = parseTask.GetAwaiter().GetResult();
            Profiler.BeginSample("ChunkTreeRuntimeLoader.ApplyParsedChunk");
            try
            {
                ChunkData data = ConvertRawChunkData(
                    state.chunk.name,
                    records
                );
                CacheChunkData(state.chunk.name, data, records);
                state.data = data;
                state.rawRecords = records;
                if (!state.wantsIndividual)
                {
                    ScheduleMatrixBuild(state);
                }
            }
            finally
            {
                Profiler.EndSample();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Invalid tree data in '{state.chunk.name}': " +
                $"{exception.Message}",
                this
            );
            return false;
        }
        finally
        {
            if (cancellation != null)
            {
                cancellation.Dispose();
            }
        }
    }

    private static ChunkData ConvertRawChunkData(
        string chunkName,
        RawTreeRecord[] records)
    {
        ChunkData data = new ChunkData
        {
            chunkName = chunkName,
            trees = new List<TreeData>(records.Length)
        };

        for (int i = 0; i < records.Length; i++)
        {
            RawTreeRecord record = records[i];
            data.trees.Add(new TreeData
            {
                position = new Vector3(record.px, record.py, record.pz),
                rotation = new Quaternion(
                    record.qx,
                    record.qy,
                    record.qz,
                    record.qw
                ),
                scale = new Vector3(record.sx, record.sy, record.sz),
                prefabIndex = record.prefabIndex
            });
        }

        return data;
    }

    private void CacheChunkData(
        string chunkName,
        ChunkData data,
        RawTreeRecord[] rawRecords)
    {
        if (data == null)
        {
            return;
        }

        int estimatedBytes = EstimateChunkDataBytes(data);
        if (maxCachedDataBytes <= 0 || estimatedBytes > maxCachedDataBytes)
        {
            return;
        }

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
            rawRecords = rawRecords,
            estimatedBytes = estimatedBytes
        };
        entry.node = parsedDataLru.AddLast(chunkName);
        parsedDataCache[chunkName] = entry;
        parsedDataBytes += estimatedBytes;
    }

    private static void CancelAsyncChunkParse(ChunkRuntimeState state)
    {
        if (state.parseCancellation != null)
        {
            state.parseCancellation.Cancel();
            state.parseCancellation.Dispose();
            state.parseCancellation = null;
        }

        state.parseTask = null;
    }

    private static void DisposePendingMatrixBuild(ChunkRuntimeState state)
    {
        if (state.pendingMatrixBuild == null)
        {
            return;
        }

        PendingMatrixBuild pending = state.pendingMatrixBuild;
        state.pendingMatrixBuild = null;
        pending.handle.Complete();
        DisposePendingMatrixBuild(pending);
    }

    private static void DisposePendingMatrixBuild(PendingMatrixBuild pending)
    {
        if (pending == null)
        {
            return;
        }

        if (pending.trees.IsCreated)
        {
            pending.trees.Dispose();
        }

        if (pending.rootMatrices.IsCreated)
        {
            pending.rootMatrices.Dispose();
        }

        for (int i = 0; i < pending.parts.Count; i++)
        {
            NativeArray<float4x4> outputMatrices =
                pending.parts[i].outputMatrices;
            if (outputMatrices.IsCreated)
            {
                outputMatrices.Dispose();
            }
        }

        pending.parts.Clear();
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
                        localMatrix = localMatrix,
                        burstLocalMatrix = ToBurstMatrix(localMatrix)
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

    private static float4x4 ToBurstMatrix(Matrix4x4 matrix)
    {
        return new float4x4(
            new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
            new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
            new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
            new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33)
        );
    }

    private void LogDiagnostics()
    {
        Debug.Log(
            $"Tree streaming: loadedChunks={loadedChunks}, preparedChunks={preparedChunks}, " +
            $"submittedInstances={frameSubmittedInstances}, " +
            $"submittedDrawCalls={frameSubmittedDrawCalls}, " +
            $"visibleInstancedChunks={frameActiveRenderChunks}, " +
            $"visibleSubChunkCells={frameActiveSubChunkCells}, " +
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
            CancelAsyncChunkParse(state);
            DisposePendingMatrixBuild(state);
            state.requestVersion++;

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
            ClearSubChunkBatches(state.subChunks);
            state.data = null;
            state.rawRecords = null;
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
