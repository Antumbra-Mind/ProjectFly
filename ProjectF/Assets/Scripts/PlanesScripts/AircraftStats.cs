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
    public float AirRessistanse;
    public float AirBrakePower;
    public float GroundBrakePower;
    public float YawSpeed;
    public float PitchSpeed;
    public float RollSpeed;
    public float LandGearTimeReveal;

    private void Awake()
    {
        switch (selectModel)
        {
            case AircraftModel.GripenE:
                PlaneMass = 8000f;
                PlaneMaxSpeed = 2470f; //км на годину. Далі треба буде перевести в Метри на секунду ig
                PlaneEnginePower = 64000f;//Потужність двигуна
                Afterburner = 98000f;//Потужність двигуна на максимумі=Афтербьорнер
                AirRessistanse = 10f; //Це ще розбирати і розбирати... Супротив Повітря.
                AirBrakePower = 10f; //Це ще розбирати і розбирати...
                GroundBrakePower = 10f;
                YawSpeed = 30f;
                PitchSpeed = 30f;
                RollSpeed = 30f;
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
