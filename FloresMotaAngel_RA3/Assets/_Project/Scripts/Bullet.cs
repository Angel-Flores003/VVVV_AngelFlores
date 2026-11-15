using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    /*[SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private int damage = 1;

    private float lifeTimer;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Init()
    {
        lifeTimer = lifeTime;
        rb.linearVelocity = transform.up * speed;
    }

    private void Update()
    {
        // TODO: Restem Time.deltaTime a lifeTimer.
        //       si arriba a 0 retorna la bullet amb BulletPool.Instance.ReturnBullet(this).
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            ObjectPool.Instance.ReturnObject(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameObject.activeSelf) return;

        if (other.transform.CompareTag("Player"))
        {
            GameEvents.LoseLife(damage);//Avisa
            Destroy(other.gameObject);
            ObjectPool.Instance.ReturnObject(this);
        }
    }

    internal void SetActive(bool v)
    {
        throw new NotImplementedException();
    }*/

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Handle player hit logic here
            Debug.Log("Player hit!");
        }
        // Destroy the bullet on collision
        Destroy(gameObject);
    }
}