using UnityEngine;

public class SearchLightHaloEffect : MonoBehaviour
{
    private Camera _cam;
    private void Start() => _cam = Camera.main;
    private void LateUpdate()
    {
        if (_cam) transform.LookAt(_cam.transform);
    }
}
