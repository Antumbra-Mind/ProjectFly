using Unity.Mathematics;
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
    public float lookSensitivity = 0.1f;
    public float minPitch = -40f;
    public float maxPitch = 60f;
    private float currentPitch = 0f;
    private float currentYaw = 0f;
    private Vector3 lastFlatForward = Vector3.forward;
    private float lookAtHeight = 1.5f;
    private Vector2 lookDelta;
    private Vector3 lookPoint;
    private Vector3 desiredPosition;

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
    public float maxFOV = 65f;
    public float effectStartSpeed = 50f;
    public float effectMaxSpeed = 200f;
    public float fovSmoothSpeed = 1f;
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
        if (cam != null)
        {
            cam.fieldOfView = normalFOV;
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
                StandartCameraView();
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

    private void StandartCameraView()
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
            Vector3 localLookDirection = new Vector3(-offset.x, lookAtHeight - offset.y, -offset.z);
            Quaternion localRotation = Quaternion.LookRotation(localLookDirection, Vector3.up);
            Quaternion desiredRotation = target.rotation * localRotation;
            float rotT = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }
    }

    private void FullCameraView()
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


    private void UpdateSpeedEffect()
    {
            if (cam == null || rigitbody == null) return;
            float speed = rigitbody.linearVelocity.magnitude;
            speedEffectFactor = Mathf.InverseLerp(effectStartSpeed, effectMaxSpeed, speed);
            float targetFOV = Mathf.Lerp(normalFOV, maxFOV, speedEffectFactor);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView,targetFOV,1f - Mathf.Exp(-fovSmoothSpeed * Time.deltaTime));
        
    }

    private void UpdateMotionBlur()
    {
        //БЛЯЯЯЯЯЯЯЯЯЯЯЯЯЯ
    }
    private Quaternion GetFreeLookBasis()
    {
        if (curentCameraMod == CameraMod.FullTraking)return target.rotation;
        Vector3 flatForward =Vector3.ProjectOnPlane(target.forward, Vector3.up);

        if (flatForward.sqrMagnitude > 0.001f)
        {
            flatForward.Normalize();
            lastFlatForward = flatForward;
        }
        else
        {
            flatForward = lastFlatForward;
        }

        return Quaternion.LookRotation(flatForward, Vector3.up);
    }

    private Vector3 GetFreeLookPoint()
    {
        if (curentCameraMod == CameraMod.FullTraking)
        return target.position + target.up * lookAtHeight;
        return target.position + Vector3.up * lookAtHeight;
    }
    private void BeginFreeLook()
    {
        if (target == null) return;
        Quaternion basis = GetFreeLookBasis();
        Vector3 lookPoint = GetFreeLookPoint();
        Vector3 cameraOffset =Quaternion.Inverse(basis) * (transform.position - lookPoint);
        currentCameraDistance = cameraOffset.magnitude;
        if (currentCameraDistance <= 0.001f) return;
        currentYaw = Mathf.Atan2(cameraOffset.x, -cameraOffset.z) * Mathf.Rad2Deg;
        currentPitch = Mathf.Asin(Mathf.Clamp(cameraOffset.y / currentCameraDistance,-1f, 1f)) * Mathf.Rad2Deg;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    private void FreeLookCamera()
    {
        if (target == null || Ainput == null) return;

        Vector2 camLook = Ainput.CameraLook;

        currentYaw -= camLook.x * lookSensitivity;
        currentPitch -= camLook.y * lookSensitivity;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        Quaternion basis = GetFreeLookBasis();
        Vector3 lookPoint = GetFreeLookPoint();

        float yawRad = currentYaw * Mathf.Deg2Rad;
        float pitchRad = currentPitch * Mathf.Deg2Rad;

        float x = Mathf.Sin(yawRad) * Mathf.Cos(pitchRad);
        float y = Mathf.Sin(pitchRad);
        float z = -Mathf.Cos(yawRad) * Mathf.Cos(pitchRad);

        Vector3 offsetDir = new Vector3(x, y, z);

        Vector3 desiredPosition =lookPoint + basis * (offsetDir * currentCameraDistance);

        float posT = 1f - Mathf.Exp(-positionSmoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);
        Vector3 lookDirection = lookPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, basis * Vector3.up);
            float rotT = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }
    }

    private void EndFreeLook()
    {

    }


}
