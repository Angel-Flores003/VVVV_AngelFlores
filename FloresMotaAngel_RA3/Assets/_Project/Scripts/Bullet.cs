using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private TimedObject timedObject;

    private void Awake()
    {
        timedObject = GetComponent<TimedObject>();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            GameEvents.LoseLife(1);
            Destroy(other.gameObject);
        }

        timedObject.ReturnToPool();
    }
}