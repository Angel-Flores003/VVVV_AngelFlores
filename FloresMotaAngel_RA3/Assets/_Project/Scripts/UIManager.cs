using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Scene1");
    }

    public void ExitGame()
    {
        Debug.Log("Salir del juego");
        Application.Quit();
    }
}