using UnityEngine;

public class TimedObject : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private ObjectPool pool;
    private float timer;
    private bool returned = false;

    public void SetPool(ObjectPool newPool)
    {
        pool = newPool;
        timer = 0f;
        returned = false;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifeTime)
        {
            ReturnToPool();
        }
    }

    public void ReturnToPool()
    {
        if (returned || pool == null)
            return;

        returned = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        pool.ReturnObject(gameObject);
    }
}