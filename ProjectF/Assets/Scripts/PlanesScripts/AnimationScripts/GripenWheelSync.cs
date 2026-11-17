using System.Collections;
using UnityEngine;

public class GripenWheelSync : MonoBehaviour
{
    private GripenRigController _rigController;
    private Rigidbody _rb;
    private float _currentVisualSteerAngle;
    private Coroutine _gearColliderRoutine;

    private void Awake()
    {
        _rigController = GetComponentInParent<GripenRigController>();
        _rb = GetComponentInParent<Rigidbody>();

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
                strut.initialAmoLocalRot = strut.strutAmoBone.localRotation;
            }
            if (strut.wheelSpinBone != null)
            {
                strut.initialWheelLocalRot = strut.wheelSpinBone.localRotation;
            }
        }

        HandleGearStateChanged();
    }

    private void LateUpdate()
    {
        if (_rigController == null || _rigController._gearRetracted || _rigController.struts == null) return;

        for (int i = 0; i < _rigController.struts.Length; i++)
        {
            var strut = _rigController.struts[i];
            if (strut.collider == null || !strut.collider.enabled) continue;

            // 1. Отримуємо реальну позицію колеса з фізики WheelCollider
            strut.collider.GetWorldPose(out Vector3 wheelWorldPos, out _);

            // 2. АМОРТИЗАТОР: зміщуємо кістку на повну величину реального стиснення пружини
            if (strut.strutAmoBone != null)
            {
                Vector3 offsetFromColliderOrigin = wheelWorldPos - strut.collider.transform.position;
                float verticalOffset = Vector3.Dot(offsetFromColliderOrigin, transform.up);

                float compression = Mathf.Clamp(
                    strut.collider.suspensionDistance + verticalOffset,
                    0f,
                    strut.collider.suspensionDistance
                );

                // Якщо в Blender стійка змодельована в розтиснутому стані — використовуємо чистий compression
                Vector3 worldDelta = transform.up * compression;
                if (strut.strutAmoBone.parent != null)
                {
                    Vector3 localDelta = strut.strutAmoBone.parent.InverseTransformVector(worldDelta);
                    strut.strutAmoBone.localPosition = strut.initialAmoLocalPos + localDelta;
                }

                // Плавний поворот лише для передньої стійки (i == 0)
                if (i == 0 && _rigController.frontSteerLocalAxis.sqrMagnitude > 0.0001f)
                {
                    float targetSteer = strut.collider.steerAngle
                        * _rigController.visualSteerMultiplier
                        * (_rigController.invertFrontSteer ? -1f : 1f);

                    _currentVisualSteerAngle = Mathf.Lerp(
                        _currentVisualSteerAngle,
                        targetSteer,
                        Time.deltaTime * _rigController.frontSteerSmoothSpeed
                    );

                    Quaternion steerDelta = Quaternion.AngleAxis(_currentVisualSteerAngle, _rigController.frontSteerLocalAxis.normalized);
                    strut.strutAmoBone.localRotation = strut.initialAmoLocalRot * steerDelta;
                }
            }

            // 3. ОБЕРТАННЯ КОЛЕСА
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

        if (_gearColliderRoutine != null)
        {
            StopCoroutine(_gearColliderRoutine);
            _gearColliderRoutine = null;
        }

        bool retracted = _rigController._gearRetracted;

        // При прибиранні вимикаємо колайдери одразу, щоб не зачепити смугу під час складання
        if (retracted)
        {
            SetCollidersEnabledImmediate(false);
        }
        else
        {
            // При випуску чекаємо повного завершення анімації, а потім вмикаємо по 1 колесу на фізичний кадр
            _gearColliderRoutine = StartCoroutine(EnableCollidersAfterAnimation());
        }
    }

    private IEnumerator EnableCollidersAfterAnimation()
    {
        // Даємо Animator один кадр на перехід у стейт випуску шасі
        yield return null;

        Animator anim = _rigController.gripenAnimator != null
            ? _rigController.gripenAnimator
            : _rigController.GetComponent<Animator>();

        if (anim != null)
        {
            while (anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
            {
                if (_rigController._gearRetracted) yield break;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        if (_rigController._gearRetracted || _rigController.struts == null)
        {
            _gearColliderRoutine = null;
            yield break;
        }

        // Послідовно вмикаємо кожне колесо в окремому фізичному кадрі (FixedUpdate)
        for (int i = 0; i < _rigController.struts.Length; i++)
        {
            if (_rigController._gearRetracted) break;

            var col = _rigController.struts[i].collider;
            if (col == null || col.enabled) continue;

            // Чекаємо початку фізичного кроку
            yield return new WaitForFixedUpdate();

            Vector3 savedVel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 savedAngVel = _rb != null ? _rb.angularVelocity : Vector3.zero;

            col.enabled = true;
            col.brakeTorque = 0f;
            col.motorTorque = 0.0001f;

            // Миттєво відновлюємо швидкість після ініціалізації WheelCollider у PhysX
            if (_rb != null)
            {
                _rb.linearVelocity = savedVel;
                _rb.angularVelocity = savedAngVel;
            }

            // Страхувальна перевірка на наступному кроці симуляції PhysX
            yield return new WaitForFixedUpdate();

            if (_rb != null && _rb.linearVelocity.sqrMagnitude < 0.01f && savedVel.sqrMagnitude > 1f)
            {
                _rb.linearVelocity = savedVel;
                _rb.angularVelocity = savedAngVel;
            }
        }

        _gearColliderRoutine = null;
    }

    private void SetCollidersEnabledImmediate(bool state)
    {
        if (_rigController.struts == null) return;

        foreach (var strut in _rigController.struts)
        {
            if (strut.collider != null)
            {
                strut.collider.enabled = state;
            }
        }
    }
}