using System;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	public static class SurfWaveRoute
	{
		public static bool TryCreate(Vector3 restaurant, Vector3 rider, float waterLevel, Func<Vector3, float> depth, double time, uint id, out SurfWave wave)
		{
			wave = default;
			Vector3 vector = Vector3.ProjectOnPlane(rider - restaurant, Vector3.up);
			float magnitude = vector.magnitude;
			if (magnitude < 12f)
			{
				return false;
			}
			Vector3 vector2 = -vector.normalized;
			Vector3 vector3 = Vector3.Cross(Vector3.up, vector2);
			Vector3 vector4 = new Vector3(restaurant.x, waterLevel, restaurant.z);
			float num = magnitude + 55f;
			Vector3 vector5 = vector4 - vector2 * num;
			int num2 = Mathf.CeilToInt((num - magnitude) / 8f);
			for (int i = 0; i <= num2; i++)
			{
				Vector3 vector6 = Vector3.Lerp(vector5, vector4 - vector2 * magnitude, (float)i / (float)num2);
				vector6.y = waterLevel + 100f;
				if (depth(vector6) < 0.8f || depth(vector6 + vector3 * 14f) < 0.35f || depth(vector6 - vector3 * 14f) < 0.35f)
				{
					return false;
				}
			}
			float num3 = 8f;
			for (float num4 = magnitude - 4f; num4 >= 8f; num4 -= 4f)
			{
				Vector3 vector7 = vector4 - vector2 * num4;
				vector7.y = waterLevel + 100f;
				if (!(depth(vector7) >= 0.55f) || !(depth(vector7 + vector3 * 14f) >= 0.2f) || !(depth(vector7 - vector3 * 14f) >= 0.2f))
				{
					num3 = num4 + 4f;
					break;
				}
			}
			wave = new SurfWave
			{
				Origin = vector5,
				Direction = vector2,
				StartTime = time + 2.0,
				Duration = (num - num3) / 5f,
				Speed = 5f,
				Height = 2f,
				Width = 46f,
				Length = 18f,
				Id = id
			};
			return true;
		}
	}
}
