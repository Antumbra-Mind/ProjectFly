
using System;
using Unity.Mathematics;
using UnityEngine;

public class AircraftRotation : MonoBehaviour
{
    private Rigidbody rigitbody;
    private AircraftStats stats;
    private AircraftControler controler;
    private AircraftPhisics phisics;
    private AircraftGroundMovement groundMove;
    private float targetPitch;
    private float targetRoll;
    private float targetYaw;
    private float Pitch;
    private Vector3 _ErrorDiferrence;
    private float angle;
    private float T;
    private float controlEffectiv = 1f;
    private float stallEffect = 0.6f;


    private void Awake()
    {
        groundMove = GetComponent<AircraftGroundMovement>();
        phisics = GetComponent<AircraftPhisics>();
        rigitbody = GetComponent<Rigidbody>();
        stats = GetComponent<AircraftStats>();
        controler = GetComponent<AircraftControler>();
    }

    private void FixedUpdate()
    {
        if (rigitbody == null || stats == null || controler == null) return;

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


        if (groundMove.IsGrounded)
        {
            targetRoll = 0f;
            targetYaw = 0f;
            targetPitch *= 0.8f;
        }

        targetPitch *= Mathf.Deg2Rad;
        targetRoll *= Mathf.Deg2Rad;
        targetYaw *= Mathf.Deg2Rad;
        //частина кода для погіршення контроля при більшому angle
        if (rigitbody.linearVelocity.sqrMagnitude > 0.01f) { 
            angle = Vector3.Angle(transform.forward, rigitbody.linearVelocity.normalized);
        }
        if(angle <= 90)
        {
            T = Mathf.InverseLerp(30f,90f,angle);
            controlEffectiv = math.lerp(1.0f,0.4f,T);
        }
        else
        {
            T = Mathf.InverseLerp(90f, 180f, angle);
            controlEffectiv = math.lerp(0.4f, 0.05f, T);
        }
        if(phisics.IsStalling == true)
        {
            controlEffectiv *= stallEffect;
        }
        // Бажані кутові швидкості в локальних координатах
        Vector3 _AllTarget = new Vector3(targetPitch, targetYaw, -targetRoll);

        // Поточна кутова швидкість у локальних координатах
        Vector3 _LocalAngVel = transform.InverseTransformDirection(rigitbody.angularVelocity);
        float currentPitch = _LocalAngVel.x;
        float currentYaw = _LocalAngVel.y;
        float currentRoll = _LocalAngVel.z;
        _ErrorDiferrence = _AllTarget - _LocalAngVel;
        //Шассі

        // PD-контролери
        float pitchAcceleration = CalculatePD(targetPitch, currentPitch,controlEffectiv, stats.KpPitch, stats.KdPitch);
        float RollAcceleration = CalculatePD(-targetRoll, currentRoll, controlEffectiv, stats.KpRoll, stats.KdRoll);
        float YawAcceleration = CalculatePD(targetYaw, currentYaw, controlEffectiv, stats.KpYaw, stats.KdYaw);

        // Бажане кутове прискорення
        Vector3 RotationAceleration = new Vector3(
            pitchAcceleration, YawAcceleration, RollAcceleration);

        // Перехід у систему головних осей інерції
        Vector3 inertiaAcceleration =Quaternion.Inverse(rigitbody.inertiaTensorRotation) * RotationAceleration;

        // Розрахунок крутного моменту
        Vector3 Torque = Vector3.Scale(inertiaAcceleration, rigitbody.inertiaTensor);

        // Повернення в локальні координати
        Vector3 localTorque = rigitbody.inertiaTensorRotation * Torque;

        // Перехід у світові координати
        Vector3 UniversaryTorque = transform.TransformDirection(localTorque);

        // Застосування крутного моменту
        rigitbody.AddTorque(UniversaryTorque, ForceMode.Force);


    }

    float CalculatePD(float target, float current,float controlEffectiv, float Kp, float Kd)
    {
        float AnglularAcceleration = Kp * (target - current) * controlEffectiv - Kd * current;
        return AnglularAcceleration;
    }
}
