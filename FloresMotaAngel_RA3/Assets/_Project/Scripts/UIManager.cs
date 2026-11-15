using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Scene1");
    }

    public void ExitGame()
    {
        Debug.Log("Salir del juego");
        Application.Quit();
    }

    public void PlayandChange()
    {
        StartCoroutine(ChangeScene());
    }

    private IEnumerator ChangeScene()
    {
        audioSource.Play();

        yield return new WaitForSecondsRealtime(audioSource.clip.length);

        SceneManager.LoadScene("Scene1");
    }
}