
using UnityEngine;

public sealed class RuntimeTreeSettingsOverlay : MonoBehaviour
{
    private const float WindowWidth = 380f;
    private const float WindowHeight = 520f;
    private const float WindowMargin = 10f;
    private const float ContentPadding = 8f;

    private int windowId = 12345;
    private Rect windowRect;
    private bool visible;
    private Vector2 scrollPosition;

    private ChunkTreeRuntimeLoader loader;

    private bool snapshotInitialized;
    private bool previousUseIndividualNearField;
    private bool previousUseSpatialSubChunks;
    private int previousInstancedLodIndex;
    private bool previousInstancedCastShadows;
    private bool previousInstancedReceiveShadows;

    private GUIStyle boldLabelStyle;

    [Header("Overlay")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool visibleOnStart;

    private GUIStyle BoldLabelStyle
    {
        get
        {
            if (boldLabelStyle == null)
            {
                boldLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
            }

            return boldLabelStyle;
        }
    }

    private void Awake()
    {
        visible = visibleOnStart;

        windowRect = new Rect(
            20f,
            20f,
            WindowWidth,
            WindowHeight
        );
    }

    private void Start()
    {
        FindLoader();
        CaptureSnapshot();

        if (loader == null)
        {
            Debug.LogError(
                "[RuntimeTreeSettingsOverlay] " +
                "ChunkTreeRuntimeLoader was not found.",
                this
            );
        }
    }

    private void FindLoader()
    {
        if (loader != null)
            return;

        loader = FindAnyObjectByType<ChunkTreeRuntimeLoader>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) ||
            Input.GetKeyDown(KeyCode.BackQuote))
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !visible)
            return;

        FindLoader();
        EnsureWindowRect();

        windowRect = GUI.Window(
            windowId,
            windowRect,
            DrawWindow,
            "Runtime Tree Settings"
        );

        EnsureWindowRect();
    }

    private void DrawWindow(int id)
    {
        // Розміри внутрішньої області завжди відповідають вікну.
        Rect contentRect = new Rect(
            ContentPadding,
            24f,
            Mathf.Max(100f, windowRect.width - ContentPadding * 2f),
            Mathf.Max(100f, windowRect.height - 32f)
        );

        GUILayout.BeginArea(contentRect);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        if (loader == null)
        {
            GUILayout.Label(
                "ChunkTreeRuntimeLoader not found!"
            );

            GUILayout.Label(
                "Assign the loader or enable its GameObject."
            );

            if (GUILayout.Button("Retry loader search"))
            {
                FindLoader();
                CaptureSnapshot();
            }

            if (GUILayout.Button("Close"))
                visible = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.DragWindow(
                new Rect(0f, 0f, 10000f, 22f)
            );

            return;
        }

        DrawStreamingSettings();
        DrawRenderingSettings();
        DrawEvaluationSettings();
        DrawDiagnosticsSettings();

        GUILayout.Space(10f);

        if (GUILayout.Button("Refresh active chunk batches"))
        {
            loader.RefreshRuntimeSettings(true);
            CaptureSnapshot();
        }

        if (GUILayout.Button("Close (F1)"))
        {
            visible = false;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUI.DragWindow(
            new Rect(0f, 0f, 10000f, 22f)
        );

        ApplyChangedSettings();
    }

    private void DrawStreamingSettings()
    {
        GUILayout.Label("Streaming", BoldLabelStyle);

        loader.renderDistance = SliderWithValue(
            "Render distance",
            loader.renderDistance,
            1000f,
            10000f,
            "F0"
        );

        loader.preloadSafetyDistance = SliderWithValue(
            "Preload safety distance",
            loader.preloadSafetyDistance,
            100f,
            2000f,
            "F0"
        );

        loader.maxConcurrentChunkLoads = IntSliderWithValue(
            "Concurrent chunk loads",
            loader.maxConcurrentChunkLoads,
            1,
            8
        );

        loader.streamingUpdateInterval = SliderWithValue(
            "Streaming update interval",
            loader.streamingUpdateInterval,
            0.02f,
            1f,
            "F2"
        );

        GUILayout.Space(8f);
    }

    private void DrawRenderingSettings()
    {
        GUILayout.Label("Rendering", BoldLabelStyle);

        loader.useIndividualNearField = GUILayout.Toggle(
            loader.useIndividualNearField,
            "Use individual near-field trees"
        );

        loader.nearFieldDistance = SliderWithValue(
            "Near-field distance",
            loader.nearFieldDistance,
            50f,
            1000f,
            "F0"
        );

        loader.instancedLodIndex = IntSliderWithValue(
            "Instanced LOD index",
            loader.instancedLodIndex,
            0,
            2
        );

        loader.instancedCastShadows = GUILayout.Toggle(
            loader.instancedCastShadows,
            "Instanced cast shadows"
        );

        loader.instancedReceiveShadows = GUILayout.Toggle(
            loader.instancedReceiveShadows,
            "Instanced receive shadows"
        );

        GUILayout.Space(8f);
    }

    private void DrawEvaluationSettings()
    {
        GUILayout.Label("Stage 3 Evaluation", BoldLabelStyle);

        loader.useSpatialSubChunks = GUILayout.Toggle(
            loader.useSpatialSubChunks,
            "Use 4x4 spatial sub-chunks"
        );

        GUILayout.Label(
            loader.useSpatialSubChunks
                ? "Warning: measured CPU submission cost is higher."
                : "Disabled: production baseline uses full chunks."
        );

        GUILayout.Space(8f);
    }

    private void DrawDiagnosticsSettings()
    {
        GUILayout.Label("Diagnostics", BoldLabelStyle);

        loader.enableDiagnostics = GUILayout.Toggle(
            loader.enableDiagnostics,
            "Enable loader diagnostics"
        );

        loader.diagnosticsLogInterval = SliderWithValue(
            "Diagnostics interval",
            loader.diagnosticsLogInterval,
            0.5f,
            30f,
            "F1"
        );

        GUILayout.Space(8f);
    }

    private void EnsureWindowRect()
    {
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);

        float width = Mathf.Min(
            WindowWidth,
            Mathf.Max(1f, screenWidth - WindowMargin * 2f)
        );

        float height = Mathf.Min(
            WindowHeight,
            Mathf.Max(1f, screenHeight - WindowMargin * 2f)
        );

        windowRect.width = width;
        windowRect.height = height;

        windowRect.x = Mathf.Clamp(
            windowRect.x,
            0f,
            Mathf.Max(0f, screenWidth - width)
        );

        windowRect.y = Mathf.Clamp(
            windowRect.y,
            0f,
            Mathf.Max(0f, screenHeight - height)
        );
    }

    private float SliderWithValue(
        string label,
        float value,
        float minimum,
        float maximum,
        string format)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(178f));

        value = GUILayout.HorizontalSlider(
            value,
            minimum,
            maximum
        );

        GUILayout.Label(
            value.ToString(format),
            GUILayout.Width(58f)
        );

        GUILayout.EndHorizontal();

        return value;
    }

    private int IntSliderWithValue(
        string label,
        int value,
        int minimum,
        int maximum)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(178f));

        value = Mathf.RoundToInt(
            GUILayout.HorizontalSlider(
                value,
                minimum,
                maximum
            )
        );

        GUILayout.Label(
            value.ToString(),
            GUILayout.Width(58f)
        );

        GUILayout.EndHorizontal();

        return value;
    }

    private void ApplyChangedSettings()
    {
        if (loader == null)
            return;

        if (!snapshotInitialized)
        {
            CaptureSnapshot();
            return;
        }

        bool nearFieldChanged =
            previousUseIndividualNearField !=
            loader.useIndividualNearField;

        bool batchLayoutChanged =
            previousUseSpatialSubChunks !=
            loader.useSpatialSubChunks ||
            previousInstancedLodIndex !=
            loader.instancedLodIndex;

        bool shadowSettingsChanged =
            previousInstancedCastShadows !=
            loader.instancedCastShadows ||
            previousInstancedReceiveShadows !=
            loader.instancedReceiveShadows;

        if (nearFieldChanged || batchLayoutChanged)
        {
            loader.RefreshRuntimeSettings(true);
        }
        else if (shadowSettingsChanged)
        {
            loader.RefreshRuntimeSettings(false);
        }

        CaptureSnapshot();
    }

    private void CaptureSnapshot()
    {
        if (loader == null)
            return;

        previousUseIndividualNearField =
            loader.useIndividualNearField;

        previousUseSpatialSubChunks =
            loader.useSpatialSubChunks;

        previousInstancedLodIndex =
            loader.instancedLodIndex;

        previousInstancedCastShadows =
            loader.instancedCastShadows;

        previousInstancedReceiveShadows =
            loader.instancedReceiveShadows;

        snapshotInitialized = true;
    }
}
