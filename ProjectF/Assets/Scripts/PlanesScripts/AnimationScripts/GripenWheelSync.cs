using UnityEngine;

public class GripenWheelSync : MonoBehaviour
{
    [Header("Налаштування руління та гальм")]
    public float maxSteerAngle = 25f;
    public float maxBrakeTorque = 3000f;

    [HideInInspector] public float steerInput;
    [HideInInspector] public bool isBraking;

    private GripenRigController _rigController;

    private void Awake()
    {
        _rigController = GetComponentInParent<GripenRigController>();
        if (_rigController != null)
        {
            _rigController.OnGearStateChanged += HandleGearStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (_rigController != null)
        {
            _rigController.OnGearStateChanged -= HandleGearStateChanged;
        }
    }

    private void Start()
    {
        if (_rigController == null) return;

        foreach (var strut in _rigController.struts)
        {
            if (strut.strutAmoBone != null)
            {
                strut.initialAmoLocalPos = strut.strutAmoBone.localPosition;
            }
            if (strut.wheelSpinBone != null)
            {
                strut.initialWheelLocalRot = strut.wheelSpinBone.localRotation;
            }
        }

        HandleGearStateChanged();
    }

    private void FixedUpdate()
    {
        if (_rigController == null || _rigController._gearRetracted || _rigController.struts == null) return;

        float currentBrake = isBraking ? maxBrakeTorque : 0f;
        // Мікро-імпульс 0.0001f не дає PhysX "приспати" WheelCollider, коли гальма відпущені
        float wakeTorque = isBraking ? 0f : 0.0001f;

        for (int i = 0; i < _rigController.struts.Length; i++)
        {
            var strut = _rigController.struts[i];
            if (strut.collider == null || !strut.collider.enabled) continue;

            // 0-й елемент масиву struts — передня стійка (керується по Yaw)
            if (i == 0)
            {
                strut.collider.steerAngle = steerInput * maxSteerAngle;
                strut.collider.brakeTorque = 0f;
                strut.collider.motorTorque = 0.0001f;
            }
            else
            {
                // 1-й та 2-й елементи — основні задні стійки (гальмування + пробудження)
                strut.collider.brakeTorque = currentBrake;
                strut.collider.motorTorque = wakeTorque;
            }
        }
    }

    private void LateUpdate()
    {
        if (_rigController == null || _rigController._gearRetracted || _rigController.struts == null) return;

        foreach (var strut in _rigController.struts)
        {
            if (strut.collider == null || !strut.collider.enabled) continue;

            // 1. Отримуємо реальну позицію колеса з фізики WheelCollider
            strut.collider.GetWorldPose(out Vector3 wheelWorldPos, out _);

            // 2. АМОРТИЗАТОР: рахуємо відхилення від номінального просідання (Target Position)
            if (strut.strutAmoBone != null)
            {
                Vector3 offsetFromColliderOrigin = wheelWorldPos - strut.collider.transform.position;
                float verticalOffset = Vector3.Dot(offsetFromColliderOrigin, transform.up);

                float compression = Mathf.Clamp(
                    strut.collider.suspensionDistance + verticalOffset,
                    0f,
                    strut.collider.suspensionDistance
                );

                float targetCompression = strut.collider.suspensionDistance * strut.collider.suspensionSpring.targetPosition;
                float deltaCompression = compression - targetCompression;

                Vector3 worldDelta = transform.up * deltaCompression;
                if (strut.strutAmoBone.parent != null)
                {
                    Vector3 localDelta = strut.strutAmoBone.parent.InverseTransformVector(worldDelta);
                    strut.strutAmoBone.localPosition = strut.initialAmoLocalPos + localDelta;
                }
            }

            // 3. ОБЕРТАННЯ КОЛЕСА: накопичуємо кут, щоб Animator не скидав обертання щокадру
            if (strut.wheelSpinBone != null)
            {
                float degreesPerSecond = strut.collider.rpm * 6f * (strut.invertSpin ? -1f : 1f);
                strut.currentSpinAngle = (strut.currentSpinAngle + degreesPerSecond * Time.deltaTime) % 360f;

                Quaternion spinDelta = Quaternion.AngleAxis(strut.currentSpinAngle, strut.spinLocalAxis.normalized);
                strut.wheelSpinBone.localRotation = strut.initialWheelLocalRot * spinDelta;
            }
        }
    }

    private void HandleGearStateChanged()
    {
        if (_rigController == null) return;

        bool retracted = _rigController._gearRetracted;
        foreach (var strut in _rigController.struts)
        {
            if (strut.collider != null)
            {
                strut.collider.enabled = !retracted;
            }
        }
    }
}