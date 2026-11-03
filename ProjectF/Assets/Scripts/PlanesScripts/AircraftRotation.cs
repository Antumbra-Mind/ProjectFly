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
        Pitch = controler.Pitch;
        if(Pitch > 0f)
        {
            targetPitch = stats._MaxPitchUpAngVel * Pitch;
        }
        else
        {
            targetPitch = -stats._MaxPitchDownAngVel * Pitch;
        }

        targetRoll = stats._MaxRollAngVel * controler.Roll;
        targetYaw = stats._MaxYawAngVel * controler.Yaw;

        targetPitch *= Mathf.Deg2Rad;
        targetRoll *= Mathf.Deg2Rad;
        targetYaw *= Mathf.Deg2Rad;

        Vector3 _AllTarget = new Vector3(targetPitch, targetYaw, -targetRoll);
        Vector3 _LocalAngVel = transform.InverseTransformDirection(rb.angularVelocity);
        _ErrorDiferrence = _AllTarget - _LocalAngVel;



        if (rb == null || stats == null || controler == null) return;

        /*float pitchAngle = controler.Pitch* stats.PitchSpeed * Time.fixedDeltaTime;
        float yawAngle = controler.Yaw* stats.YawSpeed * Time.fixedDeltaTime;
        float rollAngle = controler.Roll * stats.RollSpeed * Time.fixedDeltaTime;

        Quaternion deltaRotation = Quaternion.Euler(pitchAngle,yawAngle,-rollAngle);

        rb.MoveRotation(rb.rotation * deltaRotation);*/
    }
}
