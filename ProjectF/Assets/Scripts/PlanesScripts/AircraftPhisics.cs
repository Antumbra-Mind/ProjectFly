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

        //Суха тяга
        float dryThrottle = math.clamp(thotle, 0f, 100f);
        float fryTrust = (dryThrottle / 100f) * planeEng;
        //Прискоренна на 100-110%
        float AfterBurn = math.clamp(thotle - 100f, 0f, 10f);
        float AfterBurntrust = (AfterBurn / 10f) * planeAftb;
        //Сумуємо.
        float forceAmount = fryTrust + AfterBurntrust;
        //ну і єбашимо в addForce
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
