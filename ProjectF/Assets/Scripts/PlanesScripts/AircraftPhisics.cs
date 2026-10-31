using Unity.Mathematics;
using UnityEngine;

public class AircraftPhisics : MonoBehaviour
{
    private Rigidbody rigidbody;
    private AircraftControler controler;
    private AircraftStats stats;
    private float debugTimer;
    private float AR;
    private void Start()
    {
        rigidbody.mass = stats.PlaneMass;
        AR = math.square(stats.WingSpan) / stats.WingArea;
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
        float oswaldEF = stats.OswaldEfficiency;

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

        //Частина коду для застування опора повітря. 
        Vector3 velocity = rigidbody.linearVelocity;
        float speed = velocity.magnitude;
        if(speed > 0.01f)
        {

            //Частина коду Для АОА
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float verticalSpeed = Vector3.Dot(velocity, transform.up);
            //Саме АОА
            float AOA = Mathf.Atan2(-verticalSpeed, forwardSpeed) * Mathf.Rad2Deg;
            //ЙОБАНА КРИВАААА
            float CL = stats.LiftCoefficientCurve.Evaluate(AOA);
            float CoefInductiveDrag = math.square(CL) / (Mathf.PI * oswaldEF * AR);

            //Опір повітрю
            float DinamicPressure = 0.5f * airD * speed * speed;
            if (airbrake > 0.5f) {
                baseDc += airbrakepower;
            }
            float DragCoef = baseDc + CoefInductiveDrag;
            float dragforceMag = DinamicPressure * wingA * baseDc;

            Vector3 dragForce = -velocity.normalized * dragforceMag;
            rigidbody.AddForce(dragForce);


            //І нарешті Підйомна сила. Магнітуда і Напрямок
            float liftForceMag = DinamicPressure * wingA * CL;
            Vector3 liftDirection = Vector3.Cross(velocity.normalized, transform.right).normalized;

            if(liftDirection.sqrMagnitude > 0.0001f)
            {
                //і на добивочку множимо і їбашимо в аддфорс
                Vector3 LiftForce = liftDirection * liftForceMag;
                rigidbody.AddForce(LiftForce);
            }




        }



        

     }

}
