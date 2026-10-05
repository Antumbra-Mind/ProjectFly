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
    private float PlaneMass;
    private float PlaneMaxSpeed;
    private float PlaneEnginePower;
    private float Afterburner;
    private float AirRessistanse;
    private float AirBrakePower;
    private float GroundBrakePower;
    private float YawSpeed;
    private float PitchSpeed;
    private float RollSpeed;
    private float LandGearTimeReveal;

    private void Awake()
    {
        switch (selectModel)
        {
            case AircraftModel.GripenE:
                PlaneMass = 8000f;
                PlaneMaxSpeed = 2470f;
                PlaneEnginePower = 64000f;//Потужність двигуна
                Afterburner = 98000f;//Потужність двигуна на максимумі=Афтербьорнер
                AirRessistanse = 10f; //Це ще розбирати і розбирати... Супротив Повітря.
                AirBrakePower = 10f; //Це ще розбирати і розбирати...
                GroundBrakePower = 10f;
                YawSpeed = 10f;
                PitchSpeed = 10f;
                RollSpeed = 10f;
                LandGearTimeReveal = 10f;
                //Люба характеристика де 10f-затичка.

                break;
            case AircraftModel.EuroFighterTyphoon:
                PlaneMass = 11000f;
                PlaneMaxSpeed = 2470f;
                PlaneEnginePower = 120000f;
                Afterburner = 180000f;
                AirRessistanse = 10f; 
                AirBrakePower = 10f; 
                GroundBrakePower = 10f;
                YawSpeed = 10f;
                PitchSpeed = 10f;
                RollSpeed = 10f;
                LandGearTimeReveal = 10f;
                break;
        }
    }

}
