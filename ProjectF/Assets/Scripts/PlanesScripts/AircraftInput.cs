using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



public class AircraftInput : MonoBehaviour
{
    private AircraftInputActions inputAction;

    private void Awake()
    {
        inputAction = new AircraftInputActions();
        inputAction.Enable();
    }

    private void Update()
    {
        float pitch = inputAction.Flight.Pitch.ReadValue<float>();
        Debug.Log(pitch);
    }
}
