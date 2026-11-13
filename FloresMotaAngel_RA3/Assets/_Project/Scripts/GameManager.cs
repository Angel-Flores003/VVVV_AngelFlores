using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    private AudioSource audioSource;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlaySound(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.Play();
    }
}