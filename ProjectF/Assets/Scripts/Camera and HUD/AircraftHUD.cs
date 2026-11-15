using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AircraftHUD : MonoBehaviour
{
    public AircraftControler controler;
    public Rigidbody rigitbody;
    public AircraftInput input;
    public AirbrakeController controller;
    public AircraftPhisics phisics;
    public AircraftGroundMovement Gmove;
    public Transform GuffuAhPlane;
    public Slider slide;
    public Image FillSlider;
    public TMP_Text DryForce;
    public TMP_Text Afterburner;
    public TMP_Text Altitude;
    public TMP_Text Speed;
    public TMP_Text Stall;
    public Image AirbrakeImage;
    public Image BrakeImage;
    public Image GearImage;
    public GameObject HUDcontent;
    public bool previousHideHUD;
    

    private void Awake()
    {
        phisics = GuffuAhPlane.GetComponent<AircraftPhisics>();
        controller = GetComponent<AirbrakeController>();
        rigitbody = GuffuAhPlane.GetComponent<Rigidbody>();
        Gmove = GuffuAhPlane.GetComponent<AircraftGroundMovement>();
        if (GuffuAhPlane != null)
        input = GuffuAhPlane.GetComponent<AircraftInput>();
        if (HUDcontent == null) return;
        previousHideHUD = input.HideHUD;
        HUDcontent.SetActive(!input.HideHUD);
    }

    private void Update()
    {
        UpdaterHUDVisible();
        UpdateThrottleSlider();
        UpdateThrottleText();
        UpdateAltitudeText();
        UpdateSpeedText();
        UpdateAirbrakeImage();
        UpdateGearImage();
        UpdateBrakeImage();
        UpdateStallText();
    }
    public void UpdateThrottleSlider()
    {
        float thottle = controler.throttle;
        slide.value = thottle;
        if (thottle <= 30)
        {
            FillSlider.color = Color.green;
        }
        else if (thottle <= 100)
        {
            FillSlider.color = Color.yellow;
        }
        else if (thottle > 100)
        {
            FillSlider.color = Color.crimson;
        }

    }

    public void UpdateThrottleText()
    {
        float thottle = controler.throttle;
        float dry = math.clamp(thottle, 0f, 100f);
        float afb = math.clamp(thottle - 100f, 0f, 10f);
        DryForce.text = $"THR:{dry:F0}%";
        Afterburner.text = $"AFB:{afb * 10:F0}%";

    }

    public void UpdateAltitudeText()
    {
        float altitude = GuffuAhPlane.transform.position.y;
        Altitude.text = $"ALT.:{altitude:F0} M.";
    }

    public void UpdateSpeedText()
    {
        float speed = rigitbody.linearVelocity.magnitude * 3.6f;
        Speed.text = $"SPD.:{speed:F0} Km/h.";
    }

    public void UpdateStallText()
    {
        Stall.enabled = phisics.IsStalling;


    }

    public void UpdateAirbrakeImage()
    {
        float Airbrake = input.Airbrake;
        if (Airbrake == 0)
        {
            AirbrakeImage.color = Color.red;
        }
        else if (Airbrake == 1)
        {
            AirbrakeImage.color = Color.green;
        }
    }

    public void UpdateBrakeImage()
    {
        bool Brake = input.Brake;
        if (Brake == true)
        {
            BrakeImage.color = Color.green;
        }
        else
        {
            BrakeImage.color = Color.red;
        }

    }

    public void UpdateGearImage()
    {
        if (Gmove.IsTransitioning)
        {
            GearImage.color = Color.yellow;
        }
        else if (Gmove.CurrentState == AircraftGroundMovement.AircraftState.Taxi)
        {
            GearImage.color = Color.green;
        }
        else
        {
            GearImage.color = Color.red;
        }
    }
    
    public void UpdaterHUDVisible()
    {
        if (input == null || HUDcontent == null) return;
        bool HUDnow = input.HideHUD;
        if (HUDnow == previousHideHUD) return;

        HUDcontent.SetActive(!HUDnow);
        previousHideHUD = HUDnow;
    }
    

}
