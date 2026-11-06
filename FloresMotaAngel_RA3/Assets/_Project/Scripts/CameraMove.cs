using UnityEngine;

public class CameraMove : MonoBehaviour
{
	[SerializeField] private GameObject cameraPositions;

	private new Camera camera;

	private void OnEnable() => GameEvents.OnZoneChange += MoveCamera;
	private void OnDisable() => GameEvents.OnZoneChange -= MoveCamera;

	private void Awake() => camera = Camera.main;

	private void MoveCamera(int zone)
	{
		Transform zoneObject = cameraPositions.transform.GetChild(zone).transform;

		float x = zoneObject.position.x;
		float y = zoneObject.position.y;

		camera.transform.position = new Vector3(x, y, camera.transform.position.z);
	}
}