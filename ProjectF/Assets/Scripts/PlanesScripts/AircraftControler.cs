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

        AirBrake = aircraftInput.Airbrake;
    }
}
