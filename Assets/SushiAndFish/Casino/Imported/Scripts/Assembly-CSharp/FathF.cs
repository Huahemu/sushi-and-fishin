namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public static class FathF
{
	public static IEnumerator DelayedCall(float delay, Action action)
	{
		yield return new WaitForSeconds(delay);
		action?.Invoke();
	}

	public static Vector3 GetRandomPerpendicular(Vector3 input)
	{
		if (input == Vector3.zero)
		{
			throw new ArgumentException("Input vector cannot be zero.", "input");
		}
		Vector3 rhs = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
		return Vector3.Cross(input, rhs).normalized;
	}

	public static Vector3 GetHorizontalProjectionOfVector(Vector3 vector)
	{
		return new Vector3(vector.x, 0f, vector.z);
	}

	public static Vector3 GetRandomPointInCone(Vector3 origin, Vector3 direction, float angle, float distance)
	{
		direction = direction.normalized;
		float num = UnityEngine.Random.Range(Mathf.Cos(angle * (MathF.PI / 180f) / 2f), 1f);
		float f = UnityEngine.Random.Range(0f, MathF.PI * 2f);
		float num2 = Mathf.Sqrt(1f - num * num);
		float x = num2 * Mathf.Cos(f);
		float y = num2 * Mathf.Sin(f);
		Vector3 vector = new Vector3(x, y, num);
		float num3 = UnityEngine.Random.Range(0f, distance);
		Vector3 vector2 = Quaternion.LookRotation(direction) * vector;
		return origin + vector2 * num3;
	}

	public static T GetNextElement<T>(this List<T> list, int index)
	{
		if (index < 0 || index >= list.Count - 1)
		{
			return list[0];
		}
		return list[index + 1];
	}

	public static T GetRandomElement<T>(this List<T> list)
	{
		if (list == null || list.Count == 0)
		{
			throw new ArgumentException("List cannot be null or empty", "list");
		}
		return list[NetworkSingleton<SeededRandomManager>.Instance.Range(0, list.Count)];
	}

	public static void DestroyChildren(this Transform transform)
	{
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(transform.GetChild(num).gameObject);
		}
	}

	public static void DestroyChildrenImmediate(this Transform transform)
	{
		for (int num = transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.DestroyImmediate(transform.GetChild(num).gameObject);
		}
	}

	public static T Next<T>(this T src) where T : struct, Enum
	{
		T[] array = (T[])Enum.GetValues(typeof(T));
		int num = Array.IndexOf(array, src) + 1;
		if (num != array.Length)
		{
			return array[num];
		}
		return array[0];
	}

	public static T Previous<T>(this T src) where T : struct, Enum
	{
		T[] array = (T[])Enum.GetValues(typeof(T));
		int num = Array.IndexOf(array, src) - 1;
		if (num >= 0)
		{
			return array[num];
		}
		return array[^1];
	}

	public static float ClampAngleTo90(float angle)
	{
		angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
		return Mathf.Clamp(angle, -90f, 90f);
	}

	public static long RoundByFirstNDigits(long value, int firstDigits)
	{
		return (long)RoundByFirstNDigits((double)value, firstDigits);
	}

	public static double RoundByFirstNDigits(double value, int firstDigits)
	{
		if (value <= 0.0)
		{
			return value;
		}
		int num = (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
		if (firstDigits >= num)
		{
			return value;
		}
		int num2 = num - firstDigits;
		double num3 = Math.Pow(10.0, num2);
		return Math.Round(value / num3, MidpointRounding.AwayFromZero) * num3;
	}

	public static List<int> GetUniqueRandomNumbers(int count, int min, int max, bool shuffle = false)
	{
		if (min > max)
		{
			Debug.LogError("Min cannot be greater than Max.");
			return null;
		}
		int num = max - min + 1;
		if (count > num)
		{
			Debug.LogError($"Count cannot exceed the range size ({num}).");
			return null;
		}
		List<int> list = new List<int>();
		for (int i = min; i <= max; i++)
		{
			list.Add(i);
		}
		if (shuffle)
		{
			for (int num2 = list.Count - 1; num2 > 0; num2--)
			{
				int num3 = NetworkSingleton<SeededRandomManager>.Instance.Range(0, num2 + 1);
				List<int> list2 = list;
				int index = num2;
				List<int> list3 = list;
				int index2 = num3;
				int num4 = list[num3];
				int num5 = list[num2];
				int num6 = (list2[index] = num4);
				num6 = (list3[index2] = num5);
			}
		}
		return list.GetRange(0, count);
	}

	public static void RotateArrayDown<T>(T[] array)
	{
		if (array != null && array.Length > 1)
		{
			T val = array[^1];
			for (int num = array.Length - 1; num > 0; num--)
			{
				array[num] = array[num - 1];
			}
			array[0] = val;
		}
	}

	public static Vector3 NearestPointOnFiniteLine(Vector3 start, Vector3 end, Vector3 pnt)
	{
		Vector3 vector = end - start;
		float magnitude = vector.magnitude;
		vector.Normalize();
		float value = Vector3.Dot(pnt - start, vector);
		value = Mathf.Clamp(value, 0f, magnitude);
		return start + vector * value;
	}

	public static Vector3 NearestPointToRayOnLine(Vector3 start, Vector3 end, Vector3 origin, Vector3 direction, out float t, out float s)
	{
		Vector3 vector = end - start;
		Vector3 rhs = start - origin;
		float num = Vector3.Dot(vector, vector);
		float num2 = Vector3.Dot(vector, direction);
		float num3 = Vector3.Dot(direction, direction);
		float num4 = Vector3.Dot(vector, rhs);
		float num5 = Vector3.Dot(direction, rhs);
		float num6 = num * num3 - num2 * num2;
		if (Mathf.Abs(num6) < 1E-06f)
		{
			t = 0f;
			s = (0f - num5) / num3;
		}
		else
		{
			t = (num2 * num5 - num3 * num4) / num6;
			s = (num * num5 - num2 * num4) / num6;
		}
		t = Mathf.Clamp01(t);
		if (s < 0f)
		{
			s = 0f;
		}
		return start + vector * t;
	}

	public static void Teleport(this Rigidbody rb, Vector3 pos, bool resetVelocity = false)
	{
		RigidbodyInterpolation interpolation = rb.interpolation;
		rb.interpolation = RigidbodyInterpolation.None;
		rb.position = pos;
		if (!rb.isKinematic & resetVelocity)
		{
			rb.linearVelocity = Vector3.zero;
		}
		rb.interpolation = interpolation;
	}

	public static void Rotate(this Rigidbody rb, Quaternion rot, bool resetVelocity = false)
	{
		RigidbodyInterpolation interpolation = rb.interpolation;
		rb.interpolation = RigidbodyInterpolation.None;
		rb.rotation = rot;
		if (!rb.isKinematic & resetVelocity)
		{
			rb.angularVelocity = Vector3.zero;
		}
		rb.interpolation = interpolation;
	}

	public static Color PastelizeColor(this Color color, float saturation = 0.7f, float valueBoost = 0.2f)
	{
		Color.RGBToHSV(color, out var H, out var S, out var V);
		S *= saturation;
		V = Mathf.Lerp(V, 1f, valueBoost);
		Color result = Color.HSVToRGB(H, S, V);
		result.a = color.a;
		return result;
	}

	public static Quaternion LookRotationUpPriority(Vector3 forward, Vector3 up)
	{
		if (up.sqrMagnitude < Mathf.Epsilon)
		{
			return Quaternion.identity;
		}
		Vector3 normalized = up.normalized;
		Vector3 vector = Vector3.ProjectOnPlane(forward, normalized);
		if (vector.sqrMagnitude < Mathf.Epsilon)
		{
			vector = Vector3.Cross(normalized, Vector3.right);
			if (vector.sqrMagnitude < Mathf.Epsilon)
			{
				vector = Vector3.Cross(normalized, Vector3.forward);
			}
		}
		vector.Normalize();
		Vector3.Cross(normalized, vector);
		return Quaternion.LookRotation(vector, normalized);
	}
}

}
