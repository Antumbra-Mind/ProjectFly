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
        //Сама швидкість... тільки як би прибрати можливість їхати назад.
        float thotle = controler.Thottle;
        float planeEng = stats.PlaneEnginePower;
        float airResist = stats.AirRessistanse;
        float forceAmount = thotle * planeEng;
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
