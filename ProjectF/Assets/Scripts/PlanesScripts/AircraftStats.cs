using System.Collections;
using UnityEngine;

public class AircraftStats : MonoBehaviour
{

    public enum AircraftModel
    {
        GripenE,
        EuroFighterTyphoon
    }

    [SerializeField] private AircraftModel selectModel;
    public float PlaneMass;
    public float PlaneMaxSpeed;
    public float PlaneEnginePower;
    public float Afterburner;
    public float AirBrakePower;
    public float GroundBrakePower;
    public float YawSpeed;
    public float PitchSpeed;
    public float RollSpeed;
    public float LandGearTimeReveal;
    public float BaseDragCoefficient = 0.02f;
    public float WingArea;
    public float AirDensity = 1.225f;

    private void Awake()
    {
        switch (selectModel)
        {
            case AircraftModel.GripenE:
                PlaneMass = 8000f;
                PlaneMaxSpeed = 2470f / 3.6f; //Метри на секунду
                PlaneEnginePower = 64000f;//Потужність двигуна
                Afterburner = 98000f;//Потужність двигуна на максимумі=Афтербьорнер
                WingArea = 30f;     //Розмах крила в метрах квадратних
                AirBrakePower = 0.3f; //Сила повітряного тормозу
                GroundBrakePower = 10f; //Сила Наземного тормощу        
                YawSpeed = 40f;
                PitchSpeed = 40f;
                RollSpeed = 50f;
                LandGearTimeReveal = 10f; //Швидкість за яку шасі піднімуться/сховаються
                //Люба характеристика де 10f-затичка.

                break;
            case AircraftModel.EuroFighterTyphoon:
                PlaneMass = 11000f;
                PlaneMaxSpeed = 2470f / 3.6f;
                PlaneEnginePower = 120000f;
                Afterburner = 180000f;
                WingArea = 51.2f;
                AirBrakePower = 0.5f; 
                GroundBrakePower = 10f;
                YawSpeed = 10f;
                PitchSpeed = 10f;
                RollSpeed = 10f;
                LandGearTimeReveal = 10f;
                break;
        }
    }

}
