using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class AircraftHUD : MonoBehaviour
{
    public AircraftControler controler;
    public Transform GuffuAhPlane;
    public Slider slide;
    public Image FillSlider;
    public TMP_Text DryForce;
    public TMP_Text Afterburner;
    public TMP_Text Altitude;


    private void Update()
    {
        UpdateThrottleSlider();
        UpdateThrottleText();
        UpdateAltitudeText();
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
}
