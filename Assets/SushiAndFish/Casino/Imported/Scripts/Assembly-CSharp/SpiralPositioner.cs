namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[ExecuteInEditMode]
public class SpiralPositioner : MonoBehaviour
{
	public enum Axis
	{
		X = 0,
		Y = 1,
		Z = 2
	}

	public bool enableOnEditor = true;

	public bool enableOnPlaymode;

	[Header("Spiral Settings")]
	public Axis axis = Axis.Y;

	public float radius = 2f;

	public float heightStep = 0.5f;

	public float startOffset = 90f;

	public float rotationOffset;

	[Header("Rotation Settings")]
	public bool setRotation;

	private void OnValidate()
	{
		if (enableOnEditor)
		{
			PositionChildren();
		}
	}

	private void Update()
	{
		if (Application.isPlaying)
		{
			if (enableOnPlaymode)
			{
				PositionChildren();
			}
		}
		else if (enableOnEditor)
		{
			PositionChildren();
		}
	}

	private void PositionChildren()
	{
		List<Transform> list = new List<Transform>();
		foreach (Transform item in base.transform)
		{
			if (item.gameObject.activeSelf)
			{
				list.Add(item);
			}
		}
		int count = list.Count;
		if (count == 0)
		{
			return;
		}
		for (int i = 0; i < count; i++)
		{
			Transform transform2 = list[i];
			float f = MathF.PI / 180f * ((float)i * 360f / (float)count + startOffset);
			Vector3 localPosition = Vector3.zero;
			switch (axis)
			{
			case Axis.X:
				localPosition = new Vector3(heightStep, Mathf.Cos(f) * radius, Mathf.Sin(f) * radius);
				break;
			case Axis.Y:
				localPosition = new Vector3(Mathf.Cos(f) * radius, heightStep, Mathf.Sin(f) * radius);
				break;
			case Axis.Z:
				localPosition = new Vector3(Mathf.Cos(f) * radius, Mathf.Sin(f) * radius, heightStep);
				break;
			}
			transform2.localPosition = localPosition;
			if (setRotation)
			{
				Vector3 normalized = transform2.localPosition.normalized;
				if (normalized != Vector3.zero)
				{
					transform2.localRotation = Quaternion.LookRotation(Vector3.forward, normalized) * Quaternion.Euler(rotationOffset, 0f, 0f);
				}
			}
		}
	}
}

}
