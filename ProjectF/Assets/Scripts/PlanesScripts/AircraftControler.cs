using UnityEngine;

public class AircraftControler : MonoBehaviour
{
    private AircraftInput aircraftInput;

    private void Awake()
    {
        aircraftInput = GetComponent<AircraftInput>();
    }

    private void Update()
    {
        Debug.Log($"Pitch: {aircraftInput.Pitch}");
    }
}
