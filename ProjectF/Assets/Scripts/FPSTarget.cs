using System;
using UnityEngine;

public class FPSTarget : MonoBehaviour
{
    public bool isMainMenu = true;
    [SerializeField] private int target = 60;

    public event Action OnMenuStateChanged;

    private void Awake()
    {
        ApplyFrameRate();
    }

    private void OnEnable()
    {
        OnMenuStateChanged += ApplyFrameRate;
    }

    private void OnDisable()
    {
        OnMenuStateChanged -= ApplyFrameRate;
    }

    public void SetMainMenuState(bool state)
    {
        isMainMenu = state;
        OnMenuStateChanged?.Invoke();
    }

    private void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = isMainMenu ? target : -1;
    }
    //void Update()
    //{
    //    if (Application.targetFrameRate != target)
    //        Application.targetFrameRate = target;
    //}
}
