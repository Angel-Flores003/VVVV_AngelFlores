using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ChangeZone : MonoBehaviour
{
	[SerializeField] private int zone;

	BoxCollider2D boxCollider;

	private void Reset()
	{
		boxCollider = GetComponent<BoxCollider2D>();
		boxCollider.size = new Vector2(0.01f, 6f);
		boxCollider.isTrigger = true;
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.CompareTag("Player"))
		{
			GameEvents.ZoneChange(zone);
		}
	}
}