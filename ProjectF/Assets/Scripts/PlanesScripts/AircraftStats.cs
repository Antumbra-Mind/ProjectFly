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
    public float _MaxYawAngVel;
    public float _MaxPitchUpAngVel;
    public float _MaxPitchDownAngVel;
    public float _MaxRollAngVel;
    public float LandGearTimeReveal;
    public float BaseDragCoefficient = 0.02f;
    public float WingArea;
    public float WingSpan;
    public float AirDensity = 1.225f;
    public float OswaldEfficiency;
    public AnimationCurve LiftCoefficientCurve;
    public float HigtAOADraft = 0.3f;
    public float ReverseFlowDrag = 1.5f;
    public float KpPitch;
    public float KpRoll;
    public float KpYaw;
    public float KdPitch;
    public float KdRoll;
    public float KdYaw;


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
                WingSpan = 8.6f;
                OswaldEfficiency = 0.75f;
                AirBrakePower = 0.3f; //Сила повітряного тормозу
                GroundBrakePower = 10f; //Сила Наземного тормощу        
                _MaxYawAngVel = 30f;
                _MaxPitchUpAngVel = 40f;
                _MaxPitchDownAngVel = 20f;
                _MaxRollAngVel = 50f;
                KpPitch = 8f;
                KpRoll = 10f;
                KpYaw = 5f;
                KdPitch = 2f;
                KdRoll = 2.5f;
                KdYaw = 1.5f;
                LandGearTimeReveal = 10f; //Швидкість за яку шасі піднімуться/сховаються
                //Люба характеристика де 10f-затичка.

                break;
            /*case AircraftModel.EuroFighterTyphoon:
                PlaneMass = 11000f;
                PlaneMaxSpeed = 2470f / 3.6f;
                PlaneEnginePower = 120000f;
                Afterburner = 180000f;
                WingArea = 51.2f;
                WingSpan = 10.95f;
                OswaldEfficiency = 0.72f;
                AirBrakePower = 0.5f; 
                GroundBrakePower = 10f;
                YawSpeed = 10f;
                PitchSpeed = 10f;
                RollSpeed = 10f;
                LandGearTimeReveal = 10f;
                break;*/
        }
    }

}
