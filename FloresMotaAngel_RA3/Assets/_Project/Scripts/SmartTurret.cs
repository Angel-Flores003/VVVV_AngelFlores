using UnityEngine;

public class SmartTurret : MonoBehaviour
{
    public Transform player;
    public GameObject bulletPref;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ObjectPool pool;
    public float visionRange = 5f;
    public float bulletSpeed = 10f;
    private float timeBetweenShots = 1f;
    private float shootTimer = 0f;
    [SerializeField] private LayerMask obstacleLayer;

    void Update()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        player = playerObject.transform;

        Vector2 direction = (player.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, player.position);

        Debug.DrawRay(transform.position, direction * visionRange, Color.yellow);

        // Comprobar distancia
        if (distance > visionRange)
        {
            shootTimer = 0f;
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer);

        if (hit.collider != null)
        {
            shootTimer = 0f;
            return;
        }

        // Girar hacia el jugador
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Corregimos la orientación original del sprite
        angle -= 45f;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Disparar cada segundo
        shootTimer += Time.deltaTime;

        if (shootTimer >= timeBetweenShots)
        {
            Shoot(direction);
            shootTimer = 0f;
        }
    }

    void Shoot(Vector2 direction)
    {
        GameObject bullet = pool.GetObject(firePoint.position);

        // Reiniciar el temporizador de la bala
        TimedObject timedObject = bullet.GetComponent<TimedObject>();
        timedObject.SetPool(pool);

        // Darle velocidad hacia el jugador
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        bulletRb.linearVelocity = direction * bulletSpeed;
    }
}