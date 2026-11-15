using UnityEngine;

public class Spike : MonoBehaviour
{
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.transform.CompareTag("Player"))
        {
            GameEvents.LoseLife(1);//Avisa
            Destroy(other.gameObject);
        }
    }
}