using UnityEngine;

public class Parallax : MonoBehaviour
{
	[SerializeField] private float parallaxMultiplier = 0.00001f;
	[SerializeField] protected Transform player;
	private Material parallaxMaterial;
	private float Playerx;

	private void Start()
	{
		parallaxMaterial = GetComponent<Renderer>().material;
		Playerx = player.position.x;
	}
	void Update()
	{
		float delatx = player.position.x - Playerx;
		parallaxMaterial.mainTextureOffset += new Vector2(delatx * parallaxMultiplier, 0);
		Playerx = player.position.x;
	}
}