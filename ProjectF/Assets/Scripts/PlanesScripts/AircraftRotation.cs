
using UnityEngine;

public class AircraftRotation : MonoBehaviour
{
    private Rigidbody rb;
    private AircraftStats stats;
    private AircraftControler controler;
    private float targetPitch;
    private float targetRoll;
    private float targetYaw;
    private float Pitch;
    private Vector3 _ErrorDiferrence;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<AircraftStats>();
        controler = GetComponent<AircraftControler>();
    }

    private void FixedUpdate()
    {
        if (rb == null || stats == null || controler == null) return;

        Pitch = controler.Pitch;

        if (Pitch > 0f)
        {
            targetPitch = stats._MaxPitchUpAngVel * Pitch;
        }
        else
        {
            targetPitch = stats._MaxPitchDownAngVel * Pitch;
        }

        targetRoll = stats._MaxRollAngVel * controler.Roll;
        targetYaw = stats._MaxYawAngVel * controler.Yaw;

        targetPitch *= Mathf.Deg2Rad;
        targetRoll *= Mathf.Deg2Rad;
        targetYaw *= Mathf.Deg2Rad;

        // Бажані кутові швидкості в локальних координатах
        Vector3 _AllTarget = new Vector3(targetPitch, targetYaw, -targetRoll);

        // Поточна кутова швидкість у локальних координатах
        Vector3 _LocalAngVel = transform.InverseTransformDirection(rb.angularVelocity);
        float currentPitch = _LocalAngVel.x;
        float currentYaw = _LocalAngVel.y;
        float currentRoll = _LocalAngVel.z;
        _ErrorDiferrence = _AllTarget - _LocalAngVel;

        // PD-контролери
        float pitchAcceleration = CalculatePD(targetPitch, currentPitch, stats.KpPitch, stats.KdPitch);
        float RollAcceleration = CalculatePD(-targetRoll, currentRoll, stats.KpRoll, stats.KdRoll);
        float YawAcceleration = CalculatePD(targetYaw, currentYaw, stats.KpYaw, stats.KdYaw);

        // Бажане кутове прискорення
        Vector3 RotationAceleration = new Vector3(
            pitchAcceleration, YawAcceleration, RollAcceleration);

        // Перехід у систему головних осей інерції
        Vector3 inertiaAcceleration =Quaternion.Inverse(rb.inertiaTensorRotation) * RotationAceleration;

        // Розрахунок крутного моменту
        Vector3 Torque = Vector3.Scale(inertiaAcceleration, rb.inertiaTensor);

        // Повернення в локальні координати
        Vector3 localTorque = rb.inertiaTensorRotation * Torque;

        // Перехід у світові координати
        Vector3 UniversaryTorque = transform.TransformDirection(localTorque);

        // Застосування крутного моменту
        rb.AddTorque(UniversaryTorque, ForceMode.Force);
    }

    float CalculatePD(float target, float current, float Kp, float Kd)
    {
        float AnglularAcceleration = Kp * (target - current) - Kd * current;
        return AnglularAcceleration;
    }
}
