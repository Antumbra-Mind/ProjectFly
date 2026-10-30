using Unity.Mathematics;
using UnityEngine;

public class AircraftPhisics : MonoBehaviour
{
    private Rigidbody rigidbody;
    private AircraftControler controler;
    private AircraftStats stats;
    private float debugTimer;
    private void Start()
    {
        rigidbody.mass = stats.PlaneMass;
    }

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        controler = GetComponent<AircraftControler>();
        stats = GetComponent<AircraftStats>();
    }

    private void FixedUpdate()
     {
        //Швидкість. Реалізація сухої тяги+ прискорення, якщо двигун стоїть на 100%+
        float thotle = controler.throttle;
        float planeEng = stats.PlaneEnginePower;
        float planeAftb = stats.Afterburner;
        float forceAmount = 0f;

        float wingA = stats.WingArea;
        float baseDc = stats.BaseDragCoefficient;
        float airD = stats.AirDensity;
        float airbrake = controler.AirBrake;
        float airbrakepower = stats.AirBrakePower;

        if (thotle <= 100f)
        {
            //Суха тяга
            float dryThrottle = math.clamp(thotle, 0f, 100f);
            forceAmount = (dryThrottle / 100f) * planeEng;
        }
        else
        {
            //Прискорення. Нарешті суха тяга не сумується. А замінюється, якщо значення перевалює за 100%
            float afbProcces = math.clamp(thotle - 100f, 0f, 10f) / 10f;
            forceAmount = math.lerp(planeEng, planeAftb, afbProcces);
        }
        rigidbody.AddForce(transform.forward * forceAmount);
        debugTimer += Time.fixedDeltaTime;

        /*if (debugTimer >= 0.5f)
        {
            debugTimer = 0f;

            Debug.Log(
                $"Speed: {rigidbody.linearVelocity.magnitude:F2} | " +
                $"Velocity: {rigidbody.linearVelocity} | " +
                $"Mass: {rigidbody.mass} | " +
                $"Damping: {rigidbody.linearDamping} | " +
                $"Kinematic: {rigidbody.isKinematic} | " +
                $"Constraints: {rigidbody.constraints}"
            );
        }*/
        //Частина коду для застування опора повітря. 
        Vector3 velocity = rigidbody.linearVelocity;
        float speed = velocity.magnitude;
        if(speed > 0.01f)
        {
            float DinamicPressure = 0.5f * airD * speed * speed;
            if (airbrake > 0.5f) {
                baseDc += airbrakepower;
            }
            float dragforceMag = DinamicPressure * wingA * baseDc;

            Vector3 dragForce = -velocity.normalized * dragforceMag;
            rigidbody.AddForce(dragForce);
        }
     }

}
