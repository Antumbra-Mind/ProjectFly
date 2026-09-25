using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



public class AircraftInput : MonoBehaviour
{
    private AircraftInputActions inputAction;
    private float pitch;
    private float roll;
    private float yaw;
    private float throttle;

    public float Pitch => pitch;
    public float Roll => roll;
    public float Yaw => yaw;
    public float Throttle => throttle; 

    private void Awake()
    {
        inputAction = new AircraftInputActions();
        inputAction.Enable();

        inputAction.Flight.Pitch.performed += OnPitch;
        inputAction.Flight.Pitch.canceled += OnPitch;

        inputAction.Flight.Roll.performed += OnRoll;
        inputAction.Flight.Roll.canceled += OnRoll;

        inputAction.Flight.Yaw.performed += OnYaw;
        inputAction.Flight.Yaw.canceled += OnYaw;

        inputAction.Flight.Throttle.performed += OnThrottle;
        inputAction.Flight.Throttle.canceled += OnThrottle;
    }


    private void OnPitch(InputAction.CallbackContext context)
    {
        pitch = context.ReadValue<float>();
        Debug.Log($"Pitch: {pitch}");
    }

    private void OnRoll(InputAction.CallbackContext context)
    {
        roll = context.ReadValue<float>();
        Debug.Log($"Roll: {roll}");
    }

    private void OnYaw(InputAction.CallbackContext context)
    {
        yaw = context.ReadValue<float>();
        Debug.Log($"yaw: {yaw}");
    }

    private void OnThrottle(InputAction.CallbackContext context)
    {
        throttle = context.ReadValue<float>();
    }

}
