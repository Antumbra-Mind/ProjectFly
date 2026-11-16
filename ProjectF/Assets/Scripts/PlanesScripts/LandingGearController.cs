using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.Windows;
using static AircraftGroundMovement;

public class LandingGearController : MonoBehaviour
{
    private AircraftInput Ainput;
    private AircraftGroundMovement groundmove;
    private AircraftStats stats;
    public WheelCollider FrontGear;
    public WheelCollider BackLeftGear;
    public WheelCollider BackRightGear;


    private void Awake()
    {
        stats = GetComponent<AircraftStats>();
        Ainput = GetComponent<AircraftInput>();
        groundmove = GetComponent<AircraftGroundMovement>();
    }

    private void FixedUpdate()
    {
        if (groundmove.CurrentState == AircraftState.Taxi && !groundmove.IsTransitioning)
        {
            FrontGear.steerAngle = Ainput.Taxi * 30f;
            Debug.Log(Ainput.Taxi);
            float BrakeForce = Ainput.Brake ? stats.BrakePower : 0f;

            BackLeftGear.brakeTorque = BrakeForce;
            BackRightGear.brakeTorque = BrakeForce;
        }
        else
        {
            FrontGear.steerAngle = 0f;

            BackLeftGear.brakeTorque = 0f;
            BackRightGear.brakeTorque = 0f;
        }
    }
}
