using Unity.Mathematics;
using UnityEngine;

public class AircraftControler : MonoBehaviour
{
    private AircraftInput aircraftInput;
    private AircraftPhisics aircraftphisics;
    private AircraftRotation aircraftrotation;
    private AircraftGroundMovement airgmove;
    private LandingGearController landgear;
    private AircraftStats airstats;
    public float Pitch;
    public float Roll;
    public float Yaw;
    public float throttle = 0f;
    public float AirBrake;
    public float LangGear;
    [SerializeField] private float throttlespeed = 50f;
    [Header("Airbrake Settings")]
    [Tooltip("false = тримати кнопку для відкриття, true = перемикач вкл/викл по натисканню")]
    public bool toggleAirbrake = false;
    private bool _airbrakeToggled = false;
    private bool _prevAirbrakeInput = false;
    GripenRigController rigController;

    private void Awake()
    {
        aircraftInput = GetComponent<AircraftInput>();
        rigController = GetComponent<GripenRigController>();
    }

    private void Update()
    {
        Pitch = aircraftInput.Pitch;
        Roll = aircraftInput.Roll;
        Yaw = aircraftInput.Yaw;
        //Thottle = aircraftInput.Throttle;
        if (aircraftInput.Throttle > 0f)
        {
            throttle = math.clamp(throttle + throttlespeed * Time.deltaTime, 0f, 110f);
        }
        else if(aircraftInput.Throttle < 0f)
        {
            throttle = math.clamp(throttle - throttlespeed * Time.deltaTime, 0f, 110f);
        }

        if (rigController != null)
        {
            float normalizedThrottle = Mathf.Clamp01(throttle / 100f);
            float normalizedAfterburner = Mathf.Clamp01((throttle - 100f) / 10f);
            rigController.SetEngineState(normalizedThrottle, normalizedAfterburner);
        }

        bool rawPressed = aircraftInput.Airbrake > 0.5f;

        if (toggleAirbrake)
        {
            if (rawPressed && !_prevAirbrakeInput)
            {
                _airbrakeToggled = !_airbrakeToggled;
            }
        }
        else
        {
            _airbrakeToggled = rawPressed;
        }

        _prevAirbrakeInput = rawPressed;
        AirBrake = _airbrakeToggled ? 1f : 0f;
    }
}
