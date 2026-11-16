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
    public bool IsGrounded { get; private set; }


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
    private IEnumerator Start()
    {
        CurrentState = AircraftState.Flying;

        FrontGear.enabled = false;
        BackLeftGear.enabled = false;
        BackRightGear.enabled = false;

        yield return new WaitForFixedUpdate();

        CurrentState = AircraftState.Taxi;

        FrontGear.enabled = true;
        BackLeftGear.enabled = true;
        BackRightGear.enabled = true;
    }
    private void FixedUpdate()
    {

        WheelHit Hit;

        bool FrontInGround = FrontGear.GetGroundHit(out Hit);
        bool BackLInGround = BackRightGear.GetGroundHit(out Hit);
        bool BackRInGround = BackLeftGear.GetGroundHit(out Hit);

        IsGrounded = FrontInGround || BackRInGround || BackLInGround;
        /*if (CurrentState == AircraftState.Taxi && !IsTransitioning)
        {
            FrontGear.steerAngle = Ainput.Taxi * 30f;
            Debug.Log(Ainput.Taxi);
        }*/ //Перенести в LandGearControl
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
