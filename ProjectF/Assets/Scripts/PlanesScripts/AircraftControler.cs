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

    private void Awake()
    {
        aircraftInput = GetComponent<AircraftInput>();
    }

    private void Update()
    {
        Debug.Log($"Pitch: {aircraftInput.Pitch}");
    }
}
