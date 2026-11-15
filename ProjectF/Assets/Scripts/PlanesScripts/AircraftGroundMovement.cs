using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.XR.Haptics;

public class AircraftGroundMovement : MonoBehaviour
{
    public AircraftControler controller;
    public AircraftInput Ainput;
    public WheelCollider FrontGear;
    public WheelCollider BackLeftGear;
    public WheelCollider BackRightGear;
    private bool PreviousGearInput = false;
    public AircraftState CurrentState = AircraftState.Taxi;
    public bool IsTransitioning;
    public enum AircraftState
    {
        Taxi,
        Flying
    }

    private void Awake()
    {
        controller = GetComponent<AircraftControler>();
        Ainput = GetComponent<AircraftInput>();
    }

    private void Update()
    {
        bool GearNow = Ainput.Landgear;
        if(GearNow && !PreviousGearInput && !IsTransitioning)
        {
            StartCoroutine(ChangeAircraftState());
        }
        PreviousGearInput = Ainput.Landgear;
    }

    private IEnumerator ChangeAircraftState()
    {
        IsTransitioning = true;
        yield return new WaitForSeconds(5f);
        CurrentState = CurrentState == AircraftState.Flying ? AircraftState.Taxi : AircraftState.Flying;
        bool GearEnable = CurrentState == AircraftState.Taxi;

        FrontGear.enabled = GearEnable;
        BackLeftGear.enabled = GearEnable;
        BackRightGear.enabled = GearEnable; 
            
        IsTransitioning = false;
    }
}
