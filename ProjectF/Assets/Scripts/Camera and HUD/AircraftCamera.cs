using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AircraftCamera : MonoBehaviour
{
    [Header("Камера")]
    private Transform target;
    private AircraftInput Ainput;
    private AircraftStats stats;
    [SerializeField] private float followSpeed = 5f;
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
        if (transform.root != null)
        {
            target = transform.root;
        }
        else
        {
            target = transform;
        }
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

        Vector3 targetPos = target.position;
        Vector3 flatForward = target.forward;
        flatForward.y = 0f;
        if(flatForward.sqrMagnitude > 0.001f)
        {
            flatForward.Normalize();
        }
        else
        {
            flatForward = target.forward;
        }

        Quaternion flatRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Vector3 desiredPosition = targetPos + flatRotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);
        Vector3 lookTargetPoint = targetPos + Vector3.up * offset.y;
        Vector3 lookDirection = lookTargetPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
    private void FullCameraView()
    {

    }

    private void UpdateSpeedEffect()
    {

    }

    private void UpdateMotionBlur()
    {

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
