using UnityEngine;

public class SearchlightFlicker : MonoBehaviour
{
    [Header("Джерела світла та меші")]
    [SerializeField] private Light spotLight;
    [SerializeField] private Renderer innerConeRenderer;
    [SerializeField] private Renderer outerConeRenderer;
    [SerializeField] private Renderer haloQuadRenderer;

    [Header("Режим мерехтіння")]
    [SerializeField] private float baseLightIntensity = 150f;
    [SerializeField] private float minMultiplier = 0.25f;
    [SerializeField] private float maxMultiplier = 1.05f;
    [SerializeField] private float noiseSpeed = 8f;

    [Header("Різкі провали (ефект несправної лампи)")]
    [SerializeField] private bool enableShortGlitches = true;
    [Range(0f, 1f)]
    [SerializeField] private float glitchChance = 0.04f;

    private MaterialPropertyBlock _propBlock;
    private int _intensityPropID;

    private float _baseInnerAlpha;
    private float _baseOuterAlpha;
    private float _baseHaloAlpha;
    private float _noiseOffset;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        _intensityPropID = Shader.PropertyToID("_Intensity");
        _noiseOffset = Random.Range(0f, 100f);

        // Зчитуємо початкові значення з матеріалів, щоб не збити твоє налаштування
        if (innerConeRenderer != null)
            _baseInnerAlpha = innerConeRenderer.sharedMaterial.GetFloat(_intensityPropID);
        if (outerConeRenderer != null)
            _baseOuterAlpha = outerConeRenderer.sharedMaterial.GetFloat(_intensityPropID);
        if (haloQuadRenderer != null)
            _baseHaloAlpha = haloQuadRenderer.sharedMaterial.GetFloat(_intensityPropID);
    }

    private void Update()
    {
        // Плавне тремтіння через шум Перліна
        float noise = Mathf.PerlinNoise(Time.time * noiseSpeed, _noiseOffset);
        float multiplier = Mathf.Lerp(minMultiplier, maxMultiplier, noise);

        // Рідкісні короткі провали напруги
        if (enableShortGlitches && Random.value < glitchChance)
        {
            multiplier *= Random.Range(0.05f, 0.3f);
        }

        // 1. Оновлюємо реальне світло на бетоні та літаку
        if (spotLight != null)
        {
            spotLight.intensity = baseLightIntensity * multiplier;
        }

        // 2. Синхронно оновлюємо шейдери конусів та білборда без створення копій матеріалів
        ApplyShaderIntensity(innerConeRenderer, _baseInnerAlpha * multiplier);
        ApplyShaderIntensity(outerConeRenderer, _baseOuterAlpha * multiplier);
        ApplyShaderIntensity(haloQuadRenderer, _baseHaloAlpha * multiplier);
    }

    private void ApplyShaderIntensity(Renderer targetRenderer, float value)
    {
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(_intensityPropID, value);
        targetRenderer.SetPropertyBlock(_propBlock);
    }
}