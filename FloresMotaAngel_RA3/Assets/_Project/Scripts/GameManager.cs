using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private GameObject[] checkPoints;
    [SerializeField] private GameObject playerPrefab;

    private GameObject currentPlayer;
    private int indexCheckPoints;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
            
            checkPoints = GameObject.FindGameObjectsWithTag("CheckPoints");
            indexCheckPoints = PlayerPrefs.GetInt("IndexPoints", 0);
            indexCheckPoints = Mathf.Clamp(indexCheckPoints, 0, checkPoints.Length - 1);

            SpawnPlayer();
        }
        else 
            Destroy(gameObject);
    }

    private void Update()
    {
        if (currentPlayer == null)
        {
            GameEvents.ZoneChange(0);
            SpawnPlayer();
        }
    }

    private void SpawnPlayer()
    {
        Vector3 spawnPosition = checkPoints[indexCheckPoints].transform.position;
        currentPlayer = Instantiate( playerPrefab, spawnPosition, Quaternion.identity);

        Parallax[] parallaxes = FindObjectsByType<Parallax>(FindObjectsSortMode.None);

        foreach (Parallax parallax in parallaxes)
        {
            parallax.SetPlayer(currentPlayer.transform);
        }
    }

    public void LastCheckPoint(GameObject checkPoint)
    {
        for (int i = 0; i < checkPoints.Length; i++)
        {
            if (checkPoints[i] == checkPoint)
            {
                indexCheckPoints = i;
                PlayerPrefs.SetInt("IndexPoints", i);
                PlayerPrefs.Save();
            }
        }
    }
}