using UnityEngine;

public class AircraftCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -10f);
    [SerializeField] private float followSpeed = 5f;

    private void LateUpdate()
    {
        Vector3 targetPosition = target.TransformPoint(offset);

        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        transform.LookAt(target);
    }

}
