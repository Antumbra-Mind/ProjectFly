using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCardActions : MonoBehaviour
{
    FPSTarget fpsTarget = new FPSTarget();
    // 1. Дія для картки DEMO (завантаження ігрової сцени)
    public void OnDemoClicked()
    {
        Debug.Log("Клік по DEMO");
        // Впиши точну назву твоєї ігрової сцени (вона має бути додана в File -> Build Profiles / Build Settings)
        fpsTarget.SetMainMenuState(false);
        SceneManager.LoadScene("MainScene");
    }
    public void ExitGame()
    {
        Debug.Log("Клік по ВИХІД");
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}