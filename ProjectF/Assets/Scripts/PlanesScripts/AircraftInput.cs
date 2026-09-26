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
    private float airbrake;
    private float landgear;
    private float taxi;
    private bool brake;
    private float firegun;
    private bool firerocketspesial;
    private float selectweapon;

    public float Pitch => pitch;
    public float Roll => roll;
    public float Yaw => yaw;
    public float Throttle => throttle; 
    public float Airbrake => airbrake;
    public float Landgear => landgear;
    public float Taxi => taxi;
    public bool Brake => brake;
    public float FireGun => firegun;
    public bool FireRocketSpesial => firerocketspesial;
    public float SelectWeapon => selectweapon;



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

        inputAction.Flight.Airbrake.performed += OnAirBrake;

        inputAction.Flight.LandingGear.performed += OnLandGear;

        inputAction.Ground.Taxi.performed += OnTaxi;
        inputAction.Ground.Taxi.canceled += OnTaxi;

        inputAction.Ground.Brake.performed += OnLandBrake;
        inputAction.Ground.Brake.canceled += OnLandBrake;
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

    private void OnAirBrake(InputAction.CallbackContext context)
    {
        Debug.Log("Airbrake On!");
    }

    private void OnLandGear(InputAction.CallbackContext context)
    {
        Debug.Log("LandGear On!");
    }

    private void OnTaxi(InputAction.CallbackContext context)
    {
        taxi = context.ReadValue<float>();
    }
    private void OnLandBrake(InputAction.CallbackContext context)
    {
        brake = context.ReadValueAsButton();
    }
}
