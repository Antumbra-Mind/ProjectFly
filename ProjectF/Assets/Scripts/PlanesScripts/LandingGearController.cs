using UnityEngine;
using static AircraftGroundMovement;

public class LandingGearController : MonoBehaviour
{
    private AircraftInput Ainput;
    private AircraftGroundMovement groundmove;
    private AircraftStats stats;
    [HideInInspector]
    public Rigidbody rb;

    public WheelCollider FrontGear;
    public WheelCollider BackLeftGear;
    public WheelCollider BackRightGear;

    [Header("Налаштування руління від швидкості")]
    [Tooltip("Максимальний кут повороту на місці (м/с -> 0)")]
    public float maxLowSpeedSteer = 28f;
    [Tooltip("Максимальний кут повороту на високій швидкості розгону")]
    public float maxHighSpeedSteer = 4f;
    [Tooltip("Швидкість (м/с), при якій кут падає до мінімуму (25 м/с ≈ 90 км/год)")]
    public float highSpeedThreshold = 25f;
    [Tooltip("Швидкість повороту самої передньої стійки (град/сек)")]
    public float steerSpeed = 60f;

    private float _currentSteerAngle;

    private void Awake()
    {
        stats = GetComponent<AircraftStats>();
        Ainput = GetComponent<AircraftInput>();
        groundmove = GetComponent<AircraftGroundMovement>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float targetSteer = 0f;

        if (groundmove.CurrentState == AircraftState.Taxi && !groundmove.IsTransitioning)
        {
            // Обчислюємо поточну швидкість літака в м/с
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            float speedFactor = Mathf.Clamp01(speed / highSpeedThreshold);

            // На місці ~28°, на розгоні плавно зменшуємо до ~4°
            float allowedSteer = Mathf.Lerp(maxLowSpeedSteer, maxHighSpeedSteer, speedFactor);
            targetSteer = Ainput.Taxi * allowedSteer;

            float brakeForce = Ainput.Brake ? stats.BrakePower : 0f;
            float wakeTorque = Ainput.Brake ? 0f : 0.0001f;

            FrontGear.brakeTorque = 0f;
            FrontGear.motorTorque = wakeTorque;

            BackLeftGear.brakeTorque = brakeForce;
            BackRightGear.brakeTorque = brakeForce;

            BackLeftGear.motorTorque = wakeTorque;
            BackRightGear.motorTorque = wakeTorque;
        }
        else
        {
            FrontGear.brakeTorque = 0f;
            FrontGear.motorTorque = 0.0001f;

            BackLeftGear.brakeTorque = 0f;
            BackRightGear.brakeTorque = 0f;

            BackLeftGear.motorTorque = 0.0001f;
            BackRightGear.motorTorque = 0.0001f;
        }

        // Плавна зміна кута самого фізичного WheelCollider, щоб не було ударного імпульсу в PhysX
        _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, targetSteer, steerSpeed * Time.fixedDeltaTime);
        FrontGear.steerAngle = _currentSteerAngle;
    }
}