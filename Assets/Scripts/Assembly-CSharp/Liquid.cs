using System;
using UnityEngine;

[ExecuteInEditMode]
public class Liquid : MonoBehaviour
{
	public enum UpdateMode
	{
		Normal = 0,
		UnscaledTime = 1
	}

	public UpdateMode updateMode;

	[SerializeField]
	private float MaxWobble = 0.03f;

	[SerializeField]
	private float WobbleSpeedMove = 1f;

	[SerializeField]
	private float fillAmount = 0.5f;

	[SerializeField]
	private float Recovery = 1f;

	[SerializeField]
	private float Thickness = 1f;

	[Range(0f, 1f)]
	public float CompensateShapeAmount;

	[SerializeField]
	private Mesh mesh;

	[SerializeField]
	private Renderer rend;

	private Vector3 pos;

	private Vector3 lastPos;

	private Vector3 velocity;

	private Quaternion lastRot;

	private Vector3 angularVelocity;

	private float wobbleAmountX;

	private float wobbleAmountZ;

	private float wobbleAmountToAddX;

	private float wobbleAmountToAddZ;

	private float pulse;

	private float sinewave;

	private float time = 0.5f;

	private Vector3 comp;

	private void Start()
	{
		GetMeshAndRend();
	}

	private void OnValidate()
	{
		GetMeshAndRend();
	}

	private void GetMeshAndRend()
	{
		if (mesh == null)
		{
			mesh = GetComponent<MeshFilter>().sharedMesh;
		}
		if (rend == null)
		{
			rend = GetComponent<Renderer>();
		}
	}

	private void Update()
	{
		float num = 0f;
		switch (updateMode)
		{
		case UpdateMode.Normal:
			num = Time.deltaTime;
			break;
		case UpdateMode.UnscaledTime:
			num = Time.unscaledDeltaTime;
			break;
		}
		time += num;
		if (num != 0f)
		{
			wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0f, num * Recovery);
			wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0f, num * Recovery);
			pulse = MathF.PI * 2f * WobbleSpeedMove;
			sinewave = Mathf.Lerp(sinewave, Mathf.Sin(pulse * time), num * Mathf.Clamp(velocity.magnitude + angularVelocity.magnitude, Thickness, 10f));
			wobbleAmountX = wobbleAmountToAddX * sinewave;
			wobbleAmountZ = wobbleAmountToAddZ * sinewave;
			velocity = (lastPos - base.transform.position) / num;
			angularVelocity = GetAngularVelocity(lastRot, base.transform.rotation);
			wobbleAmountToAddX += Mathf.Clamp((velocity.x + velocity.y * 0.2f + angularVelocity.z + angularVelocity.y) * MaxWobble, 0f - MaxWobble, MaxWobble);
			wobbleAmountToAddZ += Mathf.Clamp((velocity.z + velocity.y * 0.2f + angularVelocity.x + angularVelocity.y) * MaxWobble, 0f - MaxWobble, MaxWobble);
		}
		rend.sharedMaterial.SetFloat("_WobbleX", wobbleAmountX);
		rend.sharedMaterial.SetFloat("_WobbleZ", wobbleAmountZ);
		UpdatePos(num);
		lastPos = base.transform.position;
		lastRot = base.transform.rotation;
	}

	private void UpdatePos(float deltaTime)
	{
		Vector3 vector = base.transform.TransformPoint(new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z));
		if (CompensateShapeAmount > 0f)
		{
			if (deltaTime != 0f)
			{
				comp = Vector3.Lerp(comp, vector - new Vector3(0f, GetLowestPoint(), 0f), deltaTime * 10f);
			}
			else
			{
				comp = vector - new Vector3(0f, GetLowestPoint(), 0f);
			}
			pos = vector - base.transform.position - new Vector3(0f, fillAmount - comp.y * CompensateShapeAmount, 0f);
		}
		else
		{
			pos = vector - base.transform.position - new Vector3(0f, fillAmount, 0f);
		}
		rend.sharedMaterial.SetVector("_FillAmount", pos);
	}

	private Vector3 GetAngularVelocity(Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
	{
		Quaternion quaternion = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);
		if (Mathf.Abs(quaternion.w) > 0.9995117f)
		{
			return Vector3.zero;
		}
		float num2;
		if (quaternion.w < 0f)
		{
			float num = Mathf.Acos(0f - quaternion.w);
			num2 = -2f * num / (Mathf.Sin(num) * Time.deltaTime);
		}
		else
		{
			float num3 = Mathf.Acos(quaternion.w);
			num2 = 2f * num3 / (Mathf.Sin(num3) * Time.deltaTime);
		}
		Vector3 result = new Vector3(quaternion.x * num2, quaternion.y * num2, quaternion.z * num2);
		if (float.IsNaN(result.z))
		{
			return Vector3.zero;
		}
		return result;
	}

	private float GetLowestPoint()
	{
		float num = float.MaxValue;
		Vector3 vector = Vector3.zero;
		Vector3[] vertices = mesh.vertices;
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 vector2 = base.transform.TransformPoint(vertices[i]);
			if (vector2.y < num)
			{
				num = vector2.y;
				vector = vector2;
			}
		}
		return vector.y;
	}
}
