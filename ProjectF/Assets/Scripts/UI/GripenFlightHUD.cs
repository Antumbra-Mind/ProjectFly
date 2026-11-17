using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GripenFlightHUD : MonoBehaviour
{
    [Header("Aircraft References")]
    [SerializeField] private Transform guffuAhPlane;
    [SerializeField] private AircraftControler controler;
    [SerializeField] private Rigidbody planeRigidbody;
    [SerializeField] private AircraftInput input;
    [SerializeField] private AircraftPhisics phisics;
    [SerializeField] private AircraftGroundMovement gMove;

    [Header("HUD Root (for HideHUD toggle)")]
    [SerializeField] private GameObject hudContent;

    [Header("Telemetry Texts")]
    [SerializeField] private TMP_Text spdText;
    [SerializeField] private TMP_Text altText;
    [SerializeField] private TMP_Text thrText;
    [SerializeField] private TMP_Text afbText;

    [Header("Throttle Bar (Image Type: Filled)")]
    [SerializeField] private Image throttleFillImage;
    [SerializeField] private Color normalThrottleColor = new Color(0f, 0.94f, 1f, 1f);      // #00F0FF
    [SerializeField] private Color afterburnerColor = new Color(1f, 0.55f, 0f, 1f);         // #FF8C00

    [Header("Stall Warning")]
    [SerializeField] private CanvasGroup stallWarningGroup;
    [SerializeField] private float stallPulseSpeed = 7f;

    [Header("Dynamic Center Reticle")]
    [SerializeField] private RectTransform dynamicReticle;
    [SerializeField] private Vector2 reticleBaseOffset = new Vector2(0f, 95f);              // Базове зміщення вперед/вгору від центру
    [SerializeField] private float maxReticleOffset = 45f;
    [SerializeField] private float reticleSmoothSpeed = 10f;

    [Header("System Indicators (Gear / Airbrake / Brake)")]
    [SerializeField] private Image gearBg;
    [SerializeField] private TMP_Text gearLabel;
    [SerializeField] private Image airbrakeBg;
    [SerializeField] private TMP_Text airbrakeLabel;
    [SerializeField] private Image brakeBg;
    [SerializeField] private TMP_Text brakeLabel;

    [Header("Indicator Colors")]
    [SerializeField] private Color activeCyanColor = new Color(0f, 0.85f, 0.95f, 0.9f);
    [SerializeField] private Color activeAmberColor = new Color(1f, 0.6f, 0f, 0.9f);
    [SerializeField] private Color inactiveBgColor = new Color(0.02f, 0.06f, 0.09f, 0.85f);
    [SerializeField] private Color inactiveTextColor = new Color(0.6f, 0.88f, 0.92f, 0.55f);

    private bool previousHideHUD;
    //private bool isAirbrakeDeployed;
    //private bool wasAirbrakeInputPressed;

    private void Awake()
    {
        if (guffuAhPlane != null)
        {
            if (phisics == null) phisics = guffuAhPlane.GetComponent<AircraftPhisics>();
            if (planeRigidbody == null) planeRigidbody = guffuAhPlane.GetComponent<Rigidbody>();
            if (gMove == null) gMove = guffuAhPlane.GetComponent<AircraftGroundMovement>();
            if (input == null) input = guffuAhPlane.GetComponent<AircraftInput>();
            if (controler == null) controler = guffuAhPlane.GetComponent<AircraftControler>();
        }

        if (controler == null)
            controler = GetComponent<AircraftControler>();

        if (hudContent != null && input != null)
        {
            previousHideHUD = input.HideHUD;
            hudContent.SetActive(!input.HideHUD);
        }
    }

    private void Update()
    {
        UpdateHUDVisibility();
        if (hudContent != null && !hudContent.activeSelf) return;

        UpdateThrottle();
        UpdateFlightTelemetry();
        UpdateSystemIndicators();
        UpdateStallWarning();
        UpdateDynamicReticle();
    }

    private void UpdateHUDVisibility()
    {
        if (input == null || hudContent == null) return;

        bool hideNow = input.HideHUD;
        if (hideNow == previousHideHUD) return;

        hudContent.SetActive(!hideNow);
        previousHideHUD = hideNow;
    }

    private void UpdateThrottle()
    {
        if (controler == null) return;

        float rawThrottle = controler.throttle; // 0..100 суха тяга, 100..110 форсаж
        float dry = Mathf.Clamp(rawThrottle, 0f, 100f);
        float afb = Mathf.Clamp(rawThrottle - 100f, 0f, 10f) * 10f; // переводимо 0..10 у 0..100%

        if (thrText != null)
            thrText.text = $"THR <mspace=0.58em>{dry:F0}</mspace>%";

        if (afbText != null)
        {
            afbText.text = $"AFB <mspace=0.58em>{afb:F0}</mspace>%";
            afbText.alpha = afb > 0.5f ? 1f : 0.45f;
        }

        if (throttleFillImage != null)
        {
            // 0..80% висоти шкали займає суха тяга (0..100), 80..100% — форсаж (100..110)
            float dryNormalized = dry / 100f;
            float afbNormalized = afb / 100f;
            throttleFillImage.fillAmount = Mathf.Clamp01((dryNormalized * 0.8f) + (afbNormalized * 0.2f));
            throttleFillImage.color = rawThrottle > 100.1f ? afterburnerColor : normalThrottleColor;
        }
    }

    private void UpdateFlightTelemetry()
    {
        if (guffuAhPlane != null && altText != null)
        {
            float altitude = guffuAhPlane.position.y;
            altText.text = $"<mspace=0.58em>{altitude:F0}</mspace> <size=15><color=#7AD7E3>M</color></size>";
        }

        if (planeRigidbody != null && spdText != null)
        {
            float speedKmh = planeRigidbody.linearVelocity.magnitude * 3.6f;
            spdText.text = $"<mspace=0.58em>{speedKmh:F0}</mspace> <size=15><color=#7AD7E3>KM/H</color></size>";
        }
    }

    private void UpdateSystemIndicators()
    {
        // 1. Шасі (GEAR)
        if (gMove != null && gearBg != null)
        {
            if (gMove.IsTransitioning)
            {
                SetIndicatorVisual(gearBg, gearLabel, true, activeAmberColor); // Жовтий під час випуску/прибирання
            }
            else if (gMove.CurrentState == AircraftGroundMovement.AircraftState.Taxi)
            {
                SetIndicatorVisual(gearBg, gearLabel, true, activeCyanColor);  // Бірюзовий коли випущені
            }
            else
            {
                SetIndicatorVisual(gearBg, gearLabel, false, inactiveBgColor); // Темний коли прибрані в польоті
            }
        }

        // 2. Повітряне гальмо (AIR BRAKE) — бере стан з AircraftControler
        if (controler != null && airbrakeBg != null)
        {
            bool isAirbrakeActive = controler.AirBrake > 0.5f;
            SetIndicatorVisual(airbrakeBg, airbrakeLabel, isAirbrakeActive, activeAmberColor);
        }

        // 3. Колісне гальмо (BRAKE)
        if (input != null && brakeBg != null)
        {
            SetIndicatorVisual(brakeBg, brakeLabel, input.Brake, activeAmberColor);
        }
    }

    private void SetIndicatorVisual(Image bg, TMP_Text label, bool isActive, Color activeColor)
    {
        bg.color = isActive ? activeColor : inactiveBgColor;
        if (label != null)
            label.color = isActive ? Color.white : inactiveTextColor;
    }

    private void UpdateStallWarning()
    {
        if (stallWarningGroup == null || phisics == null) return;

        if (!phisics.IsStalling)
        {
            stallWarningGroup.alpha = 0f;
            return;
        }

        stallWarningGroup.alpha = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.time * stallPulseSpeed) + 1f) * 0.5f);
    }

    private void UpdateDynamicReticle()
    {
        if (dynamicReticle == null || guffuAhPlane == null) return;

        // Поточний крен літака для нахилу символу
        float rollAngle = guffuAhPlane.eulerAngles.z;
        dynamicReticle.localRotation = Quaternion.Euler(0f, 0f, rollAngle);

        // Зміщення прицілу в бік кутової швидкості розвороту/тангажу + базовий зсув вперед (вгору)
        if (planeRigidbody != null)
        {
            Vector3 localAngVel = guffuAhPlane.InverseTransformDirection(planeRigidbody.angularVelocity);
            Vector2 turnOffset = new Vector2(localAngVel.y, -localAngVel.x) * 25f;
            turnOffset = Vector2.ClampMagnitude(turnOffset, maxReticleOffset);

            Vector2 targetPos = reticleBaseOffset + turnOffset;
            dynamicReticle.anchoredPosition = Vector2.Lerp(dynamicReticle.anchoredPosition, targetPos, Time.deltaTime * reticleSmoothSpeed);
        }
    }
}