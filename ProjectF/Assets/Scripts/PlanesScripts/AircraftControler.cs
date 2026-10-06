using UnityEngine;

public class AircraftControler : MonoBehaviour
{
    private AircraftInput aircraftInput;
    private AircraftPhisics aircraftphisics;
    private AircraftRotation aircraftrotation;
    private AircraftGroundMovement airgmove;
    private LandingGearController landgear;
    private AirbrakeController airbrake;
    private AircraftStats airstats;
    public float Pitch;
    public float Roll;
    public float Yaw;

    private void Awake()
    {
        aircraftInput = GetComponent<AircraftInput>();
    }

    private void Update()
    {
        Pitch = aircraftInput.Pitch;
        Roll = aircraftInput.Roll;
        Yaw = aircraftInput.Yaw;
        Debug.Log($"Pitch: {aircraftInput.Pitch}");
    }
}
