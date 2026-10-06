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

    public void Update()
    {
       pitchspeed = stats.PitchSpeed;
       rollspeed = stats.RollSpeed;
       yawspeed = stats.YawSpeed;
       pitch = controler.Pitch;
       roll = controler.Roll;
       yaw = controler.Yaw;

    }


    private void FixedUpdate()//Тут все-Повороти по осям.
    {
        float pitchAngle = pitch * pitchspeed * Time.fixedDeltaTime;
        float yawAngle = yaw * yawspeed * Time.fixedDeltaTime;
        float rollAngle = roll * rollspeed * Time.fixedDeltaTime;

        Quaternion deltaRotation = Quaternion.Euler(pitchAngle, yawAngle, rollAngle);
        Quaternion Rotation = rb.rotation * deltaRotation;

        rb.MoveRotation(Rotation);

    }
}
