using UnityEngine;

    public class GripenWheelSync : MonoBehaviour
    {
        private GripenRigController _rigController;
        private float _currentVisualSteerAngle;
        private Coroutine _gearColliderRoutine;

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
            SetCollidersEnabled(false);
        }
        else
        {
            // При випуску чекаємо повного завершення анімації
            _gearColliderRoutine = StartCoroutine(EnableCollidersAfterAnimation());
        }
    }

    private System.Collections.IEnumerator EnableCollidersAfterAnimation()
    {
        // Даємо Animator один кадр на перехід у стейт випуску шасі
        yield return null;

        Animator anim = _rigController.GetComponent<Animator>();
        if (anim != null)
        {
            // Чекаємо, поки триває перехід (Transition) або сам кліп ще не дійшов до кінця (normalizedTime < 1)
            while (anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
            {
                if (_rigController._gearRetracted) yield break;
                yield return null;
            }
        }
        else
        {
            // Фолбек за часом, якщо Animator на іншому об'єкті (підстав тривалість своєї анімації)
            yield return new WaitForSeconds(2.0f);
        }

        if (!_rigController._gearRetracted)
        {
            SetCollidersEnabled(true);
        }
        _gearColliderRoutine = null;
    }

    private void SetCollidersEnabled(bool state)
    {
        foreach (var strut in _rigController.struts)
        {
            if (strut.collider != null)
            {
                strut.collider.enabled = state;
            }
        }
    }
}