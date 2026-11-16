using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GripenFlightHUD : MonoBehaviour
{
    [Header("Telemetry Texts")]
    [SerializeField] private TextMeshProUGUI spdText;
    [SerializeField] private TextMeshProUGUI altText;
    [SerializeField] private TextMeshProUGUI thrText;
    [SerializeField] private TextMeshProUGUI afbText;

    [Header("Vertical Throttle Bar")]
    [SerializeField] private Image throttleFillImage;
    [SerializeField] private Color normalThrottleColor = new Color(0f, 0.94f, 1f, 1f);      // #00F0FF
    [SerializeField] private Color afterburnerColor = new Color(1f, 0.55f, 0f, 1f);         // #FF8C00

    [Header("Stall Warning")]
    [SerializeField] private CanvasGroup stallWarningGroup;
    [SerializeField] private float stallPulseSpeed = 7f;

    [Header("Dynamic Center Reticle")]
    [SerializeField] private RectTransform dynamicReticle;
    [SerializeField] private float maxReticleOffset = 45f;
    [SerializeField] private float reticleSmoothSpeed = 10f;

    [Header("System Indicators (3 Slots)")]
    [SerializeField] private Image[] indicatorBackgrounds;
    [SerializeField] private TextMeshProUGUI[] indicatorLabels;
    [SerializeField] private Color activeIndicatorBg = new Color(0f, 0.75f, 0.85f, 0.9f);
    [SerializeField] private Color inactiveIndicatorBg = new Color(0.02f, 0.06f, 0.09f, 0.85f);

    private bool isStalling;

    private void Update()
    {
        UpdateStallPulse();
    }

    public void UpdateTelemetry(float speedKmh, float altitudeMeters, float throttleNormalized, float afterburnerNormalized, bool stall)
    {
        spdText.text = $"<mspace=0.58em>{Mathf.RoundToInt(speedKmh)}</mspace> <size=15><color=#7AD7E3>KM/H</color></size>";
        altText.text = $"<mspace=0.58em>{Mathf.RoundToInt(altitudeMeters)}</mspace> <size=15><color=#7AD7E3>M</color></size>";

        int thrPercent = Mathf.RoundToInt(Mathf.Clamp01(throttleNormalized) * 100f);
        int afbPercent = Mathf.RoundToInt(Mathf.Clamp01(afterburnerNormalized) * 100f);

        thrText.text = $"THR <mspace=0.58em>{thrPercent}</mspace>%";
        afbText.text = $"AFB <mspace=0.58em>{afbPercent}</mspace>%";

        // Вертикальне заповнення: 0..80% звичайна тяга, 80..100% форсаж
        if (throttleFillImage != null)
        {
            float visualFill = (throttleNormalized * 0.8f) + (afterburnerNormalized * 0.2f);
            throttleFillImage.fillAmount = Mathf.Clamp01(visualFill);
            throttleFillImage.color = afterburnerNormalized > 0.01f ? afterburnerColor : normalThrottleColor;
        }

        afbText.alpha = afterburnerNormalized > 0.01f ? 1f : 0.45f;
        isStalling = stall;
    }

    public void UpdateDynamicReticle(Vector2 turnInput, float aircraftRollAngle)
    {
        if (dynamicReticle == null) return;

        Vector2 targetPos = Vector2.ClampMagnitude(turnInput, 1f) * maxReticleOffset;
        dynamicReticle.anchoredPosition = Vector2.Lerp(dynamicReticle.anchoredPosition, targetPos, Time.deltaTime * reticleSmoothSpeed);
        dynamicReticle.localRotation = Quaternion.Euler(0f, 0f, aircraftRollAngle);
    }

    public void SetSystemIndicator(int index, bool isActive, Color? customActiveColor = null)
    {
        if (index < 0 || index >= indicatorBackgrounds.Length) return;

        Color activeCol = customActiveColor ?? activeIndicatorBg;
        indicatorBackgrounds[index].color = isActive ? activeCol : inactiveIndicatorBg;

        if (index < indicatorLabels.Length && indicatorLabels[index] != null)
        {
            indicatorLabels[index].color = isActive ? Color.white : new Color(0.6f, 0.88f, 0.92f, 0.65f);
        }
    }

    private void UpdateStallPulse()
    {
        if (stallWarningGroup == null) return;

        if (!isStalling)
        {
            stallWarningGroup.alpha = 0f;
            return;
        }

        stallWarningGroup.alpha = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.time * stallPulseSpeed) + 1f) * 0.5f);
    }
}