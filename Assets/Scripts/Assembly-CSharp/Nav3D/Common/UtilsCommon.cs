using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nav3D.Common
{
	public static class UtilsCommon
	{
		private class RandomSeedInvariant : IDisposable
		{
			private UnityEngine.Random.State m_RandomStateBuffer;

			public RandomSeedInvariant()
			{
				m_RandomStateBuffer = UnityEngine.Random.state;
			}

			public void Dispose()
			{
				UnityEngine.Random.state = m_RandomStateBuffer;
			}
		}

		public static IDisposable RandomSeedPermanence => new RandomSeedInvariant();

		public static Color[] GetNDistancedColors(int _N)
		{
			Color[] array = new Color[_N];
			float num = 360f / (float)_N / 360f;
			float num2 = 0f;
			for (int i = 0; i < _N; i++)
			{
				array[i] = Color.HSVToRGB(num2, 1f, 1f);
				num2 += num;
			}
			return array;
		}

		public static void SmartDestroy(UnityEngine.Object _Object)
		{
			if (!(_Object == null))
			{
				UnityEngine.Object.Destroy(_Object);
			}
		}

		public static string GetPointsString(IEnumerable<Vector3> _Points)
		{
			return "[" + string.Join(", ", _Points.Select((Vector3 _Target) => _Target.ToStringExt())) + "]";
		}

		public static Vector3[] TrimEqualPoints(Vector3[] _Points)
		{
			if (_Points == null || _Points.Length < 2)
			{
				return _Points?.ToArray();
			}
			List<Vector3> list = new List<Vector3>(_Points.Length) { _Points[0] };
			Vector3 vector = _Points[0];
			for (int i = 1; i < _Points.Length; i++)
			{
				Vector3 vector2 = _Points[i];
				if (!(vector == vector2))
				{
					list.Add(vector2);
					vector = vector2;
				}
			}
			return list.ToArray();
		}

		public static Vector3[] ReorderStartingFromClosest(Vector3[] _Points, Vector3 _Position, bool _Loop)
		{
			int num = -1;
			float num2 = float.MaxValue;
			List<Vector3> list = new List<Vector3>(_Points.Length);
			for (int i = 0; i < _Points.Length; i++)
			{
				Vector3 vector = _Points[i];
				float sqrMagnitude = (_Position - vector).sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					num = i;
				}
			}
			for (int j = num; j < _Points.Length; j++)
			{
				list.Add(_Points[j]);
			}
			if (_Loop)
			{
				for (int k = 0; k < num; k++)
				{
					list.Add(_Points[k]);
				}
			}
			return list.ToArray();
		}
	}
}
