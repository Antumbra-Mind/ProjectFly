using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCardActions : MonoBehaviour
{

    // 1. Дія для картки DEMO (завантаження ігрової сцени)
    public void OnDemoClicked()
    {
        Debug.Log("Клік по DEMO");
        // Впиши точну назву твоєї ігрової сцени (вона має бути додана в File -> Build Profiles / Build Settings)
        SceneManager.LoadScene("MainScene");
    }
}