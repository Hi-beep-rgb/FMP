using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonScript : MonoBehaviour
{
    [SerializeField] private string Maingame = "Main game";
    [SerializeField] private string MainMenu = "Main Menu";

    public void PlayButtonMethod()
    {
        SceneManager.LoadScene(Maingame);
    }

    public void QuitGameButton()
    {
        Application.Quit();
    }

    public void ExitToMenuButton()
    {
        SceneManager.LoadScene(MainMenu);
    }
}
