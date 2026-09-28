using UnityEngine;

public class AircraftPhisics : MonoBehaviour
{
    private Rigidbody rigidbody;
    private AircraftInput aircraftInput;

    [SerializeField] private float engineForce = 10000f;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        aircraftInput = GetComponent<AircraftInput>();
    }

    private void FixedUpdate()
    {
        float thottle = aircraftInput.Throttle;

        rigidbody.AddForce(Vector3.forward * thottle * engineForce);
    }

}
