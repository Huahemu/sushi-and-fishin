namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

public class BakedVolumeLight : MonoBehaviour
{
	public enum LightModes
	{
		Point = 0,
		Spot = 1
	}

	public LightModes mode;

	public Color color = Color.white;

	public float intensity = 1f;

	public float radius = 10f;

	[Range(0f, 1f)]
	public float falloff = 0.5f;

	[Range(0f, 1f)]
	[Tooltip("Percentage width at which the light should be full brightness. 1.0 means the entire cone is full bright, 0.0 means that the fade lerp starts immediately in the center")]
	public float coneFalloff = 0.9f;

	[Range(0f, 90f)]
	public float coneSize = 30f;

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = color;
		switch (mode)
		{
		case LightModes.Point:
			Gizmos.DrawWireSphere(base.transform.position, radius);
			break;
		case LightModes.Spot:
		{
			Vector3 vector = base.transform.position + base.transform.forward * radius;
			Gizmos.DrawLine(base.transform.position, vector);
			float num = coneSize * (MathF.PI / 90f);
			Vector3[] array = new Vector3[4]
			{
				vector + base.transform.up * num * radius,
				vector + base.transform.right * num * radius,
				vector + -base.transform.up * num * radius,
				vector + -base.transform.right * num * radius
			};
			Vector3[] array2 = array;
			foreach (Vector3 to in array2)
			{
				Gizmos.DrawLine(base.transform.position, to);
			}
			Gizmos.DrawLineStrip(array, looped: true);
			break;
		}
		}
	}

	public void Rebake()
	{
		UnityEngine.Object.FindAnyObjectByType<LightVolume>().Bake();
	}
}

}
