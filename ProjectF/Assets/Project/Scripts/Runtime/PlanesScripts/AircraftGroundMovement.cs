using System.Collections;
using UnityEngine;

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
        if (GearNow && !PreviousGearInput && !IsTransitioning)
        {
            StartCoroutine(ChangeAircraftState());
        }
        PreviousGearInput = Ainput.Landgear;
    }

    private IEnumerator Start()
    {
        CurrentState = AircraftState.Flying;
        yield return new WaitForFixedUpdate();
        CurrentState = AircraftState.Taxi;
    }

    private void FixedUpdate()
    {
        // Перевіряємо контакт лише якщо колайдер активний
        bool frontInGround = FrontGear != null && FrontGear.enabled && FrontGear.GetGroundHit(out _);
        bool backLInGround = BackLeftGear != null && BackLeftGear.enabled && BackLeftGear.GetGroundHit(out _);
        bool backRInGround = BackRightGear != null && BackRightGear.enabled && BackRightGear.GetGroundHit(out _);

        IsGrounded = frontInGround || backLInGround || backRInGround;
    }

    private IEnumerator ChangeAircraftState()
    {
        IsTransitioning = true;
        yield return new WaitForSeconds(5f);
        CurrentState = CurrentState == AircraftState.Flying ? AircraftState.Taxi : AircraftState.Flying;
        IsTransitioning = false;
    }
}