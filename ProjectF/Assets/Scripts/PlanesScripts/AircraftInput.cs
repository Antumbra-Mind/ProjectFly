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
    private bool firegun;
    private bool firerocketspesial;
    private int selectweapon;

    private bool hideHUD;
    private bool cameraWatchSwitch;
    private bool cameraHold;

    public float Pitch => pitch;
    public float Roll => roll;
    public float Yaw => yaw;
    public float Throttle => throttle; 
    public float Airbrake => airbrake;
    public float Landgear => landgear;
    public float Taxi => taxi;
    public bool Brake => brake;
    public bool FireGun => firegun;
    public bool FireRocketSpesial => firerocketspesial;
    public int SelectWeapon => selectweapon;
    public bool HideHUD => hideHUD;
    public bool CameraWatchSwitch => cameraWatchSwitch;
    public bool CameraHold => cameraHold;


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

        inputAction.Weapons.FireGun.performed += OnFireGun;
        inputAction.Weapons.FireGun.canceled += OnFireGun;

        inputAction.Weapons.FireRocketspesial.performed += OnFireRocket;

        inputAction.Weapons.SelectWeapon.performed += OnSelectWeapon;

        inputAction.HudCamera.HideHUD.performed += OnHideHUD;
        inputAction.HudCamera.CameraWatchSwitch.performed += OnCameraWatchSwitch;

        inputAction.HudCamera.CameraHold.performed += OnCameraHold;
        inputAction.HudCamera.CameraHold.canceled += OnCameraHold;
    }


    private void OnPitch(InputAction.CallbackContext context)
    {
        pitch = context.ReadValue<float>();
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

    private void OnFireGun(InputAction.CallbackContext context)
    {
        firegun = context.ReadValueAsButton();
        Debug.Log($"Gun Fire!!");
    }

    private void OnFireRocket(InputAction.CallbackContext context)
    {
        Debug.Log("Missle away!");
    }

    private void OnSelectWeapon(InputAction.CallbackContext context)
    {
        switch (context.control.name)
        {
            case "1": selectweapon = 1; break;
            case "2": selectweapon = 2; break;
            case "3": selectweapon = 3; break;
            case "4": selectweapon = 4; break;
        }
        Debug.Log($"Selected weapin: {selectweapon}");
    }

    private void OnHideHUD(InputAction.CallbackContext context)
    {
        hideHUD = !hideHUD;
        Debug.Log($"HideHUD: {hideHUD}");
    }

    private void OnCameraWatchSwitch(InputAction.CallbackContext context)
    {
        cameraWatchSwitch = !cameraWatchSwitch;
        Debug.Log($"CameraWatchSwitch: {cameraWatchSwitch}");
    }

    private void OnCameraHold(InputAction.CallbackContext context)
    {
        cameraHold = context.ReadValueAsButton();
        Debug.Log($"CameraHold: {cameraHold}");
    }

    private void OnDisable()
    {
        inputAction?.Disable();
        inputAction?.Dispose();
    }
}
