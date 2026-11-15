using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

public class LifeLose : MonoBehaviour
{
    [SerializeField] private int startingLifes = 3;
    [SerializeField] private AudioClip deadSound;


    [Header("UI")]
    [SerializeField] private TMP_Text lifesText;
    private int lifes;

    public bool IsGameOver { get; private set; }

    private void Start()
    {
        lifes = startingLifes;
        IsGameOver = false;
        UpdateUI();
    }
    private void OnEnable() => GameEvents.OnLoseLife += LoseLife;
    private void OnDisable() => GameEvents.OnLoseLife -= LoseLife;

    private void LoseLife(int life)
    {
        lifes -= life;
        UpdateUI();
        SoundManager.Instance.PlaySound(deadSound);

        if (lifes <= 0)
        {
            GameOver();
        }
    }
    private void UpdateUI()
    {
        if (lifes < 0)
            lifesText.text = "Vides: 0";
        lifesText.text = "Vides: " + lifes;
    }

    private void GameOver()
    {
        SceneManager.LoadScene("Menu_Dead");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}