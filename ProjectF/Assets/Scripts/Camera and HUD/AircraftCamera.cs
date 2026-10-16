using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AircraftCamera : MonoBehaviour
{
    [Header("Камера")]
    [SerializeField] private Transform target;
    private AircraftInput Ainput;
    private AircraftStats stats;
    private Rigidbody rigitbody;
    public Camera cam;
    public Volume speedVolume;
    private MotionBlur motionBlur;

    [Header("Налаштування")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -10f);

    public float positionSmoothSpeed = 5f;
    public float rotationSmoothSpeed = 5f;
    public float lookSensitivity = 2f;
    public float minPitch = -40f;
    public float maxPitch = 60f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private Vector3 lastFlatForward = Vector3.forward;
    private float lookAtHeight = 1.5f;

    public enum CameraMod
    {
        Standart,
        FullTraking
    }
    [Header("Камерамод")]
    public CameraMod curentCameraMod = CameraMod.Standart;
    public bool previousCameraSwitch;
    public bool priviousCameraHold;
    public float freeLookYaw;
    public float currentCameraDistance;
    [Header("FOV")]
    public float normalFOV = 60f;
    public float maxFOV = 85f;
    public float effectStartSpeed = 50f;
    public float effectMaxSpeed = 200f;
    public float fovSmoothSpeed = 5f;
    [Header("Стан ефекту")]
    public float speedEffectFactor = 0f;


    private void Awake()
    {
        Ainput = target.GetComponent<AircraftInput>();
        rigitbody = target.GetComponent<Rigidbody>();
        stats = target.GetComponent<AircraftStats>();

        if(cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (target == null) Debug.LogError("AircraftCamera: Не знайдено кореневий об'єкт літака (target)!", this);
        if (Ainput == null) Debug.LogError("AircraftCamera: На кореневому об'єкті літака відсутній компонент AircraftInput!", this);
        if (rigitbody == null) Debug.LogError("AircraftCamera: На кореневому об'єкті літака відсутній компонент Rigidbody!", this);
        if (stats == null) Debug.LogError("AircraftCamera: На кореневому об'єкті літака відсутній компонент AircraftStats!", this);
        if (cam == null) Debug.LogError("AircraftCamera: Не знайдено компонент Camera!", this);
        curentCameraMod = CameraMod.Standart;

        if(Ainput != null)
        {
            previousCameraSwitch = Ainput.CameraWatchSwitch;
            priviousCameraHold = Ainput.CameraHold;
        }
        if (cam != null) {
            normalFOV = cam.fieldOfView;
        }
        currentCameraDistance = offset.magnitude;
        freeLookYaw = 0f;
        currentPitch = 0f;
        currentYaw = 0f;

        Vector3 initForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if(initForward.sqrMagnitude > 0.001f) { lastFlatForward = initForward.normalized; }
    }

    private void Update()
    {
        if (Ainput != null)
        {
            if (Ainput.CameraWatchSwitch != previousCameraSwitch) //Перевірка режиму камери
            {
                curentCameraMod = (curentCameraMod == CameraMod.Standart) ? CameraMod.FullTraking : CameraMod.Standart;
                previousCameraSwitch = Ainput.CameraWatchSwitch;
            }


            if (Ainput.CameraHold != priviousCameraHold)
            {
                if (Ainput.CameraHold)
                {
                    BeginFreeLook();
                }
                else
                {
                    EndFreeLook();
                }
                priviousCameraHold = Ainput.CameraHold;
            }
        }
        UpdateSpeedEffect();
        UpdateMotionBlur();
    }
    private void LateUpdate()
    {
        if (target == null || Ainput == null) return;
        if (Ainput.CameraHold)
        {
            FreeLookCamera();
        }
        else {
            if (curentCameraMod == CameraMod.Standart)
            {
                CameraView();
            }
            else if(curentCameraMod == CameraMod.FullTraking)
            {
                FullCameraView();
            }
        }


       /* Vector3 targetPosition = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target); */
    }

    private void ChangeCameraView()
    {
        if (curentCameraMod == CameraMod.Standart)
        {
            curentCameraMod = CameraMod.FullTraking;
        }
        else
        {
            curentCameraMod = CameraMod.Standart;
        }
    }
    
    private void CameraView()
    {
        if (target == null) return;

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);

        if (flatForward.sqrMagnitude > 0.001f)
        {
            flatForward.Normalize();
            lastFlatForward = flatForward;
        }
        else
        {
            flatForward = lastFlatForward;
        }
        Quaternion flatRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Vector3 desiredPosition = target.position + flatRotation * offset;
        float posT = 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);
        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        Vector3 lookDirection = lookPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotT = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }
    }
    private void FullCameraView()
    {
        if (target == null) return;
        //Розрахунок позиції
        Vector3 desiredPosition = target.position + target.rotation * offset;
        float posT = 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);
        //розрахунок повороту
        Vector3 lookPoint = target.position + target.up * lookAtHeight;

        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, target.up);
            float rotT = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }
    }

    private void UpdateSpeedEffect()
    {
        //бляяяяя....
    }

    private void UpdateMotionBlur()
    {
        //БЛЯЯЯЯЯЯЯЯЯЯЯЯЯЯ
    }

    private void FreeLookCamera()
    {

    }

    private void EndFreeLook()
    {

    }

    private void BeginFreeLook()
    {

    }
}
