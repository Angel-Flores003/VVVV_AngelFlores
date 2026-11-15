using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class PatrolMovement : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private Transform groundController;
    [SerializeField] private float distance;
    [SerializeField] private bool moveR;
    private Rigidbody2D rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        RaycastHit2D ground = Physics2D.Raycast(groundController.position, Vector2.down, distance);

        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);

        if (ground == false)
        {
            Swith();
        }
    }

    public void Swith()
    {
        moveR = !moveR;
        transform.eulerAngles = new Vector3(0, transform.eulerAngles.y + 180, 0);
        speed *= -1;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(groundController.transform.position, groundController.transform.position + Vector3.down * distance);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.transform.CompareTag("Player"))
        {
            GameEvents.LoseLife(1);//Avisa
            Destroy(other.gameObject);
        }
    }
}