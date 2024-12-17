using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{

    public void PlayGame()
    {
        SceneManager.LoadScene("MainScene");
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void OpenWebsite()
    {
        Application.OpenURL("https://jamiesweeting.github.io/index.html");
    }

    public void OpenRepo()
    {
        Application.OpenURL("https://github.com/JamieSweeting/CarController");
    }
}