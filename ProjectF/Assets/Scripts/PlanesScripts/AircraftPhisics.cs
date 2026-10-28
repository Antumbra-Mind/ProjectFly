using Unity.Mathematics;
using UnityEngine;

public class AircraftPhisics : MonoBehaviour
{
    private Rigidbody rigidbody;
    private AircraftControler controler;
    private AircraftStats stats;

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
        float airResist = stats.AirRessistanse;
        float forceAmount = 0f;

        if(thotle <= 100f)
        {
            //Суха тяга
            float dryThrottle = math.clamp(thotle, 0f, 100f);
            forceAmount = (dryThrottle / 100f) * planeEng;
        }
        else
        {
            //Прискорення. Нарешті суха тяга не сумується. А замінюється, якщо значення перевалює за 100%
            float afbProcces = math.clamp(thotle - 100f, 0f, 10f) / 10f;
            forceAmount = math.lerp(planeEng, Afterburner, afbProcces);
        }
        rigidbody.AddForce(transform.forward * forceAmount);

        //Частина коду для застування опора повітря. Да все настільки по йобнутому
        Vector3 velocity = rigidbody.linearVelocity;
        float speed = velocity.magnitude;
        if(speed > 0.01f)
        {
            float speed2 = speed * speed;
            float DragForceMagnitude = speed2 * airResist;
            Vector3 dragForce = -velocity.normalized * DragForceMagnitude;
            rigidbody.AddForce(dragForce);
        }
     }

}
