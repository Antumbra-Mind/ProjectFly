using UnityEngine;

public class AircraftRotation : MonoBehaviour
{
    private Rigidbody rb;
    private AircraftStats stats;
    private AircraftControler controler;
    private float pitchspeed;
    private float rollspeed;
    private float yawspeed;
    private float pitch;
    private float roll;
    private float yaw;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<AircraftStats>(); 
        controler = GetComponent<AircraftControler>();
    }

    private void FixedUpdate()
    {
        if (rb == null || stats == null || controler == null) return;

        float pitchAngle = controler.Pitch* stats.PitchSpeed * Time.fixedDeltaTime;
        float yawAngle = controler.Yaw* stats.YawSpeed * Time.fixedDeltaTime;
        float rollAngle = controler.Roll * stats.RollSpeed * Time.fixedDeltaTime;

        Quaternion deltaRotation = Quaternion.Euler(pitchAngle,yawAngle,-rollAngle);

        rb.MoveRotation(rb.rotation * deltaRotation);
    }
}
