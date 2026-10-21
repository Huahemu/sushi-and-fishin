namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class RandomDriftRotation : MonoBehaviour
{
	public float velocityChangeSpeed = 1f;

	public float maxAngularVelocity = 90f;

	private Vector3 angularVelocity;

	private Vector3 noiseOffset;

	private void Start()
	{
		noiseOffset = new Vector3(Random.Range(0f, 1000f), Random.Range(0f, 1000f), Random.Range(0f, 1000f));
	}

	private void Update()
	{
		float num = Time.time * velocityChangeSpeed;
		angularVelocity.x = (Mathf.PerlinNoise(num + noiseOffset.x, 0f) - 0.5f) * 2f * maxAngularVelocity;
		angularVelocity.y = (Mathf.PerlinNoise(num + noiseOffset.y, 0f) - 0.5f) * 2f * maxAngularVelocity;
		angularVelocity.z = (Mathf.PerlinNoise(num + noiseOffset.z, 0f) - 0.5f) * 2f * maxAngularVelocity;
		base.transform.Rotate(angularVelocity * Time.deltaTime, Space.Self);
	}
}

}
