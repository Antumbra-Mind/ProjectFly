using UnityEngine;
using UnityEngine.InputSystem;

public class GripenRigController : MonoBehaviour
{
    [Header("Input Actions Asset")]
    public InputActionAsset inputActions; // Перетягни сюди AircraftInputActions

    [Header("Canards (передні рулі - тангаж)")]
    public Transform leftCanard;
    public Transform rightCanard;
    public float maxCanardAngle = 25f;

    [Header("Elevons (задні крила - тангаж + крен)")]
    public Transform leftElevon;
    public Transform rightElevon;
    public float maxElevonAngle = 22f;

    [Header("Rudder (кіль - рискання)")]
    public Transform rudder;
    public float maxRudderAngle = 25f;

    [Header("Airbrakes (повітряні гальма)")]
    public Transform leftAirbrake;
    public Transform rightAirbrake;
    public float maxAirbrakeAngle = 45f;

    [Header("Сопло (пелюстки + форсаж)")]
    public Transform[] nozzlePetals;
    public float closedNozzleAngle = 0f;
    public float openNozzleAngle = 18f;

    [Header("Шасі (Animator)")]
    public Animator gripenAnimator;
    public string gearAnimState = "Gear_Retract";
    [Range(0.1f, 2f)]
    public float gearAnimSpeed = 0.4f; // 1 = стандарт, 0.4 = у 2.5 раза повільніше

    [Header("Загальні налаштування")]
    public float rigSmoothSpeed = 10f;

    private InputAction _pitchAct, _rollAct, _yawAct, _throttleAct, _airbrakeAct, _gearAct;
    private float _curPitch, _curRoll, _curYaw, _curThrottle, _curAirbrake;
    private bool _airbrakeOpen;
    private bool _gearRetracted;

    private Quaternion _lCanardBase, _rCanardBase, _lElevonBase, _rElevonBase, _rudderBase, _lBrakeBase, _rBrakeBase;
    private Quaternion[] _petalBases;

    private void Awake()
    {
        // Запам'ятовуємо початкові кути всіх кісток/об'єктів
        if (leftCanard) _lCanardBase = leftCanard.localRotation;
        if (rightCanard) _rCanardBase = rightCanard.localRotation;
        if (leftElevon) _lElevonBase = leftElevon.localRotation;
        if (rightElevon) _rElevonBase = rightElevon.localRotation;
        if (rudder) _rudderBase = rudder.localRotation;
        if (leftAirbrake) _lBrakeBase = leftAirbrake.localRotation;
        if (rightAirbrake) _rBrakeBase = rightAirbrake.localRotation;

        if (nozzlePetals != null && nozzlePetals.Length > 0)
        {
            _petalBases = new Quaternion[nozzlePetals.Length];
            for (int i = 0; i < nozzlePetals.Length; i++)
                if (nozzlePetals[i]) _petalBases[i] = nozzlePetals[i].localRotation;
        }

        // Дістаємо екшени з мапи Flight
        if (inputActions)
        {
            var flightMap = inputActions.FindActionMap("Flight", true);
            _pitchAct = flightMap.FindAction("Pitch");
            _rollAct = flightMap.FindAction("Roll");
            _yawAct = flightMap.FindAction("Yaw");
            _throttleAct = flightMap.FindAction("Throttle");
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
            float dir = _gearRetracted ? 1f : -1f;
            gripenAnimator.SetFloat("GearSpeed", dir * gearAnimSpeed);
            gripenAnimator.Play(gearAnimState, 0, _gearRetracted ? 0f : 1f);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime * rigSmoothSpeed;

        // Зчитуємо осі (1D Axis / Value)
        float targetPitch = _pitchAct != null ? _pitchAct.ReadValue<float>() : 0f;
        float targetRoll = _rollAct != null ? _rollAct.ReadValue<float>() : 0f;
        float targetYaw = _yawAct != null ? _yawAct.ReadValue<float>() : 0f;
        float targetThrottle = _throttleAct != null ? Mathf.Clamp01(_throttleAct.ReadValue<float>()) : 0f;

        _curPitch = Mathf.Lerp(_curPitch, targetPitch, dt);
        _curRoll = Mathf.Lerp(_curRoll, targetRoll, dt);
        _curYaw = Mathf.Lerp(_curYaw, targetYaw, dt);
        _curThrottle = Mathf.Lerp(_curThrottle, targetThrottle, dt);
        _curAirbrake = Mathf.Lerp(_curAirbrake, _airbrakeOpen ? 1f : 0f, dt);

        // 1. Canards (тангаж по локальній осі X)
        float canardAngle = _curPitch * maxCanardAngle;
        if (leftCanard) leftCanard.localRotation = _lCanardBase * Quaternion.Euler(canardAngle, 0f, 0f);
        if (rightCanard) rightCanard.localRotation = _rCanardBase * Quaternion.Euler(canardAngle, 0f, 0f);

        // 2. Elevons (мікс тангажу та крену)
        float leftElevonAngle = Mathf.Clamp(_curPitch - _curRoll, -1f, 1f) * maxElevonAngle;
        float rightElevonAngle = Mathf.Clamp(_curPitch + _curRoll, -1f, 1f) * maxElevonAngle;
        if (leftElevon) leftElevon.localRotation = _lElevonBase * Quaternion.Euler(leftElevonAngle, 0f, 0f);
        if (rightElevon) rightElevon.localRotation = _rElevonBase * Quaternion.Euler(rightElevonAngle, 0f, 0f);

        // 3. Rudder (кіль по локальній осі Y)
        if (rudder) rudder.localRotation = _rudderBase * Quaternion.Euler(0f, _curYaw * maxRudderAngle, 0f);

        // 4. Airbrakes (розкриття по локальній осі Y або X)
        float brakeAngle = _curAirbrake * maxAirbrakeAngle;
        if (leftAirbrake) leftAirbrake.localRotation = _lBrakeBase * Quaternion.Euler(0f, brakeAngle, 0f);
        if (rightAirbrake) rightAirbrake.localRotation = _rBrakeBase * Quaternion.Euler(0f, -brakeAngle, 0f);

        // 5. Пелюстки сопла (розкриваються на форсажі)
        if (nozzlePetals != null)
        {
            float petalAngle = Mathf.Lerp(closedNozzleAngle, openNozzleAngle, _curThrottle);
            for (int i = 0; i < nozzlePetals.Length; i++)
            {
                if (nozzlePetals[i])
                    nozzlePetals[i].localRotation = _petalBases[i] * Quaternion.Euler(petalAngle, 0f, 0f);
            }
        }
    }
}