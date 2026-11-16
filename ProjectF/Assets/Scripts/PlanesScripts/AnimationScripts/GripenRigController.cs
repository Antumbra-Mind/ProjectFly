using UnityEngine;
using UnityEngine.InputSystem;

public enum Axis
{
    X,
    Y,
    Z
}

public class GripenRigController : MonoBehaviour
{
    [System.Serializable]
    public class GearStrut
    {
        public string name;
        public WheelCollider collider;
        public Transform strutAmoBone;
        public Transform wheelSpinBone;
        public Vector3 spinLocalAxis = Vector3.right;
        public bool invertSpin = false;

        [HideInInspector] public Vector3 initialAmoLocalPos;
        [HideInInspector] public Quaternion initialAmoLocalRot;
        [HideInInspector] public float currentSpinAngle;
        [HideInInspector] public Quaternion initialWheelLocalRot;
    }

    [Header("Input Actions Asset")]
    public InputActionAsset inputActions;

    [Header("Canards (передні рулі - тангаж)")]
    public Transform leftCanard;
    public Transform rightCanard;
    public float maxCanardAngle = 25f;
    public Axis canardAxis = Axis.X;
    public bool invertLeftCanard = true;
    public bool invertRightCanard = false;

    [Header("Elevons (задні крила - тангаж + крен)")]
    public Transform innerLeftElevon;
    public Transform innerRightElevon;
    public Transform outerLeftElevon;
    public Transform outerRightElevon;
    public float maxInnerElevonAngle = 22f;
    public float maxOuterElevonAngle = 22f;
    public Axis elevonAxis = Axis.X;
    public bool invertLeftElevon = false;
    public bool invertRightElevon = false;

    [Header("Rudder (кіль - рискання)")]
    public Transform rudder;
    public float maxRudderAngle = 25f;
    public Axis rudderAxis = Axis.Y;
    public bool invertRudder = false;

    [Header("Slats (передкрилки)")]
    public Transform leftSlat;
    public Transform rightSlat;
    public float maxSlatAngle = 15f;
    [Tooltip("Локальний вектор осі обертання (наприклад, (1, 1, 0) або (0.7, 0.7, 0))")]
    public Vector3 leftSlatAxis = new Vector3(1f, 1f, 0f);
    public Vector3 rightSlatAxis = new Vector3(1f, -1f, 0f);
    public bool invertLeftSlat = false;
    public bool invertRightSlat = false;

    [Header("Airbrakes (повітряні гальма)")]
    public Transform leftAirbrake;
    public Transform rightAirbrake;
    public float maxAirbrakeAngle = 45f;
    public Axis airbrakeAxis = Axis.X;
    public bool invertLeftAirbrake = false;
    public bool invertRightAirbrake = true;

    [Header("Сопло (пелюстки + форсаж)")]
    public Transform[] nozzlePetals = { };
    public Transform innerPetals;
    public Transform outerPetals;
    [Tooltip("Локальна вісь вздовж довжини сопла, яку НЕ потрібно масштабувати")]
    public Axis nozzleLengthAxis = Axis.Y;
    public float closedNozzleScale = 0.85f;
    public float openNozzleScale = 1.15f;
    public bool invertNozzle = false;

    [Header("Стійки шасі (Передня, Ліва, Права)")]
    public GearStrut[] struts = new GearStrut[3];

    [Header("Поворот передньої стійки (Taxi)")]
    public Vector3 frontSteerLocalAxis = Vector3.up;
    [Range(0.1f, 1f)]
    public float visualSteerMultiplier = 0.45f;
    public float frontSteerSmoothSpeed = 8f;
    public bool invertFrontSteer = false;

    [Header("Шасі (Animator)")]
    public Animator gripenAnimator;
    public string gearAnimState = "Gear_Retract";
    [Range(0.1f, 2f)]
    public float gearAnimSpeed = 0.4f;

    [Header("Загальні налаштування")]
    public float rigSmoothSpeed = 10f;

    private InputAction _pitchAct, _rollAct, _yawAct, _airbrakeAct, _gearAct;
    private float _curPitch, _curRoll, _curYaw, _curNozzleOpen = 1f, _curAirbrake;
    private bool _airbrakeOpen;
    [HideInInspector]
    public bool _gearRetracted;
    public event System.Action OnGearStateChanged;

    private float _extThrottle;
    private float _extAfterburner;

    private Quaternion _lCanardBase, _rCanardBase;
    private Quaternion _inLElevonBase, _inRElevonBase, _outLElevonBase, _outRElevonBase;
    private Quaternion _rudderBase, _lSlatBase, _rSlatBase, _lBrakeBase, _rBrakeBase;
    private Vector3 _innerPetalsBaseScale, _outerPetalsBaseScale;
    private Vector3[] _petalBaseScales;

    private void Awake()
    {
        if (leftCanard) _lCanardBase = leftCanard.localRotation;
        if (rightCanard) _rCanardBase = rightCanard.localRotation;

        if (innerLeftElevon) _inLElevonBase = innerLeftElevon.localRotation;
        if (innerRightElevon) _inRElevonBase = innerRightElevon.localRotation;
        if (outerLeftElevon) _outLElevonBase = outerLeftElevon.localRotation;
        if (outerRightElevon) _outRElevonBase = outerRightElevon.localRotation;

        if (rudder) _rudderBase = rudder.localRotation;
        if (leftSlat) _lSlatBase = leftSlat.localRotation;
        if (rightSlat) _rSlatBase = rightSlat.localRotation;
        if (leftAirbrake) _lBrakeBase = leftAirbrake.localRotation;
        if (rightAirbrake) _rBrakeBase = rightAirbrake.localRotation;

        if (innerPetals) _innerPetalsBaseScale = innerPetals.localScale;
        if (outerPetals) _outerPetalsBaseScale = outerPetals.localScale;

        if (nozzlePetals != null && nozzlePetals.Length > 0)
        {
            _petalBaseScales = new Vector3[nozzlePetals.Length];
            for (int i = 0; i < nozzlePetals.Length; i++)
            {
                if (nozzlePetals[i]) _petalBaseScales[i] = nozzlePetals[i].localScale;
            }
        }

        if (inputActions)
        {
            var flightMap = inputActions.FindActionMap("Flight", true);
            _pitchAct = flightMap.FindAction("Pitch");
            _rollAct = flightMap.FindAction("Roll");
            _yawAct = flightMap.FindAction("Yaw");
            _airbrakeAct = flightMap.FindAction("Airbrake");
            _gearAct = flightMap.FindAction("LandingGear");
        }
    }

    private void OnEnable()
    {
        if (!inputActions) return;
        inputActions.FindActionMap("Flight")?.Enable();

        if (_airbrakeAct != null) _airbrakeAct.performed += OnAirbrakeToggle;
        if (_gearAct != null) _gearAct.performed += OnLandingGearToggle;
    }

    private void OnDisable()
    {
        if (_airbrakeAct != null) _airbrakeAct.performed -= OnAirbrakeToggle;
        if (_gearAct != null) _gearAct.performed -= OnLandingGearToggle;
    }

    private void OnAirbrakeToggle(InputAction.CallbackContext ctx) => _airbrakeOpen = !_airbrakeOpen;

    private void OnLandingGearToggle(InputAction.CallbackContext ctx)
    {
        _gearRetracted = !_gearRetracted;
        if (gripenAnimator)
        {
            gripenAnimator.speed = 1f;
            float dir = _gearRetracted ? 1f : -1f;
            gripenAnimator.SetFloat("GearSpeed", dir * gearAnimSpeed);
            gripenAnimator.Play(gearAnimState, 0, _gearRetracted ? 0.01f : 0.99f);
        }
        OnGearStateChanged?.Invoke();
    }

    /// <summary>
    /// Передає поточні значення тяги (0..1) та форсажу (0..1) з контролера двигуна для реалістичної роботи сопла.
    /// </summary>
    public void SetEngineState(float throttle01, float afterburner01)
    {
        _extThrottle = Mathf.Clamp01(throttle01);
        _extAfterburner = Mathf.Clamp01(afterburner01);
    }

    private void Update()
    {
        float dt = Time.deltaTime * rigSmoothSpeed;

        float targetPitch = _pitchAct != null ? _pitchAct.ReadValue<float>() : 0f;
        float targetRoll = _rollAct != null ? _rollAct.ReadValue<float>() : 0f;
        float targetYaw = _yawAct != null ? _yawAct.ReadValue<float>() : 0f;

        // 0% тяги -> відкрите (1), 100% тяги -> звужене (0), форсаж -> знову відкривається до (1)
        float milFactor = 1f - _extThrottle;
        float targetNozzleOpen = Mathf.Max(milFactor, _extAfterburner);

        _curPitch = Mathf.Lerp(_curPitch, targetPitch, dt);
        _curRoll = Mathf.Lerp(_curRoll, targetRoll, dt);
        _curYaw = Mathf.Lerp(_curYaw, targetYaw, dt);
        _curNozzleOpen = Mathf.Lerp(_curNozzleOpen, targetNozzleOpen, dt);
        _curAirbrake = Mathf.Lerp(_curAirbrake, _airbrakeOpen ? 1f : 0f, dt);

        // 1. Canards (тангаж)
        float canardAngle = _curPitch * maxCanardAngle;
        if (leftCanard) leftCanard.localRotation = _lCanardBase * GetRigRotation(canardAngle, canardAxis, invertLeftCanard);
        if (rightCanard) rightCanard.localRotation = _rCanardBase * GetRigRotation(canardAngle, canardAxis, invertRightCanard);

        // 2. Elevons (внутрішні та зовнішні — мікс тангажу та крену)
        float leftMix = Mathf.Clamp(_curPitch - _curRoll, -1f, 1f);
        float rightMix = Mathf.Clamp(_curPitch + _curRoll, -1f, 1f);

        if (innerLeftElevon) innerLeftElevon.localRotation = _inLElevonBase * GetRigRotation(leftMix * maxInnerElevonAngle, elevonAxis, invertLeftElevon);
        if (innerRightElevon) innerRightElevon.localRotation = _inRElevonBase * GetRigRotation(rightMix * maxInnerElevonAngle, elevonAxis, invertRightElevon);
        if (outerLeftElevon) outerLeftElevon.localRotation = _outLElevonBase * GetRigRotation(leftMix * maxOuterElevonAngle, elevonAxis, invertLeftElevon);
        if (outerRightElevon) outerRightElevon.localRotation = _outRElevonBase * GetRigRotation(rightMix * maxOuterElevonAngle, elevonAxis, invertRightElevon);

        // 3. Rudder (кіль - рискання)
        if (rudder) rudder.localRotation = _rudderBase * GetRigRotation(_curYaw * maxRudderAngle, rudderAxis, invertRudder);

        // 4. Slats (обертання навколо довільного вектора осі шарніра)
        float slatAngle = Mathf.Clamp01(_curPitch) * maxSlatAngle;
        if (leftSlat && leftSlatAxis.sqrMagnitude > 0.0001f)
        {
            float lAngle = invertLeftSlat ? -slatAngle : slatAngle;
            leftSlat.localRotation = _lSlatBase * Quaternion.AngleAxis(lAngle, leftSlatAxis.normalized);
        }
        if (rightSlat && rightSlatAxis.sqrMagnitude > 0.0001f)
        {
            float rAngle = invertRightSlat ? -slatAngle : slatAngle;
            rightSlat.localRotation = _rSlatBase * Quaternion.AngleAxis(rAngle, rightSlatAxis.normalized);
        }

        // 5. Airbrakes (повітряні гальма)
        float brakeAngle = _curAirbrake * maxAirbrakeAngle;
        if (leftAirbrake) leftAirbrake.localRotation = _lBrakeBase * GetRigRotation(brakeAngle, airbrakeAxis, invertLeftAirbrake);
        if (rightAirbrake) rightAirbrake.localRotation = _rBrakeBase * GetRigRotation(brakeAngle, airbrakeAxis, invertRightAirbrake);

        // 6. Сопло (зміна діаметра через localScale перпендикулярно до осі довжини)
        float nozzleFactor = invertNozzle ? (1f - _curNozzleOpen) : _curNozzleOpen;
        float scaleMult = Mathf.Lerp(closedNozzleScale, openNozzleScale, nozzleFactor);

        if (innerPetals) innerPetals.localScale = GetNozzleScale(_innerPetalsBaseScale, scaleMult, nozzleLengthAxis);
        if (outerPetals) outerPetals.localScale = GetNozzleScale(_outerPetalsBaseScale, scaleMult, nozzleLengthAxis);

        if (nozzlePetals != null && _petalBaseScales != null)
        {
            for (int i = 0; i < nozzlePetals.Length; i++)
            {
                if (nozzlePetals[i])
                    nozzlePetals[i].localScale = GetNozzleScale(_petalBaseScales[i], scaleMult, nozzleLengthAxis);
            }
        }
    }

    private Quaternion GetRigRotation(float angle, Axis axis, bool invert = false)
    {
        if (invert) angle = -angle;
        Vector3 euler = axis switch
        {
            Axis.X => new Vector3(angle, 0f, 0f),
            Axis.Y => new Vector3(0f, angle, 0f),
            Axis.Z => new Vector3(0f, 0f, angle),
            _ => Vector3.zero
        };

        return Quaternion.Euler(euler);
    }

    private Vector3 GetNozzleScale(Vector3 baseScale, float radialMultiplier, Axis lengthAxis)
    {
        return lengthAxis switch
        {
            Axis.X => new Vector3(baseScale.x, baseScale.y * radialMultiplier, baseScale.z * radialMultiplier),
            Axis.Y => new Vector3(baseScale.x * radialMultiplier, baseScale.y, baseScale.z * radialMultiplier),
            Axis.Z => new Vector3(baseScale.x * radialMultiplier, baseScale.y * radialMultiplier, baseScale.z),
            _ => baseScale * radialMultiplier
        };
    }
}